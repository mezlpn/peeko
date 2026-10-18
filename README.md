# Celah

Notch interaktif di tepi atas layar Windows 10/11 (WPF, .NET 8).

- Rancangan sistem: [docs/ARSITEKTUR.md](docs/ARSITEKTUR.md)
- Desain (handoff Claude Design): [design/README.md](design/README.md)

## Menjalankan

```bash
dotnet run --project src/Celah.App
```

Keluar lewat ikon tray → **Keluar**. Log tersimpan di `%AppData%\Celah\logs\`.

## Test

```bash
dotnet test
```
