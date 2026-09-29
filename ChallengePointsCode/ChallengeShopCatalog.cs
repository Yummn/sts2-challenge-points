namespace ChallengePoints;

internal sealed record ChallengeSquad(string Id, string Name, string Role, int[] TotalPrices, string[] TierDescriptions)
{
    internal int Price(int rank) => rank <= 0 ? 0 : TotalPrices[Math.Clamp(rank, 1, 4) - 1];
}

internal sealed record ChallengeShopItem(string Id, string Name, int Price, string Description);

internal static class ChallengeShopCatalog
{
    // Prices are cumulative totals, not per-tier supplements. Rank IV includes I–III.
    internal static readonly IReadOnlyList<ChallengeSquad> Squads = new[]
    {
        new ChallengeSquad("SQ-01", "余烬经销", "ironclad", new[] { 60, 70, 80, 100 },
            new[] { "获得3张愤怒。每回合多抽2张牌，失去1生命；每花费3能量，获得1力量。", "+每回合多抽1张牌。", "+每消耗3张牌，获得1力量。", "不再因本分队在回合开始失去1生命。" }),
        new ChallengeSquad("SQ-02", "向死而生", "ironclad", new[] { 50, 60, 70, 90 },
            new[] { "每回合开始失去2生命，抽1张牌；每回合第一张能力牌免费，但打出时失去4生命。", "+每回合获得一张0费光明券。", "+战斗结束时回复本战斗失去生命的50%。", "回复比例提高至60%。" }),
        new ChallengeSquad("SQ-03", "极限卡组", "necrobinder", new[] { 60, 70, 90, 110 },
            new[] { "每回合开始将1张灵魂或鬼火加入抽牌堆。", "生成的灵魂升级。", "每回合第一张被灵魂抽到的牌自动打出。", "每回合同时生成灵魂和鬼火。" }),
        new ChallengeSquad("SQ-04", "剧毒输出", "silent", new[] { 70, 80, 90, 100 },
            new[] { "获得3张毒雾；每对敌人施加负面状态，造成4伤害并施加2层中毒。", "触发伤害提高至6。", "中毒提高至3层。", "敌人的中毒不再自然衰减。" }),
        new ChallengeSquad("SQ-05", "变废为宝", "defect", new[] { 40, 50, 70, 100 },
            new[] { "每回合首次获得状态牌时生成1个随机充能球。", "每回合第3次获得状态牌时再生成1个随机充能球。", "首次获得状态牌时还获得1张燃料。", "每获得1张状态牌，本回合获得1临时集中。" }),
        new ChallengeSquad("SQ-06", "以印代抽", "regent", new[] { 60, 70, 90, 110 },
            new[] { "每回合开始随机生成1张本角色卡。", "开局从创世之柱和君权自授中选1张获得。", "第2、3幕开始时从创世之柱、武器库、光谱偏移中选1张获得。", "回合开始生成的随机牌已升级。" }),
        new ChallengeSquad("SQ-07", "攻防一体", "ironclad", new[] { 60, 70, 80, 100 },
            new[] { "开局将所有打击替换为铁斩波。", "开局升级所有铁斩波。", "开局获得遗物水果刀：火堆可删1张牌并获得6点最大生命。", "铁斩波可无限升级；击杀非爪牙敌人时随机升级1张。" }),
        new ChallengeSquad("SQ-08", "不确定", "common", new[] { 40, 50, 60, 70 },
            new[] { "随机获得1支一级分队。", "随机获得1支二级分队。", "随机获得1支三级分队。", "随机获得1支四级分队。" }),
        new ChallengeSquad("SQ-09", "灵体印刷", "necrobinder", new[] { 40, 50, 60, 80 },
            new[] { "获得1张生成灵体；每永久获得15张牌，再获得1张。", "改为获得灵体印刷机。", "获得的灵体印刷机升级。", "每回合多抽1张牌。" }),
        new ChallengeSquad("SQ-10", "铸刃", "regent", new[] { 60, 70, 90, 100 },
            new[] { "每花费1点能量，获得3铸造。", "获得淬炼刀刃与铸墙各1张。", "获得征召上前与武装各1张。", "第2、3幕开始时从追踪之刃和剑圣中选1张获得。" })
    };

    internal static readonly IReadOnlyList<ChallengeShopItem> Items = new[]
    {
        new ChallengeShopItem("IT-01", "小扭蛋", 10, "获得1个小扭蛋。"),
        new ChallengeShopItem("IT-02", "万花筒", 10, "获得1个万花筒。"),
        new ChallengeShopItem("IT-03", "能力三选一", 10, "从3张当前角色能力牌中选择1张。"),
        new ChallengeShopItem("IT-04", "钻石胸甲", 15, "每回合结束时获得5点格挡。"),
        new ChallengeShopItem("IT-05", "不死图腾", 15, "首次受到致命伤害时抵消，获得20格挡与6再生。"),
        new ChallengeShopItem("IT-06", "变化两张牌", 25, "选择2张牌并转化。"),
        new ChallengeShopItem("IT-07", "删除并升级", 25, "删除2张牌并升级1张。"),
        new ChallengeShopItem("IT-08", "基础牌升级", 30, "升级所有打击与防御。"),
        new ChallengeShopItem("IT-09", "冒牌会员卡", 30, "商店商品降价40%；购买时失去4生命，10次后失效。")
    };

    internal static ChallengeSquad? FindSquad(string id) => Squads.FirstOrDefault(s => s.Id == id);
    internal static ChallengeShopItem? FindItem(string id) => Items.FirstOrDefault(i => i.Id == id);
}
