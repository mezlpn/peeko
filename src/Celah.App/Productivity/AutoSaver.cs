using System.Windows.Threading;

namespace Celah.App.Productivity;

/// <summary>
/// Menyimpan data 0,5 dtk setelah perubahan terakhir (centang beruntun tidak menulis disk berkali-kali).
/// <see cref="Flush"/> dipanggil saat aplikasi keluar.
/// </summary>
public sealed class AutoSaver
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Action _save;
    private bool _dirty;

    public AutoSaver(Action save)
    {
        _save = save;
        _timer.Tick += (_, _) => Flush();
    }

    public void MarkDirty()
    {
        _dirty = true;
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        _timer.Stop();
        if (!_dirty) return;
        _dirty = false;
        _save();
    }
}
