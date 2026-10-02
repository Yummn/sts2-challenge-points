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
                    ["CHALLENGE_CONTRACT.description"] = "选择挑战词条赚取挑战点，购买分队和小商品；已选规则随本局保存。"
                }
                : new Dictionary<string, string>
                {
                    ["CHALLENGE_CONTRACT.title"] = "Challenge Contract",
                    ["CHALLENGE_CONTRACT.description"] = "Earn challenge points from contracts, then buy squads and supplies for this run."
                });
            manager.GetTable("card_selection").MergeWith(chinese
                ? new Dictionary<string, string>
                {
                    ["CHALLENGE_SELECT"] = "选择一张挑战奖励牌",
                    ["CHALLENGE_SHOP_POWER"] = "选择一张能力牌",
                    ["CHALLENGE_SHOP_START"] = "选择分队起始牌",
                    ["CHALLENGE_SHOP_ACT"] = "选择跨幕奖励牌",
                    ["CHALLENGE_SHOP_REMOVE"] = "选择要删除的牌",
                    ["CHALLENGE_SHOP_UPGRADE"] = "选择要升级的牌"
                }
                : new Dictionary<string, string>
                {
                    ["CHALLENGE_SELECT"] = "Choose a challenge reward card",
                    ["CHALLENGE_SHOP_POWER"] = "Choose a Power",
                    ["CHALLENGE_SHOP_START"] = "Choose a squad starter",
                    ["CHALLENGE_SHOP_ACT"] = "Choose an act reward",
                    ["CHALLENGE_SHOP_REMOVE"] = "Choose cards to remove",
                    ["CHALLENGE_SHOP_UPGRADE"] = "Choose a card to upgrade"
                });
            manager.GetTable("cards").MergeWith(chinese
                ? new Dictionary<string, string>
                {
                    ["CHALLENGE_LIGHT_VOUCHER.title"] = "光明券",
                    ["CHALLENGE_LIGHT_VOUCHER.description"] = "获得 1 点[gold]能量[/gold]。消耗。",
                    ["CHALLENGE_BANDAGE.title"] = "包扎",
                    ["CHALLENGE_BANDAGE.description"] = "回复 {Heal:diff()} 点生命。消耗。",
                    ["CHALLENGE_SPIRIT_MAKER.title"] = "生成灵体",
                    ["CHALLENGE_SPIRIT_MAKER.description"] = "获得 {Block:diff()} 点[gold]格挡[/gold]。获得 2 层[gold]易伤[/gold]。将 1 张[gold]灵体[/gold]加入抽牌堆。",
                    ["CHALLENGE_SPIRIT_PRINTER.title"] = "灵体印刷机",
                    ["CHALLENGE_SPIRIT_PRINTER.description"] = "获得 1 层[gold]无实体[/gold]和 2 层[gold]易伤[/gold]。将 1 张[gold]灵体[/gold]加入抽牌堆。",
                    ["CHALLENGE_MEAT_CLEAVER.title"] = "切肉刀",
                    ["CHALLENGE_MEAT_CLEAVER.description"] = "造成 {Damage:diff()} 点伤害。获得 {Block:diff()} 点[gold]格挡[/gold]。"
                }
                : new Dictionary<string, string>
                {
                    ["CHALLENGE_LIGHT_VOUCHER.title"] = "Light Voucher",
                    ["CHALLENGE_LIGHT_VOUCHER.description"] = "Gain 1 Energy. Exhaust.",
                    ["CHALLENGE_BANDAGE.title"] = "Bandage",
                    ["CHALLENGE_BANDAGE.description"] = "Heal {Heal:diff()} HP. Exhaust.",
                    ["CHALLENGE_SPIRIT_MAKER.title"] = "Create Apparition",
                    ["CHALLENGE_SPIRIT_MAKER.description"] = "Gain {Block:diff()} Block. Gain 2 Vulnerable. Add an Apparition to your draw pile.",
                    ["CHALLENGE_SPIRIT_PRINTER.title"] = "Apparition Printer",
                    ["CHALLENGE_SPIRIT_PRINTER.description"] = "Gain 1 Intangible and 2 Vulnerable. Add an Apparition to your draw pile.",
                    ["CHALLENGE_MEAT_CLEAVER.title"] = "Meat Cleaver",
                    ["CHALLENGE_MEAT_CLEAVER.description"] = "Deal {Damage:diff()} damage. Gain {Block:diff()} Block."
                });
            manager.GetTable("relics").MergeWith(chinese
                ? new Dictionary<string, string>
                {
                    ["CHALLENGE_FRUIT_KNIFE_RELIC.title"] = "水果刀",
                    ["CHALLENGE_FRUIT_KNIFE_RELIC.description"] = "在休息处可以选择削去 1 张牌，并获得 6 点最大生命。",
                    ["CHALLENGE_FRUIT_KNIFE_RELIC.flavor"] = "小小一刀，剔除累赘。"
                }
                : new Dictionary<string, string>
                {
                    ["CHALLENGE_FRUIT_KNIFE_RELIC.title"] = "Fruit Knife",
                    ["CHALLENGE_FRUIT_KNIFE_RELIC.description"] = "At a Rest Site, you may remove 1 card and gain 6 Max HP.",
                    ["CHALLENGE_FRUIT_KNIFE_RELIC.flavor"] = "A little cut removes a burden."
                });
            manager.GetTable("rest_site_ui").MergeWith(chinese
                ? new Dictionary<string, string>
                {
                    ["OPTION_CHALLENGE_FRUIT_KNIFE.name"] = "削牌",
                    ["OPTION_CHALLENGE_FRUIT_KNIFE.description"] = "删除 1 张牌，获得 6 点最大生命。"
                }
                : new Dictionary<string, string>
                {
                    ["OPTION_CHALLENGE_FRUIT_KNIFE.name"] = "Carve",
                    ["OPTION_CHALLENGE_FRUIT_KNIFE.description"] = "Remove 1 card. Gain 6 Max HP."
                });
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
