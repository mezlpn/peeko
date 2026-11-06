using System.Management;
using System.Runtime.InteropServices;

namespace Peeko.Platform.Power;

/// <summary>Satu pembacaan. Null = tidak tersedia di perangkat ini.</summary>
public sealed record StatsSample(
    double Cpu,
    double Ram,
    double RamUsedGb,
    double RamTotalGb,
    double? Gpu,
    double? TemperatureC,
    double? DiskFree,
    double DiskTotalGb);

/// <summary>
/// CPU, RAM, GPU, suhu, dan sisa disk C:. Dibaca hanya saat tab Sistem terlihat (pemiliknya yang
/// memanggil <see cref="Read"/> tiap beberapa detik), jadi tidak ada biaya saat Peeko diam.
/// </summary>
/// <remarks>
/// GPU dan suhu memakai penghitung kinerja Windows lewat WMI (tidak butuh admin). Suhu yang tersedia
/// adalah suhu zona termal ACPI, yang di banyak laptop mendekati suhu CPU; di PC tertentu tidak ada.
/// </remarks>
public sealed class SystemStats
{
    private ulong _idle, _kernel, _user;
    private bool _gpuMissing, _tempMissing;
    private double? _lastGpu, _lastTemp;

    /// <param name="slow">Ikut baca GPU dan suhu (±300 ms lewat WMI); kalau tidak, pakai nilai terakhir.</param>
    public StatsSample Read(bool slow = true)
    {
        double cpu = ReadCpu();

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);
        double total = mem.ullTotalPhys / 1073741824.0;
        double used = (mem.ullTotalPhys - mem.ullAvailPhys) / 1073741824.0;

        double? diskFree = null;
        double diskTotal = 0;
        try
        {
            var c = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            diskTotal = c.TotalSize / 1073741824.0;
            diskFree = c.AvailableFreeSpace / 1073741824.0;
        }
        catch (IOException)
        {
        }

        if (slow)
        {
            _lastGpu = ReadGpu();
            _lastTemp = ReadTemperature();
        }
        return new StatsSample(cpu, mem.dwMemoryLoad / 100.0, used, total, _lastGpu, _lastTemp, diskFree, diskTotal);
    }

    private double ReadCpu()
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt)) return 0;
        ulong idle = ToUlong(idleFt), kernel = ToUlong(kernelFt), user = ToUlong(userFt);
        // Pembacaan pertama belum punya pembanding.
        if (_kernel == 0)
        {
            (_idle, _kernel, _user) = (idle, kernel, user);
            Thread.Sleep(150);
            return ReadCpu();
        }
        ulong dIdle = idle - _idle, dKernel = kernel - _kernel, dUser = user - _user;
        (_idle, _kernel, _user) = (idle, kernel, user);
        ulong busy = dKernel + dUser; // waktu kernel sudah termasuk idle
        return busy == 0 ? 0 : Math.Clamp(1 - (double)dIdle / busy, 0, 1);
    }

    private double? ReadGpu()
    {
        if (_gpuMissing) return null;
        try
        {
            // Pemakaian per mesin GPU per proses; yang ditampilkan adalah jenis mesin tersibuk (biasanya 3D).
            var byEngine = new Dictionary<string, double>();
            using var searcher = new ManagementObjectSearcher(@"root\CIMV2",
                "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            foreach (ManagementObject o in searcher.Get())
            {
                using (o)
                {
                    var name = o["Name"] as string ?? "";
                    int i = name.IndexOf("engtype_", StringComparison.Ordinal);
                    if (i < 0) continue;
                    var type = name[(i + 8)..];
                    byEngine[type] = byEngine.GetValueOrDefault(type) + Convert.ToDouble(o["UtilizationPercentage"]);
                }
            }
            if (byEngine.Count == 0)
            {
                _gpuMissing = true;
                return null;
            }
            return Math.Clamp(byEngine.Values.Max() / 100, 0, 1);
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            _gpuMissing = true;
            return null;
        }
    }

    private double? ReadTemperature()
    {
        if (_tempMissing) return null;
        try
        {
            double? max = null;
            using var searcher = new ManagementObjectSearcher(@"root\CIMV2",
                "SELECT HighPrecisionTemperature, Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
            foreach (ManagementObject o in searcher.Get())
            {
                using (o)
                {
                    // HighPrecisionTemperature dalam sepersepuluh Kelvin; Temperature dalam Kelvin.
                    double kelvin = o["HighPrecisionTemperature"] is { } hp && Convert.ToDouble(hp) > 0
                        ? Convert.ToDouble(hp) / 10
                        : Convert.ToDouble(o["Temperature"]);
                    double c = kelvin - 273.15;
                    if (c is > 5 and < 125) max = Math.Max(max ?? c, c);
                }
            }
            if (max is null) _tempMissing = true;
            return max;
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            _tempMissing = true;
            return null;
        }
    }

    private static ulong ToUlong(FILETIME ft) => ((ulong)ft.High << 32) | ft.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}
