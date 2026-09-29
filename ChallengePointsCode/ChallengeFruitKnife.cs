using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace ChallengePoints;

// SQ-07 III grants a relic, not a card. Reuse the game's knife artwork but
// keep a distinct model and a distinct campfire option from Meat Cleaver:
// the native Cook option removes up to two cards and grants a different HP amount.
public sealed class ChallengeFruitKnifeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    protected override string IconBaseName => "meat_cleaver";

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner) return false;
        options.Add(new ChallengeFruitKnifeRestSiteOption(player));
        return true;
    }
}

public sealed class ChallengeFruitKnifeRestSiteOption : RestSiteOption
{
    public override string OptionId => "CHALLENGE_FRUIT_KNIFE";

    // RestSiteOption.Icon is not virtual. The matching Harmony patch redirects
    // only this option to the native Cook icon; preloading uses the same path.
    internal static string CookIconPath => ImageHelper.GetImagePath("ui/rest_site/option_cook.png");
    public override IEnumerable<string> AssetPaths => new[] { CookIconPath };
    public override bool IsEnabled => PileType.Deck.GetPile(Owner).Cards.Any(c => c.IsRemovable);

    public ChallengeFruitKnifeRestSiteOption(Player owner) : base(owner) { }

    public override async Task<bool> OnSelect()
    {
        if (!IsEnabled) return false;
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        CardModel? selected = (await CardSelectCmd.FromDeckForRemoval(Owner, prefs)).FirstOrDefault();
        if (selected is null) return false;
        await ApplySelectedCard(selected);
        return true;
    }

    internal async Task ApplySelectedCard(CardModel selected)
    {
        if (selected.Owner != Owner || !selected.IsRemovable || !Owner.Deck.Cards.Contains(selected))
            throw new InvalidOperationException("Fruit Knife selected a card outside the owner's deck");
        await CardPileCmd.RemoveFromDeck(selected);
        await CreatureCmd.GainMaxHp(Owner.Creature, 6m);
    }
}
