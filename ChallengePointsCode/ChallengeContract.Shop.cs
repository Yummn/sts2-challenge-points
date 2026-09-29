using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChallengePoints;

public sealed partial class ChallengeContract
{
    internal static int ShopChoiceOpenedCount;
    private bool _shopApplyingPoison;
    private bool _shopAddingAcquisitionReward;
    private bool _shopSuppressAcquisition;
    private bool _shopProcessingChoices;
    private bool _shopWatchingSoulDraw;
    private CardModel? _shopSoulDrawnCard;

    private void OnShopDeckCardAdded(Player player, CardModel card)
    {
        if (_shopAddingAcquisitionReward || _shopSuppressAcquisition || base.RunState.CurrentRoom is null) return;
        if (++ShopCardsAcquired % 15 != 0) return;
        string name = SquadRank("SQ-09") >= 2 ? "灵体印刷机" : "生成灵体";
        if (FindCard(name) is not { } canonical) return;
        _shopAddingAcquisitionReward = true;
        try
        {
            CardModel added = base.RunState.CreateCard(canonical, player);
            if (SquadRank("SQ-09") >= 3) Upgrade(added);
            player.Deck.AddInternal(added);
        }
        finally { _shopAddingAcquisitionReward = false; }
    }

    private void GrantPurchasedStartingCards(RunState run, Player player)
    {
        if (SquadRank("SQ-01") > 0)
            for (int i = 0; i < 3; i++) AddCard("愤怒");
        if (SquadRank("SQ-04") > 0)
            for (int i = 0; i < 3; i++) AddCard("毒雾");
        if (SquadRank("SQ-09") > 0)
            AddCard(SquadRank("SQ-09") >= 2 ? "灵体印刷机" : "生成灵体", SquadRank("SQ-09") >= 3);
        if (SquadRank("SQ-10") >= 2)
        {
            AddCard("淬炼刀刃");
            AddCard("铸墙");
        }
        if (SquadRank("SQ-10") >= 3)
        {
            AddCard("征召上前");
            AddCard("武装");
        }
        if (SquadRank("SQ-07") > 0 && FindCard("铁斩波") is { } ironWave)
        {
            foreach (CardModel old in player.Deck.Cards.Where(c => c.Id.Entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase)
                || SquadRank("SQ-07") >= 2 && c.Id.Entry.Contains("DEFEND", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                int index = player.Deck.Cards.ToList().IndexOf(old);
                bool upgraded = old.IsUpgraded;
                player.Deck.RemoveInternal(old);
                run.RemoveCard(old);
                CardModel replacement = run.CreateCard(ironWave, player);
                if (upgraded) Upgrade(replacement);
                player.Deck.AddInternal(replacement, index);
            }
        }
        if (SquadRank("SQ-07") >= 3) AddCard("切肉刀");

        void AddCard(string name, bool upgraded = false)
        {
            CardModel? canonical = FindCard(name);
            if (canonical is null)
            {
                MainFile.Logger.Warn($"[ChallengePoints] purchased squad card not found: {name}");
                return;
            }
            CardModel card = run.CreateCard(canonical, player);
            if (upgraded) Upgrade(card);
            player.Deck.AddInternal(card);
        }
    }

    private async Task ApplyShopTurnStart(Player player, PlayerChoiceContext context)
    {
        if (SquadRank("SQ-01") is > 0 and < 4)
            player.Creature.LoseHpInternal(1, ValueProp.Unblockable | ValueProp.Unpowered);
        if (SquadRank("SQ-02") > 0)
            player.Creature.LoseHpInternal(2, ValueProp.Unblockable | ValueProp.Unpowered);

        if (SquadRank("SQ-03") > 0)
        {
            string[] choices = SquadRank("SQ-03") >= 4
                ? new[] { "灵魂", "鬼火" }
                : new[] { base.RunState.Rng.Niche.NextItem(new[] { "灵魂", "鬼火" }) ?? "灵魂" };
            foreach (string name in choices)
                if (FindCard(name) is { } canonical)
                {
                    CardModel card = canonical.ToMutable();
                    if (name == "灵魂" && SquadRank("SQ-03") >= 2) Upgrade(card);
                    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player, CardPilePosition.Bottom);
                }
        }

        if (SquadRank("SQ-02") >= 2)
            await CardPileCmd.AddGeneratedCardToCombat(ModelDb.Card<ChallengeLightVoucher>().ToMutable(), PileType.Hand, player);

        if (SquadRank("SQ-06") > 0)
        {
            CardModel[] pool = ModelDb.AllCards.Where(c => c.Pool == player.Character.CardPool &&
                c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).ToArray();
            if (base.RunState.Rng.Niche.NextItem(pool) is { } canonical)
            {
                CardModel card = canonical.ToMutable();
                if (SquadRank("SQ-06") >= 4) Upgrade(card);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            }
        }
    }

