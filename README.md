# Celah

Notch interaktif di tepi atas layar Windows 10/11 (WPF, .NET 8).

- Rancangan sistem: [docs/ARSITEKTUR.md](docs/ARSITEKTUR.md)
- Desain (handoff Claude Design): [design/README.md](design/README.md)

## Menjalankan

```bash
dotnet run --project src/Celah.App
```

Keluar lewat ikon tray → **Keluar**. Log tersimpan di `%AppData%\Celah\logs\`.

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

Argumen `--demo-peeks` memutar semua contoh peek untuk mengecek tampilan.

## Test

```bash
dotnet test
```
