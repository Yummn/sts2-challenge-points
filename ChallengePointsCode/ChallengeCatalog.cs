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

    private static string PurchaseKey(string role, string kind, string id) => $"shop:{role}:{kind}:{id}";

    internal static int SquadRank(string role, string id) => Ranks.GetValueOrDefault(PurchaseKey(role, "squad", id));
    internal static bool HasItem(string role, string id) => Ranks.GetValueOrDefault(PurchaseKey(role, "item", id)) > 0;

    internal static int Spent(string role) => ChallengeShopCatalog.Squads.Sum(s => s.Price(SquadRank(role, s.Id)))
        + ChallengeShopCatalog.Items.Sum(i => HasItem(role, i.Id) ? i.Price : 0);

    internal static int Available(string role) => Score("common") + Score(role) - Spent(role);

    internal static bool SetSquadRank(string role, string id, int rank)
    {
        ChallengeSquad? squad = ChallengeShopCatalog.FindSquad(id);
        if (squad is null || (squad.Role != "common" && squad.Role != role)) return false;
        rank = Math.Clamp(rank, 0, 4);
        string key = PurchaseKey(role, "squad", id);
        int before = Ranks.GetValueOrDefault(key);
        if (Available(role) < squad.Price(rank) - squad.Price(before)) return false;
        if (rank == 0) Ranks.Remove(key);
        else Ranks[key] = rank;
        SavePreset();
        return true;
    }

    internal static bool SetItem(string role, string id, bool purchased)
    {
        ChallengeShopItem? item = ChallengeShopCatalog.FindItem(id);
        if (item is null || (purchased && !HasItem(role, id) && Available(role) < item.Price)) return false;
        string key = PurchaseKey(role, "item", id);
        if (purchased) Ranks[key] = 1;
        else Ranks.Remove(key);
        SavePreset();
        return true;
    }

    internal static int Rank(string id) => Ranks.TryGetValue(id, out int value) ? value : 0;

    internal static bool SetRank(string id, int value)
    {
        ChallengeDefinition? def = ChallengeCatalog.Find(id);
        if (def is null || !def.Selectable) return false;
        int rank = Math.Clamp(value, 0, def.MaxRank);
        int before = Rank(id);
        if (rank < before)
        {
            foreach (string role in new[] { "ironclad", "regent", "necrobinder", "silent", "defect" })
                if ((def.Role == "common" || def.Role == role) && Available(role) < (before - rank) * def.CpPerRank)
                    return false;
        }
        if (rank == 0) Ranks.Remove(id);
        else Ranks[id] = rank;
        SavePreset();
        return true;
    }

    internal static int Score(string role) => ChallengeCatalog.All
        .Where(d => d.Selectable && d.Role == role)
        .Sum(d => Rank(d.Id) * d.CpPerRank);

    internal static string Snapshot(string role)
    {
        // Old presets can become over-budget when challenge prices change.
        // Reconcile purchases before a new run rather than silently handing
        // out more squads than the selected challenges can pay for.
        if (ReconcileBudget(role)) SavePreset();
        return JsonSerializer.Serialize(Ranks
            .Where(kv => kv.Key.StartsWith($"shop:{role}:", StringComparison.Ordinal)
                || ChallengeCatalog.Find(kv.Key) is { Selectable: true } d && (d.Role == "common" || d.Role == role))
            .ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    private static bool ReconcileBudget(string role)
    {
        bool changed = false;
        while (Available(role) < 0)
        {
            ChallengeShopItem? item = ChallengeShopCatalog.Items.Reverse().FirstOrDefault(i => HasItem(role, i.Id));
            if (item is not null)
            {
                Ranks.Remove(PurchaseKey(role, "item", item.Id));
                changed = true;
                continue;
            }
            ChallengeSquad? squad = ChallengeShopCatalog.Squads.Reverse().FirstOrDefault(s => SquadRank(role, s.Id) > 0);
            if (squad is null) break;
            string key = PurchaseKey(role, "squad", squad.Id);
            int next = SquadRank(role, squad.Id) - 1;
            if (next == 0) Ranks.Remove(key);
            else Ranks[key] = next;
            changed = true;
        }
        return changed;
    }

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
                else if (pair.Key.StartsWith("shop:", StringComparison.Ordinal))
                {
                    string[] parts = pair.Key.Split(':');
                    if (parts.Length != 4) continue;
                    if (parts[2] == "squad" && ChallengeShopCatalog.FindSquad(parts[3]) is { } squad &&
                        (squad.Role == "common" || squad.Role == parts[1]))
                        Ranks[pair.Key] = Math.Clamp(pair.Value, 0, 4);
                    else if (parts[2] == "item" && ChallengeShopCatalog.FindItem(parts[3]) is not null)
                        Ranks[pair.Key] = Math.Clamp(pair.Value, 0, 1);
                }
            }
            bool corrected = false;
            foreach (string role in new[] { "ironclad", "regent", "necrobinder", "silent", "defect" })
                corrected |= ReconcileBudget(role);
            if (corrected) SavePreset();
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
