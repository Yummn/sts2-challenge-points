#if !STS2_V110
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ChallengePoints;

// PC v107.1 predates the official Ambergris. This is a compatibility model
// with the v110 behaviour: heal 50% max HP, and in combat gain one extra turn.
public sealed class ChallengeAmbergris : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Event;
    public override PotionUsage Usage => PotionUsage.AnyTime;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        if (target is null) return;
        await CreatureCmd.Heal(target, target.MaxHp * 0.5m);
        if (CombatManager.Instance.IsInProgress)
            await PowerCmd.Apply<ChallengeAmbergrisPower>(choiceContext, target, 1, Owner.Creature, null);
    }
}

public sealed class ChallengeAmbergrisPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    public override bool ShouldTakeExtraTurn(Player player) => Amount > 0 && player == Owner.Player;

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (player == Owner.Player) await PowerCmd.Decrement(this);
    }
}
#endif
