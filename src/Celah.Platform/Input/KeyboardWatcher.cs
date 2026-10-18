using System.Runtime.InteropServices;

namespace Celah.Platform.Input;

/// <summary>
/// Caps Lock dan pergantian bahasa keyboard. Memakai low-level keyboard hook di thread sendiri
/// (supaya tidak dilepas Windows saat thread UI sibuk). Yang dibaca hanya kode tombol Caps Lock
/// dan tombol pengubah bahasa (Shift, Alt, Ctrl, Win, Spasi); tidak ada ketikan yang disimpan.
/// </summary>
public sealed class KeyboardWatcher : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_QUIT = 0x0012;
    private const int VK_CAPITAL = 0x14;
    private static readonly TimeSpan LanguageCheckDelay = TimeSpan.FromMilliseconds(150);

    private readonly HookProc _proc;
    private readonly Timer _languageTimer;
    private Thread? _thread;
    private uint _threadId;
    private bool _capsDown;
    private bool _capsOn;
    private int _lcid;

    public KeyboardWatcher()
    {
        _proc = HookCallback;
        _languageTimer = new Timer(_ => CheckLanguage(notify: true), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Caps Lock berubah: aktif?</summary>
    public event Action<bool>? CapsLockChanged;

    /// <summary>(LCID baru, LCID lama)</summary>
    public event Action<int, int>? LanguageChanged;

    public void Start()
    {
        _lcid = ForegroundLcid();
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            _capsOn = (GetKeyState(VK_CAPITAL) & 1) != 0;
            var hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            ready.Set();
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
        })
        {
            IsBackground = true,
            Name = "Celah keyboard hook",
        };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Jendela aktif berganti: bahasa per-aplikasi bisa berbeda, jadi catat diam-diam tanpa peek.
    /// </summary>
    public void SyncLanguageSilently() => CheckLanguage(notify: false);

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            int vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode
            bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool up = msg is WM_KEYUP or WM_SYSKEYUP;

            if (vk == VK_CAPITAL)
            {
                if (down && !_capsDown)
                {
                    // Hook dipanggil sebelum Windows memproses tombol, jadi status dihitung dari toggle.
                    _capsDown = true;
                    _capsOn = !_capsOn;
                    bool on = _capsOn;
                    ThreadPool.QueueUserWorkItem(_ => CapsLockChanged?.Invoke(on));
                }
                else if (up)
                {
                    _capsDown = false;
                }
            }
            else if (up && IsLanguageKey(vk))
            {
                // Win+Spasi, Alt+Shift, Ctrl+Shift: cek bahasa sebentar setelah tombol dilepas.
                _languageTimer.Change(LanguageCheckDelay, Timeout.InfiniteTimeSpan);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static bool IsLanguageKey(int vk) => vk is
        0x10 or 0xA0 or 0xA1 // Shift
        or 0x11 or 0xA2 or 0xA3 // Ctrl
        or 0x12 or 0xA4 or 0xA5 // Alt
        or 0x5B or 0x5C // Win
        or 0x20; // Spasi

    private void CheckLanguage(bool notify)
    {
        int lcid = ForegroundLcid();
        if (lcid == 0) return;
        int previous = Interlocked.Exchange(ref _lcid, lcid);
        if (notify && previous != 0 && previous != lcid)
            LanguageChanged?.Invoke(lcid, previous);
    }

    private static int ForegroundLcid()
    {
        var hwnd = GetForegroundWindow();
        uint thread = GetWindowThreadProcessId(hwnd, out _);
        return (int)((long)GetKeyboardLayout(thread) & 0xFFFF);
    }

    public void Dispose()
    {
        _languageTimer.Dispose();
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    // ── Interop ──────────────────────────────────────────────────────────────

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public int x, y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);
}
