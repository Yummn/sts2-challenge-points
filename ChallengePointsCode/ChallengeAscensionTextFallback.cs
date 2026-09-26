namespace ChallengePoints;

// Display-only compatibility. Never clamp the run's ascension or fabricate
// rules for unknown levels. Preserve every title available in either language.
internal static class ChallengeAscensionTextFallback
{
    internal static Dictionary<string, string> MissingTitles(int level, Func<string, bool> hasEntry, bool chinese)
    {
        var missing = new Dictionary<string, string>();
        for (int i = 1; i <= level; i++)
        {
            string key = $"LEVEL_{i:D2}.title";
            if (!hasEntry(key))
                missing[key] = chinese ? $"进阶 {i}（等级文本缺失）" : $"Ascension {i} (level text unavailable)";
        }
        return missing;
    }
}