    private async Task HandleShopStatusGenerated(CardModel card, Player creator)
    {
        int rank = SquadRank("SQ-05");
        if (rank == 0) return;
        int nth = ++ShopStatusesThisTurn;
        var context = new ThrowingPlayerChoiceContext();
        if (nth == 1 || nth == 3 && rank >= 2)
        {
            int orb = base.RunState.Rng.Niche.NextInt(0, 3);
            if (orb == 0) await OrbCmd.Channel<LightningOrb>(context, creator);
            else if (orb == 1) await OrbCmd.Channel<FrostOrb>(context, creator);
            else await OrbCmd.Channel<DarkOrb>(context, creator);
        }
        if (nth == 1 && rank >= 3 && FindCard("燃料") is { } fuel)
            await CardPileCmd.AddGeneratedCardToCombat(fuel.ToMutable(), PileType.Hand, creator);
        if (rank >= 4)
            await PowerCmd.Apply<TemporaryFocusPower>(context, creator.Creature, 1, null, null);
    }

    private async Task HandleShopDebuffApplied(PlayerChoiceContext context, PowerModel power, decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier)
    {
        if (_shopApplyingPoison || SquadRank("SQ-04") <= 0 || amount <= 0 ||
            power.Owner.Side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || power.Type != PowerType.Debuff ||
            applier?.IsPlayer != true)
            return;
        _shopApplyingPoison = true;
        try
        {
            Player? owner = base.RunState.Players.FirstOrDefault();
            if (owner is null) return;
            int damage = SquadRank("SQ-04") >= 2 ? 6 : 4;
            int poison = SquadRank("SQ-04") >= 3 ? 3 : 2;
            await CreatureCmd.Damage(context, power.Owner, damage, ValueProp.Unpowered, owner.Creature);
            await PowerCmd.Apply<PoisonPower>(context, power.Owner, poison, owner.Creature, null);
        }
        finally { _shopApplyingPoison = false; }
    }

    internal async Task ProcessShopChoicesAfterFadeIn()
    {
        if (ShopSchemaVersion <= 0 || !StartupRewardsGranted || _shopProcessingChoices ||
            base.RunState.CurrentRoom is null || base.RunState.CurrentRoom is CombatRoom)
            return;
        if (base.RunState.Players.FirstOrDefault() is not { } player) return;
        _shopProcessingChoices = true;
        try
        {
            if (base.RunState.CurrentActIndex == 0)
            {
                while (ShopStartupChoiceStage < 4)
                {
                    switch (ShopStartupChoiceStage)
                    {
                        case 0 when HasShopItem("IT-03"):
                            await ChooseAndAddShopCard(player, ModelDb.AllCards.Where(c => c.Pool == player.Character.CardPool &&
                                c.Type == CardType.Power && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare),
                                "CHALLENGE_SHOP_POWER", 3);
                            break;
                        case 1 when SquadRank("SQ-06") >= 2:
                            await ChooseAndAddShopCard(player, ShopNamedCards("创世之柱", "君权自授"), "CHALLENGE_SHOP_START", 2);
                            break;
                        case 2 when HasShopItem("IT-06"):
                            await TransformShopCards(player);
                            break;
                        case 3 when HasShopItem("IT-07"):
                            await RemoveAndUpgradeShopCards(player);
                            break;
                    }
                    ShopStartupChoiceStage++;
                    await PersistShopChoice();
                }
            }

            int act = base.RunState.CurrentActIndex;
            if (act is 1 or 2)
            {
                if (ShopActChoiceAct != act)
                {
                    ShopActChoiceAct = act;
                    ShopActChoiceStage = 0;
                }
                while (ShopActChoiceStage < 2)
                {
                    switch (ShopActChoiceStage)
                    {
                        case 0 when SquadRank("SQ-06") >= 3:
                            await ChooseAndAddShopCard(player, ShopNamedCards("创世之柱", "武器库", "光谱偏移"),
                                "CHALLENGE_SHOP_ACT", 3);
                            break;
                        case 1 when SquadRank("SQ-10") >= 4:
                            await ChooseAndAddShopCard(player, ShopNamedCards("追踪之刃", "剑圣"),
                                "CHALLENGE_SHOP_ACT", 2);
                            break;
                    }
                    ShopActChoiceStage++;
                    await PersistShopChoice();
                }
            }
        }
        finally { _shopProcessingChoices = false; }
    }

