# Celah

Notch interaktif di tepi atas layar Windows 10/11 (WPF, .NET 8): musik dan lirik sinkron,
timer fokus, pengingat minum, to-do, rak file, serta info sistem singkat (volume, baterai,
Wi-Fi, headphone…).

**Unduh:** [Releases](https://github.com/mezlpn/celah/releases/latest) → `Celah-<versi>-win-x64.zip`.

- Rancangan sistem: [docs/ARSITEKTUR.md](docs/ARSITEKTUR.md)
- Desain (handoff Claude Design): [design/README.md](design/README.md)
- Ingin melapor bug atau mengusulkan fitur? Lihat [CONTRIBUTING.md](CONTRIBUTING.md).
- Lisensi: [MIT](LICENSE)

## Memakai versi rilis

1. Ekstrak `Celah-<versi>-win-x64.zip` ke folder mana saja (mis. `C:\Program Files\Celah` atau `Documents\Celah`).
2. Jalankan `Celah.exe`. Tidak perlu memasang .NET; semuanya ada di dalam satu file.
3. Windows SmartScreen mungkin memperingatkan karena aplikasinya belum ditandatangani:
   pilih **Info selengkapnya → Tetap jalankan**.

- **Pengaturan:** klik kiri ikon tray, atau klik kanan → Pengaturan.
- **Buka notch dari keyboard:** Win + Alt + C.
- **Mulai bersama Windows:** Pengaturan → Umum. Jika folder `Celah.exe` dipindah, matikan lalu nyalakan lagi.
- **Data:** `%AppData%\Celah\` (pengaturan, to-do, rak, cache lirik, log). Hapus folder ini untuk mulai dari nol.

Butuh Windows 10 versi 2004 (build 19041) atau lebih baru.

## Menjalankan dari kode

```bash
dotnet run --project src/Celah.App
```

Keluar lewat ikon tray → **Keluar**. Log tersimpan di `%AppData%\Celah\logs\`.

## Membuat rilis

```bash
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
```

Hasilnya `publish/Celah-<versi>-win-x64.zip`: satu `Celah.exe` mandiri (±180 MB, zip ±70 MB).
Rilis dibatalkan jika ada test yang gagal.

## Catatan pengembangan

Aplikasi Claude desktop adalah paket MSIX. Proses yang dijalankan dari sesi Claude Code ikut membawa
identitas paket itu, sehingga:

- prosesnya ikut ditutup saat sesi Claude berakhir;
- tulisannya ke `%AppData%\Celah` dialihkan ke
  `%LocalAppData%\Packages\Claude_<id>\LocalCache\Roaming\Celah`.

Untuk menjalankan Celah "normal" dari sesi Claude Code, buat prosesnya lewat WMI:

```bash
powershell -Command "Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = '\"<path>\\Celah.exe\"' }"
```

Argumen pengembang: `--demo-peeks` memutar semua contoh peek; `--settings` langsung membuka Pengaturan.

## Test

```bash
dotnet test
```
