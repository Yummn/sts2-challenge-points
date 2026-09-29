using System.Text.Json;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChallengePoints;

// A real run modifier, rather than a sidecar save, so the selected contract and
// its counters are serialized by the game's own run-save pipeline.
public sealed partial class ChallengeContract : ModifierModel
{
    private Dictionary<string, int>? _ranks;

    [SavedProperty] public string ContractData { get; set; } = "{}";
    [SavedProperty] public string CharacterRole { get; set; } = "ironclad";
    // Zero is an existing v0.1.x run. Those saves keep their original reward
    // contract; only newly created runs use the shop economy.
    [SavedProperty] public int ShopSchemaVersion { get; set; }
    [SavedProperty] public string RandomSquadId { get; set; } = "";
    [SavedProperty] public bool StartupRewardsGranted { get; set; }
    [SavedProperty] public bool FirstShopSeen { get; set; }
    [SavedProperty] public int MerchantCardPurchases { get; set; }
    [SavedProperty] public bool ShopTotemSpent { get; set; }
    [SavedProperty] public int ShopEnergySpent { get; set; }
    [SavedProperty] public int ShopExhausted { get; set; }
    [SavedProperty] public int ShopStatusesThisTurn { get; set; }
    [SavedProperty] public int ShopCombatStartHp { get; set; }
    [SavedProperty] public int ShopPowersPlayedThisTurn { get; set; }
    [SavedProperty] public int ShopCardsAcquired { get; set; }
    [SavedProperty] public int ShopStartupChoiceStage { get; set; }
    [SavedProperty] public int ShopActChoiceAct { get; set; } = -1;
    [SavedProperty] public int ShopActChoiceStage { get; set; }
    [SavedProperty] public bool ShopSoulAutoplayUsedThisTurn { get; set; }
    [SavedProperty] public int ColorlessPlayedThisTurn { get; set; }
    [SavedProperty] public int ZeroCostPlayedThisTurn { get; set; }
    [SavedProperty] public int PowerPlayedThisTurn { get; set; }
    [SavedProperty] public int EliteRewardsSuppressed { get; set; }
    [SavedProperty] public int LastEliteRewardFloor { get; set; } = -1;
    [SavedProperty] public int PotionsSuppressed { get; set; }
    [SavedProperty] public int FirstShopFloor { get; set; } = -1;
    [SavedProperty] public string LastRestOption { get; set; } = "";
    [SavedProperty] public int LastRestFloor { get; set; } = -100;
    [SavedProperty] public int ExhaustedThisTurn { get; set; }
    [SavedProperty] public int GeneratedThisTurn { get; set; }
    [SavedProperty] public int SoulGeneratedThisTurn { get; set; }
    [SavedProperty] public int VoidPlayedThisTurn { get; set; }
    [SavedProperty] public bool VoidExhaustedThisTurn { get; set; }
    [SavedProperty] public int SlyPlayedThisTurn { get; set; }
    [SavedProperty] public int CardsPlayedThisTurn { get; set; }
    [SavedProperty] public int NonHandDrawsThisTurn { get; set; }
    [SavedProperty] public bool BurningBloodAdjusted { get; set; }
    [SavedProperty] public int ColorlessGeneratedThisTurn { get; set; }
    [SavedProperty] public int SoulPlayedThisTurn { get; set; }
    [SavedProperty] public int ForgeDecayCount { get; set; }
    [SavedProperty] public int StatusReroutedThisTurn { get; set; }
    private bool _copyingCurse;
    private bool _grantingStartupPotion;
    private bool _addingStatus;
    private Player? _shopTotemPendingPlayer;

    public override LocString Title => new("modifiers", "CHALLENGE_CONTRACT.title");
    public override LocString Description => new("modifiers", "CHALLENGE_CONTRACT.description");

    private Dictionary<string, int> Ranks => _ranks ??= JsonSerializer.Deserialize<Dictionary<string, int>>(ContractData)
        ?? new Dictionary<string, int>();

    internal int Rank(string id) => Ranks.TryGetValue(id, out int rank) ? rank : 0;
    internal int SquadRank(string id)
    {
        int direct = Rank($"shop:{CharacterRole}:squad:{id}");
        return id == RandomSquadId ? Math.Max(direct, Rank($"shop:{CharacterRole}:squad:SQ-08")) : direct;
    }
    internal bool HasShopItem(string id) => Rank($"shop:{CharacterRole}:item:{id}") > 0;
    internal int CommonCp => ChallengeCatalog.All.Where(x => x.Role == "common").Sum(x => Rank(x.Id) *
        (ShopSchemaVersion == 0 && x.Id is "G-01" or "G-02" or "G-03" or "G-13" or "G-14" or "G-25"
            ? 1 : x.CpPerRank));
    internal int RoleCp => ChallengeCatalog.All.Where(x => x.Role == CharacterRole).Sum(x => Rank(x.Id) * x.CpPerRank);

