using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChallengePoints;

// These cards are granted only by purchased squads. They are deliberately not
// added to ordinary rewards, so their pool and art are set explicitly instead
// of detouring the global card-pool generator on Android.
internal static class ChallengeShopCards
{
    private static readonly FieldInfo? PoolField = AccessTools.Field(typeof(CardModel), "_pool");
    private static bool _poolsAssigned;

    // ModelDb.Init discovers mod CardModel subclasses automatically. Injecting
    // them in ModInitializer creates duplicate canonical models at startup.

    internal static void EnsurePools()
    {
        if (_poolsAssigned) return;
        SetPool(ModelDb.Card<ChallengeLightVoucher>(), ModelDb.CardPool<IroncladCardPool>());
        SetPool(ModelDb.Card<ChallengeMeatCleaver>(), ModelDb.CardPool<IroncladCardPool>());
        SetPool(ModelDb.Card<ChallengeSpiritMaker>(), ModelDb.CardPool<NecrobinderCardPool>());
        SetPool(ModelDb.Card<ChallengeSpiritPrinter>(), ModelDb.CardPool<NecrobinderCardPool>());
        _poolsAssigned = true;
    }

    private static void SetPool(CardModel card, CardPoolModel pool)
    {
        if (PoolField is null) throw new MissingFieldException(typeof(CardModel).FullName, "_pool");
        PoolField.SetValue(card, pool);
    }
}

public sealed class ChallengeLightVoucher : CardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override string PortraitPath => ModelDb.Card<Fuel>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<Fuel>().BetaPortraitPath;
    public ChallengeLightVoucher() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => PlayerCmd.GainEnergy(1, Owner);
}

public sealed class ChallengeSpiritMaker : CardModel
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain, CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(15, ValueProp.Move) };
    public override string PortraitPath => ModelDb.Card<CaptureSpirit>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<CaptureSpirit>().BetaPortraitPath;
    public ChallengeSpiritMaker() : base(1, CardType.Skill, CardRarity.Event, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner.Creature, 2, Owner.Creature, this);
        CardModel spirit = (Owner.Creature.CombatState ?? throw new InvalidOperationException("No combat for generated Apparition"))
            .CreateCard(ModelDb.Card<Apparition>(), Owner);
        await CardPileCmd.AddGeneratedCardToCombat(spirit, PileType.Draw, Owner, CardPilePosition.Bottom);
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5);
        RemoveKeyword(CardKeyword.Exhaust);
    }
}

public sealed class ChallengeSpiritPrinter : CardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain, CardKeyword.Exhaust };
    public override string PortraitPath => ModelDb.Card<Apparition>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<Apparition>().BetaPortraitPath;
    public ChallengeSpiritPrinter() : base(1, CardType.Skill, CardRarity.Event, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<IntangiblePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner.Creature, 2, Owner.Creature, this);
        CardModel spirit = (Owner.Creature.CombatState ?? throw new InvalidOperationException("No combat for generated Apparition"))
            .CreateCard(ModelDb.Card<Apparition>(), Owner);
        await CardPileCmd.AddGeneratedCardToCombat(spirit, PileType.Draw, Owner, CardPilePosition.Bottom);
    }
    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}

// The handwritten name has no defined effect. A modest attack/block hybrid is
// chosen so the level-III reward reinforces SQ-07 without duplicating Iron Wave.
public sealed class ChallengeMeatCleaver : CardModel
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8, ValueProp.Move), new BlockVar(8, ValueProp.Move)
    };
    public override string PortraitPath => ModelDb.Card<IronWave>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<IronWave>().BetaPortraitPath;
    public ChallengeMeatCleaver() : base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is null) return;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
#if STS2_V110
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
#else
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
            .Targeting(cardPlay.Target).Execute(choiceContext);
#endif
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}
