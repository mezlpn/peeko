using System.Text.Json;

namespace Celah.Core.Storage;

/// <summary>
/// Satu file JSON di folder data (%AppData%\Celah). Penulisan atomik: tulis ke .tmp lalu ganti,
/// supaya file tidak rusak jika aplikasi mati di tengah penulisan. File rusak dianggap kosong.
/// </summary>
public sealed class JsonFileStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;

    public JsonFileStore(string directory, string fileName)
    {
        _path = Path.Combine(directory, fileName);
    }

    public T Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options) ?? new T();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // File rusak/terkunci: mulai dari kosong; file lama disimpan untuk diperiksa.
            TryBackupCorrupt();
        }
        return new T();
    }

    public void Save(T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Gagal menyimpan tidak boleh menjatuhkan aplikasi; data tetap ada di memori.
        }
    }

    private void TryBackupCorrupt()
    {
        try
        {
            File.Copy(_path, _path + ".rusak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
