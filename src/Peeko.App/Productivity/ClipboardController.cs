using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Peeko.App.Settings;
using Peeko.Core.Productivity;
using Peeko.Core.Storage;

namespace Peeko.App.Productivity;

/// <summary>
/// Riwayat clipboard (mati dari awal). Windows mengabari lewat WM_CLIPBOARDUPDATE (tanpa polling),
/// lalu teksnya dibaca di thread UI. Isi yang ditandai aplikasi sebagai rahasia (password manager)
/// atau tidak boleh masuk riwayat diabaikan, sama seperti riwayat clipboard Windows (Win + V).
/// </summary>
public sealed class ClipboardController : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly string[] SecretFormats =
        ["ExcludeClipboardContentFromMonitorProcessing", "Clipboard Viewer Ignore"];

    private readonly SettingsService _settings;
    private readonly JsonFileStore<ClipboardData> _store;
    private readonly AutoSaver _saver;
    private HwndSource? _window;
    private bool _ignoreNext;

    public ClipboardController(SettingsService settings, TimeProvider time)
    {
        _settings = settings;
        _store = new JsonFileStore<ClipboardData>(App.DataDirectory, "clipboard.json");
        History = new ClipboardHistory(time, _store.Load());
        _saver = new AutoSaver(() => _store.Save(History.Data));
        History.Changed += _saver.MarkDirty;
    }

    public ClipboardHistory History { get; }

    public bool Enabled => _window is not null;

    public event Action? EnabledChanged;

    public void Apply()
    {
        bool want = _settings.Current.ClipboardEnabled;
        if (want == Enabled) return;
        if (want)
        {
            // Jendela pesan tersembunyi (HWND_MESSAGE) hanya untuk menerima kabar clipboard.
            var p = new HwndSourceParameters("Peeko clipboard") { ParentWindow = new IntPtr(-3), WindowStyle = 0 };
            _window = new HwndSource(p);
            _window.AddHook(WndProc);
            AddClipboardFormatListener(_window.Handle);
        }
        else
        {
            RemoveClipboardFormatListener(_window!.Handle);
            _window.Dispose();
            _window = null;
            History.Clear(); // yang tidak di-pin langsung dilupakan
        }
        EnabledChanged?.Invoke();
    }

    /// <summary>Salin item kembali ke clipboard (tidak dicatat ulang sebagai salinan baru).</summary>
    public bool CopyBack(ClipItem item)
    {
        _ignoreNext = true;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(item.Text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(30); // clipboard sedang dipegang aplikasi lain
            }
        }
        _ignoreNext = false;
        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            handled = true;
            if (_ignoreNext)
            {
                _ignoreNext = false;
                return IntPtr.Zero;
            }
            // Baca sedikit kemudian: aplikasi sumber kadang masih menulis format lain.
            Application.Current.Dispatcher.BeginInvoke(Read, System.Windows.Threading.DispatcherPriority.Background);
        }
        return IntPtr.Zero;
    }

    private void Read()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var data = Clipboard.GetDataObject();
                if (data is null || !data.GetDataPresent(DataFormats.UnicodeText)) return;
                var formats = data.GetFormats(false);
                if (formats.Any(f => SecretFormats.Contains(f, StringComparer.OrdinalIgnoreCase))) return;
                if (formats.Contains("CanIncludeInClipboardHistory") && data.GetData("CanIncludeInClipboardHistory") is System.IO.MemoryStream ms
                    && ms.Length >= 4 && BitConverter.ToInt32(ms.ToArray(), 0) == 0) return;
                History.Add(data.GetData(DataFormats.UnicodeText) as string);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(30);
            }
        }
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            RemoveClipboardFormatListener(_window.Handle);
            _window.Dispose();
        }
        _saver.Flush();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