    private IEnumerable<CardModel> ShopNamedCards(params string[] names)
    {
        foreach (string name in names)
        {
            CardModel? card = FindCard(name);
            if (card is null) MainFile.Logger.Warn($"[ChallengePoints] squad choice unavailable: {name}");
            else yield return card;
        }
    }

    private async Task ChooseAndAddShopCard(Player player, IEnumerable<CardModel> source, string prompt, int count)
    {
        List<CardModel> pool = source.Where(c => c is not null).GroupBy(c => c.Id.Entry)
            .Select(group => group.First()).ToList();
        if (pool.Count == 0) throw new InvalidOperationException($"No candidates for {prompt}");
        var offered = new List<CardModel>();
        while (pool.Count > 0 && offered.Count < count)
        {
            CardModel? chosen = base.RunState.Rng.Niche.NextItem(pool);
            if (chosen is null) break;
            pool.Remove(chosen);
            offered.Add(base.RunState.CreateCard(chosen, player));
        }
        if (offered.Count == 0) throw new InvalidOperationException($"No offered cards for {prompt}");
        ShopChoiceOpenedCount++;
        MainFile.Logger.Info($"[ChallengePoints] opening {prompt} choice with {offered.Count} cards.");
        CardModel? selected = null;
        try
        {
            selected = (await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), offered, player,
                new CardSelectorPrefs(new LocString("card_selection", prompt), 1))).FirstOrDefault();
            if (selected is null) throw new InvalidOperationException($"Card choice {prompt} returned none");
            await CardPileCmd.Add(selected, PileType.Deck);
        }
        finally
        {
            foreach (CardModel candidate in offered)
                if (candidate != selected) base.RunState.RemoveCard(candidate);
        }
    }

    private async Task TransformShopCards(Player player)
    {
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "TO_TRANSFORM"), 0, 2)
        { Cancelable = true };
        CardModel[] selected = (await CardSelectCmd.FromDeckGeneric(player, prefs, c => c.IsTransformable && c.Type != CardType.Quest)).ToArray();
        _shopSuppressAcquisition = true;
        try
        {
            foreach (CardModel old in selected)
            {
                CardRarity targetRarity = old.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                    ? old.Rarity : CardRarity.Common;
                List<CardModel> pool = ModelDb.AllCards.Where(c => c.Pool == player.Character.CardPool &&
                    c.Rarity == targetRarity && c.Id != old.Id).ToList();
                if (pool.Count == 0) throw new InvalidOperationException($"No transform candidate for {old.Id.Entry}");
                CardModel replacement = base.RunState.CreateCard(base.RunState.Rng.Niche.NextItem(pool)!, player);
                await CardCmd.Transform(old, replacement);
            }
        }
        finally { _shopSuppressAcquisition = false; }
    }

    private async Task RemoveAndUpgradeShopCards(Player player)
    {
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "CHALLENGE_SHOP_REMOVE"), 0, 2)
        { Cancelable = true };
        CardModel[] selected = (await CardSelectCmd.FromDeckForRemoval(player, prefs)).ToArray();
        if (selected.Length > 0) await CardPileCmd.RemoveFromDeck(selected);
        if (!player.Deck.Cards.Any(c => c.IsUpgradable)) return;
        var upgradePrefs = new CardSelectorPrefs(new LocString("card_selection", "CHALLENGE_SHOP_UPGRADE"), 1);
        CardModel? upgrade = (await CardSelectCmd.FromDeckForUpgrade(player, upgradePrefs)).FirstOrDefault();
        if (upgrade is not null) CardCmd.Upgrade(upgrade);
    }

    private async Task PersistShopChoice()
    {
        if (!RunManager.Instance.ShouldSave || base.RunState.CurrentRoom is CombatRoom ||
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), base.RunState)) return;
        await SaveManager.Instance.SaveRun(null, false);
    }
}
