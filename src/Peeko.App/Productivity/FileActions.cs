using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;

namespace Peeko.App.Productivity;

/// <summary>
/// Aksi untuk file di rak (melengkapi Ubah format): zip, ekstrak, salin/pindah, Recycle Bin, bagikan.
/// Semua yang berat berjalan di thread latar; hasil baru diberi nama unik di sebelah file aslinya,
/// tidak pernah menimpa file yang sudah ada.
/// </summary>
public static class FileActions
{
    /// <summary>Kompres file/folder menjadi "nama.zip" di sebelahnya.</summary>
    public static Task<string> ZipAsync(string path) => Task.Run(() =>
    {
        var trimmed = path.TrimEnd('\\');
        var dir = Path.GetDirectoryName(trimmed) ?? throw new IOException("Lokasi tidak dikenali");
        var name = Directory.Exists(trimmed) ? Path.GetFileName(trimmed) : Path.GetFileNameWithoutExtension(trimmed);
        var target = Unique(Path.Combine(dir, name + ".zip"));
        if (Directory.Exists(trimmed))
        {
            ZipFile.CreateFromDirectory(trimmed, target, CompressionLevel.Optimal, includeBaseDirectory: true);
        }
        else
        {
            using var zip = ZipFile.Open(target, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(trimmed, Path.GetFileName(trimmed), CompressionLevel.Optimal);
        }
        return target;
    });

    public static bool IsZip(string path) => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path);

    /// <summary>Ekstrak ke folder bernama sama di sebelah file zip.</summary>
    public static Task<string> ExtractAsync(string zipPath) => Task.Run(() =>
    {
        var dir = Path.GetDirectoryName(zipPath) ?? throw new IOException("Lokasi tidak dikenali");
        var target = Unique(Path.Combine(dir, Path.GetFileNameWithoutExtension(zipPath)));
        // ExtractToDirectory menolak entri yang mencoba keluar dari folder tujuan ("../").
        ZipFile.ExtractToDirectory(zipPath, target);
        return target;
    });

    public static Task<string> CopyToAsync(string path, string folder) => Task.Run(() =>
    {
        var trimmed = path.TrimEnd('\\');
        var target = Unique(Path.Combine(folder, Path.GetFileName(trimmed)));
        if (Directory.Exists(trimmed)) CopyDirectory(trimmed, target);
        else File.Copy(trimmed, target);
        return target;
    });

    public static Task<string> MoveToAsync(string path, string folder) => Task.Run(() =>
    {
        var trimmed = path.TrimEnd('\\');
        var target = Unique(Path.Combine(folder, Path.GetFileName(trimmed)));
        if (Directory.Exists(trimmed))
        {
            // Directory.Move hanya bisa di drive yang sama; beda drive: salin lalu hapus.
            if (string.Equals(Path.GetPathRoot(trimmed), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase))
                Directory.Move(trimmed, target);
            else
            {
                CopyDirectory(trimmed, target);
                Directory.Delete(trimmed, recursive: true);
            }
        }
        else
        {
            File.Move(trimmed, target);
        }
        return target;
    });

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }

    /// <summary>"laporan.zip" → "laporan (2).zip" jika sudah ada.</summary>
    public static string Unique(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var ext = Directory.Exists(path) ? "" : Path.GetExtension(path);
        var name = ext.Length > 0 ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    // ── Recycle Bin ────────────────────────────────────────────────────────

    /// <summary>Pindahkan ke Recycle Bin (bisa dipulihkan), bukan dihapus permanen.</summary>
    public static bool Recycle(string path)
    {
        const uint FO_DELETE = 3;
        const ushort FOF_ALLOWUNDO = 0x40, FOF_NOCONFIRMATION = 0x10, FOF_SILENT = 0x4, FOF_NOERRORUI = 0x400;
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path.TrimEnd('\\') + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };
        return SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted && !File.Exists(path) && !Directory.Exists(path);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    // ── Bagikan (jendela Share Windows) ─────────────────────────────────────

    /// <summary>Buka jendela Bagikan Windows (Nearby Sharing, email, aplikasi lain) untuk file ini.</summary>
    public static bool Share(System.Windows.Window owner, string path)
    {
        try
        {
            var hwnd = new WindowInteropHelper(owner).Handle;
            var interop = DataTransferManager.As<IDataTransferManagerInterop>();
            var iid = new Guid("a5caee9b-8708-49d1-8d36-67d25a8da00c"); // IDataTransferManager
            var manager = MarshalInterface<DataTransferManager>.FromAbi(interop.GetForWindow(hwnd, iid));
            manager.DataRequested += async (_, args) =>
            {
                var deferral = args.Request.GetDeferral();
                try
                {
                    args.Request.Data.Properties.Title = Path.GetFileName(path.TrimEnd('\\'));
                    IStorageItem item = Directory.Exists(path)
                        ? await StorageFolder.GetFolderFromPathAsync(path)
                        : await StorageFile.GetFileFromPathAsync(path);
                    args.Request.Data.SetStorageItems([item]);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
                {
                    args.Request.FailWithDisplayText("File tidak bisa dibagikan.");
                }
                finally
                {
                    deferral.Complete();
                }
            };
            interop.ShowShareUIForWindow(hwnd);
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return false;
        }
    }

    [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow(IntPtr appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(IntPtr appWindow);
    }
}