    protected override void AfterRunCreated(RunState runState)
    {
        ChallengeLocalization.Ensure();
        ChallengeShopCards.EnsurePools();
        if (ShopSchemaVersion > 0 && Rank($"shop:{CharacterRole}:squad:SQ-08") > 0 && RandomSquadId.Length == 0)
        {
            var pool = ChallengeShopCatalog.Squads.Where(s => s.Id != "SQ-08").ToArray();
            RandomSquadId = runState.Rng.Niche.NextItem(pool)?.Id ?? "";
        }
        foreach (Player player in runState.Players)
        {
            if (Rank("G-02") > 0)
                player.Creature.SetMaxHpInternal(Math.Max(1, player.Creature.MaxHp - 3 * Rank("G-02")));
            if (Rank("G-03") > 0)
                player.Gold = Math.Max(0, player.Gold - 25 * Rank("G-03"));
            if (ShopSchemaVersion == 0 && RoleCp >= 20)
                ReplaceStartingCards(runState, player);
            if (ShopSchemaVersion == 0 && CommonCp >= 70)
                UpgradeStartingCards(player);
            if (ShopSchemaVersion > 0 && HasShopItem("IT-08"))
                foreach (CardModel card in player.Deck.Cards.Where(c => c.Id.Entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase)
                    || c.Id.Entry.Contains("DEFEND", StringComparison.OrdinalIgnoreCase)).ToArray())
                    Upgrade(card);
            if (Rank("G-10") > 0)
            {
                CardModel bane = runState.CreateCard(ModelDb.Card<AscendersBane>(), player);
                player.Deck.AddInternal(bane);
            }
            if (ShopSchemaVersion > 0) GrantPurchasedStartingCards(runState, player);
            // Starting-deck grants are not player card acquisitions. In
            // particular, SQ-09's every-15 counter starts at zero.
            AttachPlayerEvents(player);
        }
        MainFile.Logger.Info($"[ChallengePoints] run created: common={CommonCp}, role={RoleCp}, character={CharacterRole}.");
    }

    protected override void AfterRunLoaded(RunState runState)
    {
        ChallengeLocalization.Ensure();
        ChallengeShopCards.EnsurePools();
        foreach (Player player in runState.Players) AttachPlayerEvents(player);
    }

    private void AttachPlayerEvents(Player player)
    {
        if (Rank("G-09") > 0) player.RelicObtained += relic => OnRelicObtained(player, relic);
        if (Rank("G-11") > 0) player.Deck.CardAdded += card => OnDeckCardAdded(player, card);
        if (ShopSchemaVersion > 0 && SquadRank("SQ-09") > 0)
            player.Deck.CardAdded += card => OnShopDeckCardAdded(player, card);
    }

    private void OnRelicObtained(Player player, RelicModel relic)
    {
        if (relic.Rarity != RelicRarity.Ancient) return;
        try
        {
            CardModel[] curses = ModelDb.AllCards.Where(c => c.Rarity == CardRarity.Curse && c.Id.Entry != "ASCENDERS_BANE").ToArray();
            if (curses.Length == 0) return;
            CardModel? selected = base.RunState.Rng.Niche.NextItem(curses);
            if (selected is null) return;
            CardModel curse = base.RunState.CreateCard(selected, player);
            player.Deck.AddInternal(curse);
        }
        catch (Exception ex) { MainFile.Logger.Warn($"[ChallengePoints] ancient curse failed: {ex.Message}"); }
    }

    private void OnDeckCardAdded(Player player, CardModel card)
    {
        if (_copyingCurse || card.Rarity != CardRarity.Curse) return;
        try
        {
            _copyingCurse = true;
            CardModel duplicate = base.RunState.CreateCard(ModelDb.GetById<CardModel>(card.Id), player);
            player.Deck.AddInternal(duplicate);
        }
        catch (Exception ex) { MainFile.Logger.Warn($"[ChallengePoints] curse copy failed: {ex.Message}"); }
        finally { _copyingCurse = false; }
    }

    // Stable model IDs: localized titles differ between v107/v110 and translation packs.
    private static readonly Dictionary<string, string> CardIds = new(StringComparer.Ordinal)
    {
        ["双重打击"] = "TWIN_STRIKE", ["耸肩无视"] = "SHRUG_IT_OFF",
        ["碰撞碎屑"] = "COLLISION_COURSE", ["收集星辉"] = "GATHER_LIGHT",
        ["戳击"] = "POKE", ["违逆"] = "DEFY", ["突然一拳"] = "SUCKER_PUNCH",
        ["斗篷与匕首"] = "CLOAK_AND_DAGGER", ["充电"] = "CHARGE_BATTERY",
        ["球状闪电"] = "BALL_LIGHTNING", ["燃烧"] = "INFLAME",
        ["环绕轨道"] = "ORBIT", ["友谊"] = "FRIENDSHIP",
        ["灵动步伐"] = "FOOTWORK", ["碎片整理"] = "DEFRAGMENT",
        ["黑暗之拥"] = "DARK_EMBRACE", ["无惧疼痛"] = "FEEL_NO_PAIN",
        ["撕裂"] = "RUPTURE", ["祭品"] = "OFFERING", ["狱火"] = "INFERNO",
        ["创世之柱"] = "PILLAR_OF_CREATION", ["招架"] = "PARRY",
        ["群星之子"] = "CHILD_OF_THE_STARS", ["光谱偏移"] = "SPECTRUM_SHIFT",
        ["大爆炸"] = "BIG_BANG", ["致死性"] = "LETHALITY",
        ["死亡之舞"] = "DANSE_MACABRE", ["钙质化"] = "CALCIFY",
        ["精神过载"] = "NEUROSURGE", ["余像"] = "AFTERIMAGE",
        ["刀扇"] = "FAN_OF_KNIVES", ["青雾"] = "HAZE",
        ["肾上腺素"] = "ADRENALINE", ["精准"] = "ACCURACY",
        ["子程序"] = "SUBROUTINE", ["雷霆"] = "THUNDER",
        ["散热片"] = "BD_HEATSINKS", ["变废为宝"] = "TRASH_TO_TREASURE",
        ["壁垒"] = "BARRICADE", ["绯红披风"] = "CRIMSON_MANTLE",
        ["坚定不移"] = "UNMOVABLE", ["武器库"] = "ARSENAL",
        ["剑圣"] = "SWORD_SAGE", ["追踪刃"] = "SEEKING_EDGE",
        ["创世纪"] = "GENESIS", ["死神形态"] = "REAPER_FORM",
        ["血肉戏法"] = "SLEIGHT_OF_FLESH", ["触媒"] = "ACCELERANT",
        ["群蛇形态"] = "SERPENT_FORM", ["计划妥当"] = "WELL_LAID_PLANS",
        ["创造性 AI"] = "CREATIVE_AI", ["回响形态"] = "ECHO_FORM",
        ["愤怒"] = "ANGER", ["灵魂"] = "SOUL", ["鬼火"] = "WISP",
        ["毒雾"] = "NOXIOUS_FUMES", ["燃料"] = "FUEL", ["铁斩波"] = "IRON_WAVE",
        ["光明券"] = "CHALLENGE_LIGHT_VOUCHER", ["生成灵体"] = "CHALLENGE_SPIRIT_MAKER",
        ["灵体印刷机"] = "CHALLENGE_SPIRIT_PRINTER", ["切肉刀"] = "CHALLENGE_MEAT_CLEAVER",
        ["追猎之刃"] = "SEEKING_EDGE", ["追踪之刃"] = "SEEKING_EDGE",
        ["君权自授"] = "MANIFEST_AUTHORITY", ["淬炼刀刃"] = "REFINE_BLADE",
        ["筑墙"] = "BULWARK", ["铸墙"] = "BULWARK",
        ["征召上前"] = "SUMMON_FORTH", ["武装"] = "ARMAMENTS"
    };

    private static CardModel? FindCard(string title)
    {
        // Purchased cards are injected into ModelDb but deliberately excluded
        // from ordinary reward pools / AllCards.
        if (title == "光明券") return ModelDb.Card<ChallengeLightVoucher>();
        if (title == "生成灵体") return ModelDb.Card<ChallengeSpiritMaker>();
        if (title == "灵体印刷机") return ModelDb.Card<ChallengeSpiritPrinter>();
        if (title == "切肉刀") return ModelDb.Card<ChallengeMeatCleaver>();
        if (CardIds.TryGetValue(title, out string? id))
        {
            CardModel? mapped = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (mapped is not null) return mapped;
        }
        return ModelDb.AllCards.FirstOrDefault(c => c.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
            ?? ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry.Equals(title, StringComparison.OrdinalIgnoreCase));
    }

    internal static string AuditRewardNames()
    {
        string[] roleTen = { "燃烧", "环绕轨道", "友谊", "灵动步伐", "碎片整理" };
        string[] replacements = { "双重打击", "耸肩无视", "碰撞碎屑", "收集星辉", "戳击", "违逆", "突然一拳", "斗篷与匕首", "充电", "球状闪电" };
        string[] missing = BaseRewardPools.Values.Concat(ExtraRewardPools.Values)
            .SelectMany(x => x).Concat(roleTen).Concat(replacements).Distinct()
            .Where(x => FindCard(x) is null).ToArray();
        return $"cards missing {missing.Length}: {string.Join(", ", missing)}";
    }

    private static void Upgrade(CardModel card)
    {
        if (!card.IsUpgradable) return;
        card.UpgradeInternal();
        card.FinalizeUpgradeInternal();
    }

    private void ReplaceStartingCards(RunState run, Player player)
    {
        (string attack, string defense) = CharacterRole switch
        {
            "ironclad" => ("双重打击", "耸肩无视"),
            "regent" => ("碰撞碎屑", "收集星辉"),
            "necrobinder" => ("戳击", "违逆"),
            "silent" => ("突然一拳", "斗篷与匕首"),
            "defect" => ("充电", "球状闪电"),
            _ => ("", "")
        };
        ReplaceOne(attack, true);
        ReplaceOne(defense, false);

        void ReplaceOne(string title, bool strike)
        {
            CardModel? canonical = FindCard(title);
            if (canonical is null)
            {
                MainFile.Logger.Warn($"[ChallengePoints] starting replacement unavailable: {title}");
                return;
            }
            CardModel? old = player.Deck.Cards.FirstOrDefault(c =>
                c.Id.Entry.Contains(strike ? "STRIKE" : "DEFEND", StringComparison.OrdinalIgnoreCase)
                || c.Title.Contains(strike ? "打击" : "防御", StringComparison.Ordinal));
            if (old is null) return;
            int index = player.Deck.Cards.ToList().IndexOf(old);
            player.Deck.RemoveInternal(old);
            run.RemoveCard(old);
            CardModel replacement = run.CreateCard(canonical, player);
            player.Deck.AddInternal(replacement, index);
        }
    }

    private static void UpgradeStartingCards(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToArray()) Upgrade(card);
    }

    public override decimal ModifyGoldGained(Player player, decimal amount) =>
        Rank("G-05") > 0 && amount > 0 ? Math.Floor(amount * (1m - 0.1m * Rank("G-05"))) : amount;

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost)
    {
        if (ShopSchemaVersion == 0 && CommonCp >= 90 && FirstShopSeen && base.RunState.TotalFloor == FirstShopFloor && entry is not MerchantRelicEntry)
            return 0;
        decimal adjusted = Rank("G-04") > 0 ? Math.Ceiling(cost * (1m + 0.1m * Rank("G-04"))) : cost;
        if (ShopSchemaVersion > 0 && HasShopItem("IT-09") && MerchantCardPurchases < 10 &&
            entry is not MerchantCardRemovalEntry)
            adjusted = Math.Ceiling(adjusted * 0.6m);
        return adjusted;
    }

    private static CardModel GenerateForCombat(CardModel canonical, Player owner) =>
        (owner.Creature.CombatState ?? throw new InvalidOperationException("No combat state for generated card"))
        .CreateCard(canonical, owner);

    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (ShopSchemaVersion > 0 && HasShopItem("IT-09") && MerchantCardPurchases < 10 &&
            itemPurchased is not MerchantCardRemovalEntry)
        {
            MerchantCardPurchases++;
            player.Creature.LoseHpInternal(4, ValueProp.Unblockable | ValueProp.Unpowered);
        }
        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        int turn = player.PlayerCombatState?.TurnNumber ?? 0;
        if (turn >= 1 && turn <= Rank("G-06")) count = Math.Max(0, count - 1);
        if (turn == 1 && CharacterRole == "silent" && Rank("SL-01") > 0) count = Math.Max(0, count - 1);
        if (ShopSchemaVersion > 0)
        {
            if (SquadRank("SQ-01") > 0) count += SquadRank("SQ-01") >= 2 ? 3 : 2;
            if (SquadRank("SQ-02") > 0) count += 1;
            if (SquadRank("SQ-09") >= 4) count += 1;
        }
        return count;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (ShopSchemaVersion > 0 && SquadRank("SQ-02") > 0 && card.Type == CardType.Power &&
            card.Owner.PlayerCombatState is not null && ShopPowersPlayedThisTurn == 0)
        {
            modifiedCost = 0;
            return true;
        }
        return false;
    }

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (CharacterRole == "ironclad" && Rank("IC-04") > 0 && target.IsPlayer && cardSource?.Owner == target.Player && amount > 0)
            return amount + 1;
        return amount;
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (ShopSchemaVersion > 0 && HasShopItem("IT-05") && !ShopTotemSpent &&
            target.IsPlayer && amount > 0 && amount >= target.CurrentHp && target.Player is { } player)
        {
            ShopTotemSpent = true;
            _shopTotemPendingPlayer = player;
            return 0;
        }
        return amount;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        Player? player = _shopTotemPendingPlayer;
        _shopTotemPendingPlayer = null;
        if (player?.Creature.CombatState is null) return;
        await CreatureCmd.GainBlock(player.Creature, 20m, ValueProp.Unpowered, null);
        await PowerCmd.Apply<RegenPower>(new ThrowingPlayerChoiceContext(), player.Creature, 6, null, null);
    }

