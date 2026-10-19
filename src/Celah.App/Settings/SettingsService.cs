using Celah.Core.Settings;
using Celah.Core.Storage;

namespace Celah.App.Settings;

/// <summary>Pengaturan pengguna: dimuat sekali, disimpan dan diumumkan setiap kali berubah.</summary>
public sealed class SettingsService
{
    private readonly JsonFileStore<AppSettings> _store = new(App.DataDirectory, "settings.json");

    public SettingsService()
    {
        Current = _store.Load().Normalize();
    }

    public AppSettings Current { get; }

    /// <summary>Pengaturan berubah (sudah disimpan). Semua perubahan langsung diterapkan, tanpa tombol Simpan.</summary>
    public event Action<AppSettings>? Changed;

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Current.Normalize();
        _store.Save(Current);
        Changed?.Invoke(Current);
    }
}
