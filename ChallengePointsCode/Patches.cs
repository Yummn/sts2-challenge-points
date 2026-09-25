using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
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
