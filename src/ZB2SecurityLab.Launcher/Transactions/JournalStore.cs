using System.Text.Json;
using System.Text.Json.Serialization;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Transactions;

internal sealed class JournalStore(LocalDataPaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal string Save(LaunchSessionJournal journal)
    {
        paths.EnsureCreated();
        journal.UpdatedUtc = DateTimeOffset.UtcNow;
        var destination = paths.JournalPath(journal.SessionId);
        var temporary = destination + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(journal, Options));
        File.Move(temporary, destination, overwrite: true);
        return destination;
    }

    internal LaunchSessionJournal Load(string path)
    {
        var journal = JsonSerializer.Deserialize<LaunchSessionJournal>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Journal vazio: {path}.");
        if (journal.SchemaVersion != 1 || string.IsNullOrWhiteSpace(journal.SessionId) || string.IsNullOrWhiteSpace(journal.GamePath))
        {
            throw new InvalidDataException($"Journal inválido ou incompatível: {path}.");
        }

        return journal;
    }

    internal IReadOnlyList<string> PendingJournalPaths()
    {
        paths.EnsureCreated();
        var pending = new List<string>();
        foreach (var path in Directory.EnumerateFiles(paths.Journals, "*.json"))
        {
            try
            {
                var journal = Load(path);
                if (journal.Phase is not JournalPhase.Completed)
                {
                    pending.Add(path);
                }
            }
            catch
            {
                pending.Add(path);
            }
        }

        return pending;
    }
}
