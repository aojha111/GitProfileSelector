using System.Text.Json;

namespace GitProfile.Core;

/// <summary>How a GitHub account should sign commits.</summary>
public sealed record Identity(string Login, string? Name, string? Email);

/// <summary>Thrown when the saved profile file cannot be understood.</summary>
public sealed class IdentityStoreCorruptException(string path, Exception cause)
    : Exception($"The profile file at {path} is not readable: {cause.Message}", cause);

public static class GitHubIdentity
{
    /// <summary>GitHub's privacy-preserving reply address for an account.</summary>
    public static string SuggestedEmail(string login) => $"{login}@users.noreply.github.com";
}

/// <summary>Names and emails per GitHub login, kept outside git config so switching stays cheap.</summary>
public sealed class IdentityStore(string filePath)
{
    private static readonly JsonSerializerOptions Writing = new() { WriteIndented = true };

    public IReadOnlyList<Identity> Load()
    {
        if (!File.Exists(filePath))
            return [];

        var entries = Read();
        return entries.Select(pair => new Identity(pair.Key, pair.Value.Name, pair.Value.Email)).ToList();
    }

    public Identity? Find(string login) =>
        Load().FirstOrDefault(i => string.Equals(i.Login, login, StringComparison.OrdinalIgnoreCase));

    public void Save(string login, string? name, string? email)
    {
        var entries = File.Exists(filePath) ? Read() : NewEntries();
        entries[login] = new Entry { Name = name, Email = email };
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(entries, Writing));
    }

    private Dictionary<string, Entry> Read()
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(filePath));
            return parsed is null ? NewEntries() : new Dictionary<string, Entry>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            throw new IdentityStoreCorruptException(filePath, ex);
        }
    }

    private static Dictionary<string, Entry> NewEntries() => new(StringComparer.OrdinalIgnoreCase);

    private sealed class Entry
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}
