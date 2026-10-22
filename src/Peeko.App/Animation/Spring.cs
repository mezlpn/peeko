using System.Diagnostics;
using System.Windows.Media;

// Diadaptasi dari Bloom (github.com/SehajveerSingh2005/bloom, GPLv3): transisi pegas island.

namespace Peeko.App.Animation;

/// <summary>
/// Parameter pegas seperti framer-motion (dipakai Bloom): kekakuan, redaman, massa. Tidak ada durasi;
/// lama gerak muncul dari fisikanya sendiri.
/// </summary>
public readonly record struct SpringConfig(double Stiffness, double Damping, double Mass = 1, double RestDelta = 0.05)
{
    // Nilai dari Bloom (src/App.tsx, transisi island): tiap sifat bentuk punya pegas sendiri.
    public static readonly SpringConfig Width = new(400, 31);
    public static readonly SpringConfig Height = new(450, 29);
    public static readonly SpringConfig Offset = new(550, 45, 0.8);
    public static readonly SpringConfig Radius = new(1000, 40);
    public static readonly SpringConfig Default = new(500, 30, 1, 0.002);
    public static readonly SpringConfig Content = new(400, 30);
    public static readonly SpringConfig Bar = new(600, 30, 0.5);
}

/// <summary>
/// Satu nilai yang digerakkan pegas. Target boleh diganti kapan saja: kecepatan yang sedang berjalan
/// dipertahankan, jadi gerakan yang dibalik di tengah jalan tetap mulus (tidak patah seperti
/// animasi berdurasi tetap).
/// </summary>
public sealed class SpringValue
{
    private readonly Action<double> _apply;
    private SpringConfig _config;
    private double _startAt;

    public SpringValue(double initial, SpringConfig config, Action<double> apply)
    {
        Value = Target = initial;
        _config = config;
        _apply = apply;
    }

    public double Value { get; private set; }

    public double Velocity { get; private set; }

    public double Target { get; private set; }

    public bool IsMoving { get; private set; }

    /// <summary>Sekali, saat pegas berhenti di target berikutnya.</summary>
    public event Action? Settled;

    public void AnimateTo(double target, double delayMs = 0, SpringConfig? config = null)
    {
        if (config is { } c) _config = c;
        if (Motion.Reduced)
        {
            Set(target);
            return;
        }
        Target = target;
        _startAt = SpringTicker.Now + delayMs / 1000;
        if (!IsMoving && Math.Abs(Value - target) < _config.RestDelta)
        {
            Set(target);
            return;
        }
        IsMoving = true;
        SpringTicker.Add(this);
    }

    /// <summary>Lompat langsung ke nilai, tanpa gerak.</summary>
    public void Set(double value)
    {
        Value = Target = value;
        Velocity = 0;
        _apply(value);
        Stop();
    }

    private void Stop()
    {
        bool wasMoving = IsMoving;
        IsMoving = false;
        SpringTicker.Remove(this);
        if (wasMoving) RaiseSettled();
    }

    private void RaiseSettled()
    {
        var handler = Settled;
        Settled = null;
        handler?.Invoke();
    }

    /// <summary>Maju sejauh <paramref name="dt"/> detik. Mengembalikan false saat sudah diam.</summary>
    internal bool Step(double now, double dt)
    {
        if (now < _startAt) return true;

        // Euler semi-implisit dengan langkah 1 ms: stabil untuk kekakuan setinggi 1000.
        const double h = 0.001;
        for (double t = 0; t < dt; t += h)
        {
            double step = Math.Min(h, dt - t);
            double force = -_config.Stiffness * (Value - Target) - _config.Damping * Velocity;
            Velocity += force / _config.Mass * step;
            Value += Velocity * step;
        }

        bool resting = Math.Abs(Value - Target) < _config.RestDelta && Math.Abs(Velocity) < _config.RestDelta * 10;
        if (resting)
        {
            Value = Target;
            Velocity = 0;
        }
        _apply(Value);
        if (resting)
        {
            IsMoving = false;
            RaiseSettled();
        }
        return !resting;
    }
}

/// <summary>
/// Satu detak per frame layar untuk semua pegas aktif. Berlangganan CompositionTarget.Rendering hanya
/// selama ada yang bergerak, jadi saat diam Peeko tetap 0% CPU.
/// </summary>
internal static class SpringTicker
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly List<SpringValue> Active = [];
    private static double _last;
    private static bool _hooked;

    public static double Now => Clock.Elapsed.TotalSeconds;

    public static void Add(SpringValue spring)
    {
        if (!Active.Contains(spring)) Active.Add(spring);
        if (_hooked) return;
        _last = Now;
        CompositionTarget.Rendering += OnRendering;
        _hooked = true;
    }

    public static void Remove(SpringValue spring) => Active.Remove(spring);

    private static void OnRendering(object? sender, EventArgs e)
    {
        double now = Now;
        // Frame yang terlambat (mis. jendela baru muncul) tidak boleh melompatkan pegas jauh.
        double dt = Math.Min(now - _last, 1.0 / 30);
        _last = now;

        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (i >= Active.Count) continue;
            var spring = Active[i];
            // Handler Settled boleh langsung memberi target baru; pegas itu tetap di daftar.
            if (!spring.Step(now, dt) && !spring.IsMoving) Active.Remove(spring);
        }

        if (Active.Count == 0)
        {
            CompositionTarget.Rendering -= OnRendering;
            _hooked = false;
        }
    }
}
