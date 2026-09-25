using System.Text.Json;

namespace Registration.Storage;

/// <summary>
/// Cererile, codurile de invitatie si statisticile, intr-un singur fisier JSON scris
/// atomic (fisier temporar + rename). Volumul e mic (zeci-sute de cereri), deci totul
/// sta in memorie si se rescrie la fiecare schimbare.
/// </summary>
public sealed class RequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _lock = new();
    private readonly string _path;
    private StoreData _data;

    public RequestStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "registrations.json");
        _data = Load(_path);
    }

    private static StoreData Load(string path)
    {
        if (!File.Exists(path))
        {
            return new StoreData();
        }

        try
        {
            return JsonSerializer.Deserialize<StoreData>(File.ReadAllText(path), JsonOptions) ?? new StoreData();
        }
        catch (JsonException)
        {
            // Un fisier stricat nu trebuie sa opreasca plugin-ul; il pastram pentru investigatie.
            File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), overwrite: true);
            return new StoreData();
        }
    }

    /// <summary>Citire: primeste o copie consistenta a datelor.</summary>
    public T Read<T>(Func<StoreData, T> reader)
    {
        lock (_lock)
        {
            return reader(_data);
        }
    }

    /// <summary>Modificare + salvare atomica. Daca salvarea esueaza, datele din memorie revin.</summary>
    public T Write<T>(Func<StoreData, T> writer)
    {
        lock (_lock)
        {
            var backup = JsonSerializer.Serialize(_data, JsonOptions);
            try
            {
                var result = writer(_data);
                Save();
                return result;
            }
            catch
            {
                _data = JsonSerializer.Deserialize<StoreData>(backup, JsonOptions)!;
                throw;
            }
        }
    }

    public void Write(Action<StoreData> writer) => Write(d => { writer(d); return 0; });

    public void Count(string counter, DateTimeOffset now, int amount = 1) => Write(d => Increment(d, counter, now, amount));

    internal static void Increment(StoreData data, string counter, DateTimeOffset now, int amount = 1)
    {
        var day = now.UtcDateTime.ToString("yyyy-MM-dd");
        var stats = data.Stats.FirstOrDefault(s => s.Day == day);
        if (stats == null)
        {
            stats = new DailyStats { Day = day };
            data.Stats.Add(stats);
            data.Stats.RemoveAll(s => string.CompareOrdinal(s.Day, now.UtcDateTime.AddDays(-365).ToString("yyyy-MM-dd")) < 0);
        }

        stats.Counters[counter] = stats.Counters.GetValueOrDefault(counter) + amount;
    }

    /// <summary>Sterge tot (pentru „Sterge datele plugin-ului”).</summary>
    public void Clear() => Write(d =>
    {
        d.Requests.Clear();
        d.Invites.Clear();
        d.Stats.Clear();
    });

    private void Save()
    {
        // Date personale (nume, e-mail, telefon, IP): fisierul e citibil doar de utilizatorul emby.
        var temp = _path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(temp, options))
        {
            JsonSerializer.Serialize(stream, _data, JsonOptions);
        }

        File.Move(temp, _path, overwrite: true);
    }
}
