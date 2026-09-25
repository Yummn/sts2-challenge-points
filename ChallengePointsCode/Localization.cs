using MegaCrit.Sts2.Core.Localization;

namespace ChallengePoints;

internal static class ChallengeLocalization
{
    private static string? _language;

    internal static void Ensure()
    {
        try
        {
            LocManager manager = LocManager.Instance;
            string lang = manager.Language ?? "eng";
            if (_language == lang) return;
            bool chinese = lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            manager.GetTable("modifiers").MergeWith(chinese
                ? new Dictionary<string, string>
                {
                    ["CHALLENGE_CONTRACT.title"] = "挑战点契约",
                    ["CHALLENGE_CONTRACT.description"] = "通用与角色挑战点分别计算，并在达到各自阈值时给予奖励。"
                }
                : new Dictionary<string, string>
                {
                    ["CHALLENGE_CONTRACT.title"] = "Challenge Contract",
                    ["CHALLENGE_CONTRACT.description"] = "Common and character challenge points unlock separate rewards."
                });
            manager.GetTable("card_selection").MergeWith(chinese
                ? new Dictionary<string, string> { ["CHALLENGE_SELECT"] = "选择一张挑战奖励牌" }
                : new Dictionary<string, string> { ["CHALLENGE_SELECT"] = "Choose a challenge reward card" });
            manager.GetTable("powers").MergeWith(chinese
                ? new Dictionary<string, string>
                {
                    ["CHALLENGE_MALLEABLE_POWER.title"] = "坚韧",
                    ["CHALLENGE_MALLEABLE_POWER.description"] = "每次受到攻击后，获得等同于此层数的格挡，并使此层数增加 1。",
                    ["CHALLENGE_AMBERGRIS_POWER.title"] = "龙涎香",
                    ["CHALLENGE_AMBERGRIS_POWER.description"] = "本回合结束后，获得一个额外回合。"
                }
                : new Dictionary<string, string>
                {
                    ["CHALLENGE_MALLEABLE_POWER.title"] = "Malleable",
                    ["CHALLENGE_MALLEABLE_POWER.description"] = "After each attack hit, gain Block equal to this amount and increase it by 1.",
                    ["CHALLENGE_AMBERGRIS_POWER.title"] = "Ambergris",
                    ["CHALLENGE_AMBERGRIS_POWER.description"] = "Take one extra turn after this turn."
                });
#if !STS2_V110
            manager.GetTable("potions").MergeWith(chinese
                ? new Dictionary<string, string>
                {
                    ["CHALLENGE_AMBERGRIS.title"] = "龙涎香",
                    ["CHALLENGE_AMBERGRIS.description"] = "回复最大生命值的 50%。若在战斗中使用，本回合结束后获得一个额外回合。"
                }
                : new Dictionary<string, string>
                {
                    ["CHALLENGE_AMBERGRIS.title"] = "Ambergris",
                    ["CHALLENGE_AMBERGRIS.description"] = "Heal 50% of max HP. If used in combat, take one extra turn after this turn."
                });
#endif
            _language = lang;
        }
        catch (Exception ex) { MainFile.Logger.Warn($"[ChallengePoints] localization: {ex.Message}"); }
    }
}
