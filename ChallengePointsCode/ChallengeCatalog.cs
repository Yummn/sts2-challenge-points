using System.Reflection;
using System.Text.Json;
using Godot;

namespace ChallengePoints;

public sealed class ChallengeDefinition
{
    public string Id { get; set; } = "";
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CpPerRank { get; set; }
    public int MaxRank { get; set; }
    public bool Selectable { get; set; }
}

internal static class ChallengeCatalog
{
    internal static IReadOnlyList<ChallengeDefinition> All { get; private set; } = Array.Empty<ChallengeDefinition>();
    internal static readonly int[] CommonThresholds = { 15, 20, 35, 55, 70, 90 };
    internal static readonly int[] CharacterThresholds = { 10, 20, 35, 42 };

    internal static void Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChallengePoints.ChallengePointsData.json")
            ?? throw new FileNotFoundException("Embedded challenge data missing");
        All = JsonSerializer.Deserialize<List<ChallengeDefinition>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ChallengeDefinition>();
    }

    internal static ChallengeDefinition? Find(string id) => All.FirstOrDefault(x => x.Id == id);

    internal static string NormalizeRole(string entry)
    {
        string name = entry.ToLowerInvariant();
        if (name.Contains("ironclad")) return "ironclad";
        if (name.Contains("regent")) return "regent";
        if (name.Contains("necrobinder")) return "necrobinder";
        if (name.Contains("silent")) return "silent";
        if (name.Contains("defect")) return "defect";
        return "ironclad";
    }
}

internal static class ChallengeSelection
{
    private static readonly Dictionary<string, int> Ranks = new(StringComparer.Ordinal);
    private static readonly string PresetPath = Path.Combine(OS.GetUserDataDir(), "ChallengePoints", "preset.json");

    internal static int Rank(string id) => Ranks.TryGetValue(id, out int value) ? value : 0;

    internal static void SetRank(string id, int value)
    {
        ChallengeDefinition? def = ChallengeCatalog.Find(id);
        if (def is null || !def.Selectable) return;
        int rank = Math.Clamp(value, 0, def.MaxRank);
        if (rank == 0) Ranks.Remove(id);
        else Ranks[id] = rank;
        SavePreset();
    }

    internal static int Score(string role) => ChallengeCatalog.All
        .Where(d => d.Selectable && d.Role == role)
        .Sum(d => Rank(d.Id) * d.CpPerRank);

    internal static string Snapshot(string role) => JsonSerializer.Serialize(Ranks
        .Where(kv => ChallengeCatalog.Find(kv.Key) is { Selectable: true } d && (d.Role == "common" || d.Role == role))
        .ToDictionary(kv => kv.Key, kv => kv.Value));

    internal static void LoadPreset()
    {
        try
        {
            if (!File.Exists(PresetPath)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(PresetPath));
            if (data is null) return;
            foreach (var pair in data)
            {
                ChallengeDefinition? def = ChallengeCatalog.Find(pair.Key);
                if (def is { Selectable: true }) Ranks[pair.Key] = Math.Clamp(pair.Value, 0, def.MaxRank);
            }
        }
        catch (Exception ex) { MainFile.Logger.Warn($"[ChallengePoints] preset load failed: {ex.Message}"); }
    }

    private static void SavePreset()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PresetPath)!);
            string tmp = PresetPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Ranks));
            File.Move(tmp, PresetPath, true);
        }
        catch (Exception ex) { MainFile.Logger.Warn($"[ChallengePoints] preset save failed: {ex.Message}"); }
    }
}