#if STS2_V110
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
#endif
    {
        if (CharacterRole == "necrobinder" && Rank("NB-04") > 0 && target is { IsPlayer: true } && dealer is { Side: CombatSide.Enemy }
            && dealer.GetPowerAmount<DoomPower>() > dealer.CurrentHp)
            return 1.5m;
        return 1m;
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (CharacterRole == "ironclad" && Rank("IC-05") > 0 && target.IsPlayer && canonicalPower is VulnerablePower && amount > 0)
        {
            modifiedAmount = amount + 1;
            return true;
        }
        if (CharacterRole == "ironclad" && Rank("IC-08") > 0 && target.Side == CombatSide.Enemy && canonicalPower is WeakPower)
        {
            modifiedAmount = 0;
            return true;
        }
        if (CharacterRole == "silent" && Rank("SL-07") > 0 && target.Side == CombatSide.Enemy && canonicalPower is PoisonPower && amount > 0)
        {
            modifiedAmount = Math.Max(0, 50 - target.GetPowerAmount<PoisonPower>());
            return true;
        }
        return false;
    }

    public override decimal ModifyOrbValue(OrbModel orb, decimal value)
    {
        if (CharacterRole == "defect" && Rank("DF-02") > 0 && orb is LightningOrb or FrostOrb)
            return Math.Min(33, value);
        if (CharacterRole == "defect" && Rank("DF-06") > 0)
        {
            if (orb is DarkOrb) return Math.Min(33, value);
            if (orb is PlasmaOrb) return Math.Max(0, value - 1);
        }
        return value;
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy) return;
        int hpRank = Rank("G-01");
        if (hpRank > 0)
        {
            int increased = (int)Math.Ceiling(creature.MaxHp * (1m + 0.1m * hpRank));
            creature.SetMaxHpInternal(increased);
            creature.SetCurrentHpInternal(increased);
        }
        var context = new ThrowingPlayerChoiceContext();
        if (Rank("G-12") > 0)
            await PowerCmd.Apply<PlatingPower>(context, creature, Math.Max(1, base.RunState.TotalFloor) * 3, null, null);
        if (Rank("G-17") > 0)
            await PowerCmd.Apply<RegenPower>(context, creature, 2 + Rank("G-17"), null, null);
        if (Rank("G-18") > 0)
            await PowerCmd.Apply<StrengthPower>(context, creature, 1, null, null);
        if (Rank("G-22") > 0)
            await PowerCmd.Apply<BarricadePower>(context, creature, 1, null, null);
        if (CharacterRole == "silent" && Rank("SL-03") > 0)
            await PowerCmd.Apply<ChallengeMalleablePower>(context, creature, 2, null, null);
    }

    public override async Task AfterOstyRevived(Creature osty)
    {
        if (CharacterRole == "necrobinder" && Rank("NB-01") > 0 && osty.PetOwner?.PlayerCombatState?.TurnNumber >= 4)
            await PowerCmd.Remove<DieForYouPower>(osty);
    }

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator is null) return;
        if (ShopSchemaVersion > 0 && card.Type == CardType.Status)
            await HandleShopStatusGenerated(card, creator);
        if (_addingStatus) return;
        if (CharacterRole == "regent" && Rank("RG-05") > 0 && card.Pool is ColorlessCardPool
            && ++ColorlessGeneratedThisTurn > 5)
        {
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            return;
        }
        if (CharacterRole == "regent" && Rank("RG-04") > 0 && ++GeneratedThisTurn % 2 == 0)
        {
            CardModel[] statuses = ModelDb.AllCards.Where(c => c.Rarity == CardRarity.Status && c is Wound or Dazed).ToArray();
            if (statuses.Length > 0)
            {
                _addingStatus = true;
                try
                {
                    if (base.RunState.Rng.Niche.NextItem(statuses) is { } status)
                        await CardPileCmd.AddGeneratedCardToCombat(GenerateForCombat(status, creator), PileType.Hand, creator);
                }
                finally { _addingStatus = false; }
            }
        }
        if (CharacterRole == "necrobinder" && Rank("NB-03") > 0 && card.Id.Entry.Contains("SOUL", StringComparison.OrdinalIgnoreCase)
            && ++SoulGeneratedThisTurn == 1)
        {
            _addingStatus = true;
            try { await CardPileCmd.AddGeneratedCardToCombat(GenerateForCombat(ModelDb.Card<Dazed>(), creator), PileType.Draw, creator, CardPilePosition.Top); }
            finally { _addingStatus = false; }
        }
    }

    public override async Task AfterCardExhausted(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, CardModel card, bool causedByEthereal)
    {
        if (ShopSchemaVersion > 0 && SquadRank("SQ-01") >= 3 && ++ShopExhausted % 3 == 0)
            await PowerCmd.Apply<StrengthPower>(context, card.Owner.Creature, 1, null, null);
        if (CharacterRole == "ironclad")
        {
            if (Rank("IC-03") > 0 && ++ExhaustedThisTurn % 2 == 0)
                await CardPileCmd.AddGeneratedCardToCombat(GenerateForCombat(ModelDb.Card<Wound>(), card.Owner), PileType.Hand, card.Owner);
            if (Rank("IC-07") > 0 && card is AscendersBane)
                card.Owner.Creature.LoseHpInternal(1, ValueProp.Unblockable | ValueProp.Unpowered);
        }
        if (CharacterRole == "necrobinder" && Rank("NB-07") > 0 && card.Id.Entry.Contains("VOID", StringComparison.OrdinalIgnoreCase))
            VoidExhaustedThisTurn = true;
    }

    public override async Task AfterCardDrawn(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        if (_shopWatchingSoulDraw && _shopSoulDrawnCard is null && card.Owner == base.RunState.Players.FirstOrDefault())
            _shopSoulDrawnCard = card;
        if (CharacterRole == "silent" && Rank("SL-02") > 0 && !fromHandDraw && ++NonHandDrawsThisTurn == 1)
            await CardPileCmd.AddGeneratedCardToCombat(GenerateForCombat(ModelDb.Card<Dazed>(), card.Owner), PileType.Draw, card.Owner, CardPilePosition.Top);
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ShopSchemaVersion > 0 || CharacterRole != "defect" || Rank("DF-04") <= 0 || card.Type != CardType.Status || card.Pile?.Type != PileType.Discard)
            return;
        if (++StatusReroutedThisTurn % 2 == 0)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ShopSchemaVersion > 0 && SquadRank("SQ-03") >= 3 && !ShopSoulAutoplayUsedThisTurn &&
            cardPlay.Card is Soul)
        {
            _shopWatchingSoulDraw = true;
            _shopSoulDrawnCard = null;
        }
        if (CharacterRole == "necrobinder" && Rank("NB-02") > 0 && cardPlay.Card is Soul soul && ++SoulPlayedThisTurn == 1)
            soul.DynamicVars.Cards.BaseValue = 1;
        return Task.CompletedTask;
    }

    public override Task AfterFlush(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext, Player player,
        IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        if (CharacterRole != "regent") return Task.CompletedTask;
        foreach (SovereignBlade blade in retainedCards.OfType<SovereignBlade>())
        {
            if (Rank("RG-03") > 0 && blade.EnergyCost.GetWithModifiers(CostModifiers.Local) > blade.EnergyCost.Canonical)
                blade.EnergyCost.AddThisCombat(-1);
            if (Rank("RG-07") > 0 && ForgeDecayCount < Math.Max(1, base.RunState.TotalFloor))
            {
                decimal amount = Math.Min(8m, Math.Max(0m, blade.DynamicVars.Damage.BaseValue - 10m));
                if (amount > 0) { blade.AddDamage(-amount); ForgeDecayCount++; }
            }
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0) return;
        if (CharacterRole == "silent" && Rank("SL-08") > 0 && power is WeakPower && power.Owner.Side == CombatSide.Enemy)
            await PowerCmd.Apply<TemporaryStrengthPower>(context, power.Owner, 1, null, null);
        if (CharacterRole == "defect" && Rank("DF-01") > 0 && power is FocusPower && power.Owner.IsPlayer)
            await PowerCmd.Apply<StrengthPower>(context, power.Owner, -amount, null, null);
        if (ShopSchemaVersion > 0) await HandleShopDebuffApplied(context, power, amount, applier);
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        MainFile.Logger.Info($"[ChallengePoints] room entered: type={room.RoomType}, common={CommonCp}, startupGranted={StartupRewardsGranted}; interactive rewards deferred.");
        if (room is MerchantRoom && !FirstShopSeen)
        {
            FirstShopSeen = true;
            FirstShopFloor = base.RunState.TotalFloor;
        }
        if (ShopSchemaVersion > 0 && room is CombatRoom)
        {
            ShopEnergySpent = ShopExhausted = ShopStatusesThisTurn = ShopPowersPlayedThisTurn = 0;
            ShopCombatStartHp = base.RunState.Players.FirstOrDefault()?.Creature.CurrentHp ?? 0;
        }
        foreach (Player player in base.RunState.Players)
        {
            if (CharacterRole == "ironclad" && Rank("IC-01") > 0 && !BurningBloodAdjusted)
            {
                if (player.Relics.OfType<BurningBlood>().FirstOrDefault() is { } blood)
                {
                    blood.DynamicVars.Heal.BaseValue = Math.Max(0m, blood.DynamicVars.Heal.BaseValue - 2m);
                    BurningBloodAdjusted = true;
                }
            }
        }
        // EventRoom.Enter awaits this hook BEFORE RunManager.FadeIn. In
        // particular SmallCapsule.AfterObtained waits for a reward screen to
        // close, which cannot be clicked behind the transition's black veil.
        // Interactive rewards are granted by the post-FadeIn hook instead.
        return Task.CompletedTask;
    }

    private bool _grantingStartupRewards;

    internal async Task GrantStartupRewardsAfterFadeIn()
    {
        if (StartupRewardsGranted || _grantingStartupRewards) return;
        _grantingStartupRewards = true;
        StartupRewardsGranted = true;
        try
        {
            MainFile.Logger.Info($"[ChallengePoints] startup rewards begin after room fade-in: common={CommonCp}, role={RoleCp}.");
            foreach (Player player in base.RunState.Players)
            {
                if (ShopSchemaVersion > 0)
                {
                    if (HasShopItem("IT-01")) await GiveRelic(player, "小扭蛋");
                    if (HasShopItem("IT-02")) await GiveRelic(player, "万花筒");
                    continue;
                }
                if (CommonCp >= 15)
                {
                    MainFile.Logger.Info("[ChallengePoints] granting startup Gorget.");
                    await GiveRelic(player, "护喉甲");
                }
                if (CommonCp >= 20)
                {
                    MainFile.Logger.Info("[ChallengePoints] granting startup SmallCapsule; reward window is now visible.");
                    await GiveRelic(player, "小扭蛋");
                    MainFile.Logger.Info("[ChallengePoints] startup SmallCapsule reward window closed.");
                }
                if (CommonCp >= 35)
                {
                    _grantingStartupPotion = true;
                    try { await GivePotion(player, "龙涎香"); }
                    finally { _grantingStartupPotion = false; }
                }
            }
            // Room-entry saving happens before FadeIn. Rewards are deliberately
            // later, so persist their inventory and one-shot flag together after
            // interaction completes. Never overwrite a mid-combat checkpoint.
            if (RunManager.Instance.ShouldSave && base.RunState.CurrentRoom is EventRoom &&
                ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), base.RunState))
            {
                await SaveManager.Instance.SaveRun(null, false);
                MainFile.Logger.Info("[ChallengePoints] startup rewards and granted flag persisted.");
            }
            MainFile.Logger.Info("[ChallengePoints] startup rewards complete.");
        }
        finally { _grantingStartupRewards = false; }
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (Rank("G-16") <= 0 || room is not CombatRoom { RoomType: RoomType.Elite }) return false;
        int floor = base.RunState.TotalFloor;
        if (EliteRewardsSuppressed >= Rank("G-16")) return false;
        if (LastEliteRewardFloor == floor) return false;
        LastEliteRewardFloor = floor;
        EliteRewardsSuppressed++;
        rewards.Clear();
        return true;
    }

    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions)
    {
        if (player.RunState.CurrentRoom?.RoomType != RoomType.Monster) return false;
        bool modified = false;
        if (Rank("G-07") > 0)
        {
            CardModel[] alternatives = ModelDb.AllCards.Where(c => c.Rarity == CardRarity.Uncommon && c.Pool == player.Character.CardPool).ToArray();
            foreach (CardCreationResult option in options)
            {
                if (option.Card.Rarity != CardRarity.Rare || alternatives.Length == 0) continue;
                if (base.RunState.Rng.Niche.NextItem(alternatives) is not { } replacementModel) continue;
                CardModel replacement = base.RunState.CreateCard(replacementModel, player);
                option.ModifyCard(replacement);
                modified = true;
            }
        }
        if (Rank("G-19") > 0 && options.Count == 3)
        {
            options.RemoveAt(2);
            modified = true;
        }
        return modified;
    }

    public override bool ShouldAllowMerchantCardRemoval(Player player)
    {
        int rank = Rank("G-15");
        return rank <= 0 || player.ExtraFields.CardShopRemovalsUsed < 6 - rank;
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (Rank("G-23") <= 0 || string.IsNullOrEmpty(LastRestOption)
            || base.RunState.TotalFloor != LastRestFloor + 1) return false;
        // Never remove the last available choice; special rest-site actions stay legal.
        var blocked = options.Where(o => o.GetType().Name == LastRestOption).ToArray();
        if (blocked.Length == 0 || options.Count <= blocked.Length) return false;
        foreach (RestSiteOption option in blocked) options.Remove(option);
        return true;
    }

    public override Task AfterRestSiteSmith(Player player)
    {
        LastRestOption = nameof(SmithRestSiteOption);
        LastRestFloor = base.RunState.TotalFloor;
        return Task.CompletedTask;
    }

    public override async Task AfterPotionProcured(PotionModel potion)
    {
        if (_grantingStartupPotion || Rank("G-24") <= PotionsSuppressed) return;
        PotionsSuppressed++;
        await PotionCmd.Discard(potion);
    }

    private static async Task GiveRelic(Player player, string name)
    {
        string id = name switch { "护喉甲" => "GORGET", "小扭蛋" => "SMALL_CAPSULE", "万花筒" => "KALEIDOSCOPE", _ => name };
        RelicModel? relic = ModelDb.AllRelics.FirstOrDefault(x => x.Id.Entry == id)
            ?? ModelDb.AllRelics.FirstOrDefault(x => x.Title.GetFormattedText().Equals(name, StringComparison.Ordinal));
        if (relic is null) { MainFile.Logger.Warn($"[ChallengePoints] relic unavailable: {name}"); return; }
        await RelicCmd.Obtain(relic.ToMutable(), player);
    }

    private static async Task GivePotion(Player player, string name)
    {
        string id = name == "龙涎香" ? "AMBERGRIS" : name;
        PotionModel? potion = ModelDb.AllPotions.FirstOrDefault(x => x.Id.Entry == id)
            ?? ModelDb.AllPotions.FirstOrDefault(x => x.Title.GetFormattedText().Equals(name, StringComparison.Ordinal));
#if !STS2_V110
        // Ambergris was added after PC v107.1. Grant our equivalent event potion.
        if (potion is null && name == "龙涎香")
        {
            potion = ModelDb.Potion<ChallengeAmbergris>();
        }
#endif
        if (potion is null) { MainFile.Logger.Warn($"[ChallengePoints] potion unavailable: {name}"); return; }
        await PotionCmd.TryToProcure(potion.ToMutable(), player);
    }

    public override async Task BeforeHandDraw(Player player, MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        ChallengeLocalization.Ensure();
        int turn = player.PlayerCombatState?.TurnNumber ?? 0;
        var context = new ThrowingPlayerChoiceContext();
        // v2's status tax is a once-per-turn transfer, not an interception of
        // the second status as it enters discard. Bottom placement is stable
        // across save/load and does not consume the combat RNG.
        if (ShopSchemaVersion > 0 && CharacterRole == "defect" && Rank("DF-04") > 0 && StatusReroutedThisTurn == 0)
        {
            CardModel? status = PileType.Discard.GetPile(player).Cards.FirstOrDefault(c => c.Type == CardType.Status);
            if (status is not null)
            {
                await CardPileCmd.Add(status, PileType.Draw, CardPilePosition.Bottom);
                StatusReroutedThisTurn = 1;
            }
        }
        if (ShopSchemaVersion > 0)
            await ApplyShopTurnStart(player, choiceContext);
        if (turn == 1)
        {
            ColorlessPlayedThisTurn = ZeroCostPlayedThisTurn = PowerPlayedThisTurn = 0;
            ForgeDecayCount = 0;
            if (ShopSchemaVersion == 0 && CommonCp >= 55)
                await PowerCmd.Apply<PlatingPower>(context, player.Creature, 5, player.Creature, null);
            if (Rank("G-13") > 0)
                await PowerCmd.Apply<WeakPower>(context, player.Creature, Rank("G-13"), null, null);
            if (Rank("G-14") > 0)
                await PowerCmd.Apply<FrailPower>(context, player.Creature, Rank("G-14"), null, null);
            if (Rank("G-20") > 0)
                await PowerCmd.Apply<StrengthPower>(context, player.Creature, -1, null, null);
            if (Rank("G-21") > 0)
                await PowerCmd.Apply<DexterityPower>(context, player.Creature, -1, null, null);
            if (Rank("G-25") > 0)
                player.Creature.LoseHpInternal(Math.Min(Rank("G-25"), Math.Max(0, player.Creature.CurrentHp - 1)), ValueProp.Unblockable | ValueProp.Unpowered);
            if (ShopSchemaVersion == 0 && RoleCp >= 10) await PlayNamedReward(player, choiceContext, CharacterRole switch
            {
                "ironclad" => "燃烧", "regent" => "环绕轨道", "necrobinder" => "友谊",
                "silent" => "灵动步伐", "defect" => "碎片整理", _ => ""
            });
            if (ShopSchemaVersion == 0 && RoleCp >= 35) await ChooseAndPlayRewards(player, choiceContext);
        }
        if (CharacterRole == "ironclad" && player.Creature.GetPowerAmount<StrengthPower>() >= 1
            && ((turn % 2 == 1 && Rank("IC-02") > 0) || (turn % 2 == 0 && Rank("IC-06") > 0)))
            await PowerCmd.Apply<StrengthPower>(context, player.Creature, -1, null, null);
        if (CharacterRole == "defect" && turn >= 6 && Rank("DF-05") > 0)
            await PowerCmd.Apply<FocusPower>(context, player.Creature, -1, null, null);
        if (CharacterRole == "necrobinder" && Rank("NB-01") > 0 && turn >= 4 && player.Osty is { IsAlive: true } osty)
            await PowerCmd.Remove<DieForYouPower>(osty);
        if (CharacterRole == "necrobinder" && Rank("NB-06") > 0)
            foreach (Creature enemy in combatState.Enemies)
                if (enemy.GetPower<DoomPower>() is { } doom)
                    await PowerCmd.ModifyAmount(context, doom, -5, null, null);
    }

    public override Task AfterEnergyReset(Player player)
    {
        if (CharacterRole == "necrobinder" && Rank("NB-07") > 0 && VoidExhaustedThisTurn)
        {
            player.PlayerCombatState?.LoseEnergy(1);
            VoidExhaustedThisTurn = false;
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy || CharacterRole != "silent" || Rank("SL-06") <= 0) return;
        foreach (Creature enemy in combatState.Enemies)
            if (enemy.GetPower<PoisonPower>() is { } poison)
                await PowerCmd.ModifyAmount(context, poison, -1, null, null);
    }

    private static readonly Dictionary<string, string[]> BaseRewardPools = new()
    {
        ["ironclad"] = new[] { "黑暗之拥", "无惧疼痛", "撕裂", "祭品", "狱火" },
        ["regent"] = new[] { "创世之柱", "招架", "群星之子", "光谱偏移", "大爆炸" },
        ["necrobinder"] = new[] { "致死性", "死亡之舞", "钙质化", "精神过载" },
        ["silent"] = new[] { "余像", "刀扇", "青雾", "肾上腺素", "精准" },
        ["defect"] = new[] { "子程序", "雷霆", "散热片", "肾上腺素", "变废为宝" }
    };
    private static readonly Dictionary<string, string[]> ExtraRewardPools = new()
    {
        ["ironclad"] = new[] { "壁垒", "绯红披风", "坚定不移" },
        ["regent"] = new[] { "武器库", "剑圣", "追踪刃", "创世纪" },
        ["necrobinder"] = new[] { "死神形态", "血肉戏法" },
        ["silent"] = new[] { "触媒", "群蛇形态", "计划妥当" },
        ["defect"] = new[] { "创造性 AI", "回响形态" }
    };

    private static async Task PlayNamedReward(Player player, MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, string title)
    {
        CardModel? canonical = FindCard(title);
        if (canonical is null) { MainFile.Logger.Warn($"[ChallengePoints] reward card unavailable: {title}"); return; }
        if (player.Creature.CombatState is not { } combat) return;
        CardModel card = combat.CreateCard(canonical, player);
        if (title != "灵动步伐") Upgrade(card);
        await CardCmd.AutoPlay(context, card, null);
    }

    private async Task ChooseAndPlayRewards(Player player, MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context)
    {
        if (!BaseRewardPools.TryGetValue(CharacterRole, out string[]? names)) return;
        if (player.Creature.CombatState is not { } combat) return;
        var cards = new List<CardModel>();
        var firstPool = new List<CardModel>();
        foreach (string title in names.Concat(RoleCp >= 42 ? ExtraRewardPools[CharacterRole] : Array.Empty<string>()))
        {
            CardModel? canonical = FindCard(title);
            if (canonical is null) { MainFile.Logger.Warn($"[ChallengePoints] reward candidate unavailable: {title}"); continue; }
            CardModel card = combat.CreateCard(canonical, player);
            Upgrade(card);
            cards.Add(card);
            if (names.Contains(title)) firstPool.Add(card);
        }
        try
        {
            int picks = RoleCp >= 42 ? 2 : 1;
            for (int i = 0; i < picks; i++)
            {
                List<CardModel> choices = i == 0 ? firstPool : cards;
                if (choices.Count == 0) break;
                var prefs = new CardSelectorPrefs(new LocString("card_selection", "CHALLENGE_SELECT"), 1);
                CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(context, choices, player, prefs)).FirstOrDefault();
                if (chosen is null) break;
                cards.Remove(chosen);
                await CardCmd.AutoPlay(context, chosen, null);
            }
        }
        finally
        {
            foreach (CardModel unused in cards)
                combat.RemoveCard(unused);
        }
    }

    public override async Task AfterCardPlayed(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, CardPlay play)
    {
        Player owner = play.Card.Owner;
        if (owner.PlayerCombatState is null) return;
        CardModel card = play.Card;
        if (ShopSchemaVersion > 0 && card is Soul && _shopWatchingSoulDraw)
        {
            _shopWatchingSoulDraw = false;
            CardModel? drawn = _shopSoulDrawnCard;
            _shopSoulDrawnCard = null;
            ShopSoulAutoplayUsedThisTurn = true;
            if (drawn?.Pile?.Type == PileType.Hand && !drawn.Keywords.Contains(CardKeyword.Unplayable))
                await CardCmd.AutoPlay(context, drawn, null);
        }
        if (ShopSchemaVersion > 0 && SquadRank("SQ-02") > 0 && card.Type == CardType.Power && ShopPowersPlayedThisTurn++ == 0)
            owner.Creature.LoseHpInternal(4, ValueProp.Unblockable | ValueProp.Unpowered);
        if (card.Pool is ColorlessCardPool && CharacterRole == "regent" && Rank("RG-08") > 0)
        {
            if (++ColorlessPlayedThisTurn == 2) owner.PlayerCombatState.LoseEnergy(1);
        }
        if (CharacterRole == "defect")
        {
            if (card.EnergyCost.GetWithModifiers(CostModifiers.All) == 0 && Rank("DF-07") > 0 && ++ZeroCostPlayedThisTurn == 2)
                owner.PlayerCombatState.LoseEnergy(1);
            if (card.Type == CardType.Power)
            {
                PowerPlayedThisTurn++;
                if (Rank("DF-03") > 0)
                    owner.Creature.LoseHpInternal(4, ValueProp.Unblockable | ValueProp.Unpowered);
            }
        }
        if (CharacterRole == "necrobinder" && Rank("NB-08") > 0 && card.Id.Entry.Contains("VOID", StringComparison.OrdinalIgnoreCase)
            && ++VoidPlayedThisTurn == 2)
            owner.PlayerCombatState.LoseEnergy(1);
        if (CharacterRole == "regent" && Rank("RG-03") > 0 && card is SovereignBlade)
            card.EnergyCost.AddThisCombat(1);
        if (CharacterRole == "silent")
        {
            CardsPlayedThisTurn++;
            if (Rank("SL-04") > 0 && card.Keywords.Contains(CardKeyword.Sly) && ++SlyPlayedThisTurn % 2 == 0)
                owner.PlayerCombatState.LoseEnergy(1);
            if (Rank("SL-05") > 0 && CardsPlayedThisTurn > 12)
                owner.PlayerCombatState.LoseEnergy(1);
        }
        await Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType type) =>
        !(CharacterRole == "defect" && Rank("DF-08") > 0 && card.Type == CardType.Power && PowerPlayedThisTurn >= 3);

    public override async Task AfterSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        foreach (Player player in base.RunState.Players)
        {
            if (CharacterRole == "regent" && Rank("RG-01") > 0)
            {
                if (player.PlayerCombatState is { } combat)
                {
                    if (Rank("RG-02") > 0)
                    {
                        int oldDebt = Math.Max(0, -combat.Stars) / 2;
                        combat.Stars -= 1;
                        int newDebt = Math.Max(0, -combat.Stars) / 2;
                        if (newDebt > oldDebt)
                        {
                            await PowerCmd.Apply<StrengthPower>(choiceContext, player.Creature, oldDebt - newDebt, null, null);
                            await PowerCmd.Apply<DexterityPower>(choiceContext, player.Creature, oldDebt - newDebt, null, null);
                        }
                    }
                    else combat.LoseStars(1);
                }
            }
            if (CharacterRole == "regent" && Rank("RG-06") > 0 && player.Creature.GetPower<VigorPower>() is { } vigor)
                await PowerCmd.ModifyAmount(choiceContext, vigor, -2, null, null);
            if (CharacterRole == "necrobinder" && Rank("NB-05") > 0 && player.Osty is { IsAlive: true } osty)
                osty.LoseHpInternal(2, ValueProp.Unblockable | ValueProp.Unpowered);
            if (ShopSchemaVersion > 0 && HasShopItem("IT-04"))
                await CreatureCmd.GainBlock(player.Creature, 1m, ValueProp.Unpowered, null);
        }
        ColorlessPlayedThisTurn = ZeroCostPlayedThisTurn = PowerPlayedThisTurn = 0;
        ExhaustedThisTurn = GeneratedThisTurn = SoulGeneratedThisTurn = VoidPlayedThisTurn = 0;
        ColorlessGeneratedThisTurn = SoulPlayedThisTurn = StatusReroutedThisTurn = 0;
        SlyPlayedThisTurn = CardsPlayedThisTurn = NonHandDrawsThisTurn = 0;
        ShopStatusesThisTurn = ShopPowersPlayedThisTurn = 0;
        ShopSoulAutoplayUsedThisTurn = false;
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (ShopSchemaVersion <= 0 || SquadRank("SQ-07") < 4 ||
            creature.Side != CombatSide.Enemy || wasRemovalPrevented) return Task.CompletedTask;
        Player? player = base.RunState.Players.FirstOrDefault();
        if (player is null) return Task.CompletedTask;
        var seen = new HashSet<CardModel>();
        foreach (CardModel card in player.Deck.Cards.Concat(player.PlayerCombatState?.AllCards ?? Enumerable.Empty<CardModel>()))
        {
            if (!seen.Add(card) || card is not IronWave) continue;
            // SQ-07 raises Iron Wave's MaxUpgradeLevel through Harmony so the
            // native setter and save reconstruction accept every new level.
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterEnergySpent(CardModel card, int amount)
    {
        if (ShopSchemaVersion <= 0 || amount <= 0) return;
        if (SquadRank("SQ-01") > 0)
        {
            int before = ShopEnergySpent / 3;
            ShopEnergySpent += amount;
            int gained = ShopEnergySpent / 3 - before;
            if (gained > 0)
                await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), card.Owner.Creature, gained, null, card);
        }
        if (SquadRank("SQ-10") > 0)
            await ForgeCmd.Forge(amount * 3m, card.Owner, this);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (ShopSchemaVersion <= 0 || SquadRank("SQ-02") < 3) return;
        foreach (Player player in base.RunState.Players)
        {
            int lost = Math.Max(0, ShopCombatStartHp - player.Creature.CurrentHp);
            int percent = SquadRank("SQ-02") >= 4 ? 60 : 50;
            if (lost > 0) await CreatureCmd.Heal(player.Creature, Math.Floor(lost * percent / 100m));
        }
    }

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (!isMimicked)
        {
            LastRestOption = nameof(HealRestSiteOption);
            LastRestFloor = base.RunState.TotalFloor;
        }
        if (!isMimicked && Rank("G-08") > 0)
            player.Creature.SetMaxHpInternal(Math.Max(1, player.Creature.MaxHp - 3));
        return Task.CompletedTask;
    }
}
