using HarmonyLib;
using Godot;
using System.Reflection;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;

namespace ChallengePoints;

[HarmonyPatch(typeof(NCharacterSelectScreen), "_Ready")]
internal static class CharacterSelectPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        try
        {
            if (__instance.GetNodeOrNull<ChallengeUi>("ChallengePointsUi") is not null) return;
            __instance.AddChild(new ChallengeUi(__instance));
        }
        catch (Exception ex) { MainFile.Logger.Warn($"[ChallengePoints] UI install failed: {ex}"); }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
internal static class NewRunPatch
{
    private static void Prefix(IReadOnlyList<Player> players, ref IReadOnlyList<ModifierModel> modifiers)
    {
        try
        {
            if (players.Count != 1 || modifiers.Any(x => x is ChallengeContract)) return;
            string role = ChallengeCatalog.NormalizeRole(players[0].Character.Id.Entry);
            if (ChallengeSelection.Score("common") == 0 && ChallengeSelection.Score(role) == 0) return;
            var contract = ModelDb.Modifier<ChallengeContract>().ToMutable() as ChallengeContract;
            if (contract is null) return;
            contract.CharacterRole = role;
            contract.ContractData = ChallengeSelection.Snapshot(role);
            modifiers = modifiers.Append(contract).ToArray();
        }
        catch (Exception ex) { MainFile.Logger.Error($"[ChallengePoints] contract creation failed: {ex}"); }
    }
}

// Neow uses the presence of any ModifierModel as a signal to show only
// modifier-provided Neow choices. ChallengeContract is a gameplay modifier,
// but intentionally has no Neow choice, so hide it only while vanilla Neow
// options are being generated.
[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class ChallengeNeowCompatibilityPatch
{
    private static readonly MethodInfo OriginalMethod =
        AccessTools.Method(typeof(Neow), "GenerateInitialOptions")
        ?? throw new MissingMethodException(typeof(Neow).FullName, "GenerateInitialOptions");

    private static readonly MethodInfo SetModifiersMethod =
        AccessTools.PropertySetter(typeof(RunState), nameof(RunState.Modifiers))
        ?? throw new MissingMethodException(typeof(RunState).FullName, "set_Modifiers");

    [ThreadStatic]
    private static bool _reentrant;

    private static bool Prefix(Neow __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (_reentrant || __instance.Owner is not { } owner)
            return true;

        IReadOnlyList<ModifierModel> modifiers = owner.RunState.Modifiers;
        if (!modifiers.Any(modifier => modifier is ChallengeContract) ||
            modifiers.Any(modifier => modifier is not ChallengeContract))
            return true;

        try
        {
            _reentrant = true;
            SetModifiersMethod.Invoke(owner.RunState, new object[] { Array.Empty<ModifierModel>() });
            __result = (IReadOnlyList<EventOption>?)OriginalMethod.Invoke(__instance, null)
                ?? Array.Empty<EventOption>();
            MainFile.Logger.Info("[ChallengePoints] restored vanilla Neow blessing options for ChallengeContract.");
            return false;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"[ChallengePoints] Neow compatibility patch failed: {exception}");
            return true;
        }
        finally
        {
            SetModifiersMethod.Invoke(owner.RunState, new object[] { modifiers });
            _reentrant = false;
        }
    }
}

[HarmonyPatch(typeof(PowerModel), "get_Icon")]
internal static class MalleableSmallIconPatch
{
    private static bool Prefix(PowerModel __instance, ref Texture2D __result)
    {
        if (__instance is not ChallengeMalleablePower || ChallengeArt.MalleableIcon is not { } icon) return true;
        __result = icon;
        return false;
    }
}

[HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
internal static class MalleableBigIconPatch
{
    private static bool Prefix(PowerModel __instance, ref Texture2D __result)
    {
        if (__instance is not ChallengeMalleablePower || ChallengeArt.MalleableIcon is not { } icon) return true;
        __result = icon;
        return false;
    }
}

#if !STS2_V110
[HarmonyPatch(typeof(PotionModel), "get_Image")]
internal static class AmbergrisPotionImagePatch
{
    private static bool Prefix(PotionModel __instance, ref Texture2D __result)
    {
        if (__instance is not ChallengeAmbergris || ChallengeArt.AmbergrisIcon is not { } icon) return true;
        __result = icon;
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "get_Pool")]
internal static class AmbergrisPotionPoolPatch
{
    private static bool Prefix(PotionModel __instance, ref PotionPoolModel __result)
    {
        if (__instance is not ChallengeAmbergris) return true;
        PotionPoolModel? pool = ModelDb.AllPotionPools.FirstOrDefault(x => x.GetType().Name == "EventPotionPool");
        if (pool is null) return true;
        __result = pool;
        return false;
    }
}
#endif
