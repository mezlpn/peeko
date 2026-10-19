namespace Celah.Core.Productivity;

public sealed class WaterData
{
    public DateOnly Day { get; set; }
    public int Glasses { get; set; }
    public DateTimeOffset? LastDrink { get; set; }
    public DateTimeOffset? LastReminder { get; set; }
    public int Goal { get; set; } = 8;
    public int GlassMl { get; set; } = 250;
    public int IntervalMinutes { get; set; } = 60;
    /// <summary>Pengingat hanya antara jam ini (waktu lokal), sesuai Settings.dc.html.</summary>
    public int ActiveFromHour { get; set; } = 8;
    public int ActiveUntilHour { get; set; } = 21;
}

/// <summary>
/// Penghitung minum harian dengan pengingat berkala. Direset pukul 00.00 waktu lokal; pengingat
/// berhenti setelah target tercapai (WaterTodo.dc.html).
/// </summary>
public sealed class WaterTracker
{
    private readonly TimeProvider _time;
    private readonly WaterData _d;

    public WaterTracker(TimeProvider time, WaterData? restored = null)
    {
        _time = time;
        _d = restored ?? new WaterData();
        RollOverIfNewDay();
    }

    public event Action? Changed;

    public WaterData Data => _d;
    public int Glasses { get { RollOverIfNewDay(); return _d.Glasses; } }
    public int Goal => _d.Goal;
    public bool GoalReached => Glasses >= _d.Goal;
    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(5, _d.IntervalMinutes));

    /// <summary>Kapan pengingat berikutnya, atau null jika target sudah tercapai.</summary>
    public DateTimeOffset? NextReminder
    {
        get
        {
            if (GoalReached) return null;
            var activeFrom = TodayAt(_d.ActiveFromHour);
            var activeUntil = TodayAt(_d.ActiveUntilHour);

            // Dihitung dari minum terakhir atau pengingat terakhir, mana yang lebih baru;
            // tanpa catatan hari ini, pengingat pertama tepat saat jam aktif dimulai.
            var from = Max(_d.LastDrink, _d.LastReminder) ?? activeFrom - Interval;
            var next = from + Interval;

            // Hanya di jam aktif (default 08.00–21.00): sebelum → tunggu jam mulai, sesudah → besok pagi.
            if (next < activeFrom) next = activeFrom;
            if (next >= activeUntil) next = activeFrom.AddDays(1);

            var now = _time.GetUtcNow();
            return next < now ? now : next;
        }
    }

    public bool IsDue => NextReminder is { } next && next <= _time.GetUtcNow();

    public void Drink()
    {
        RollOverIfNewDay();
        _d.Glasses++;
        _d.LastDrink = _time.GetUtcNow();
        Changed?.Invoke();
    }

    public void Undo()
    {
        RollOverIfNewDay();
        if (_d.Glasses == 0) return;
        _d.Glasses--;
        Changed?.Invoke();
    }

    /// <summary>Pengingat sudah ditampilkan; berikutnya satu interval lagi walau belum minum.</summary>
    public void MarkReminded()
    {
        _d.LastReminder = _time.GetUtcNow();
        Changed?.Invoke();
    }

    private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b) =>
        a is null ? b : b is null ? a : a > b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    /// <summary>Jumlah liter dengan satu desimal bila perlu: "1,25 L".</summary>
    public static string Liters(int ml) =>
        (ml / 1000.0).ToString(ml % 1000 == 0 ? "0" : ml % 100 == 0 ? "0.0" : "0.00",
            System.Globalization.CultureInfo.GetCultureInfo("id-ID")) + " L";

    public void RollOverIfNewDay()
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        if (_d.Day == today) return;
        _d.Day = today;
        _d.Glasses = 0;
        _d.LastDrink = null;
        _d.LastReminder = null;
        Changed?.Invoke();
    }

    private DateTimeOffset TodayAt(int hour)
    {
        var local = _time.GetLocalNow();
        return new DateTimeOffset(local.Date.AddHours(hour), local.Offset);
    }
}
