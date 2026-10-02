using HarmonyLib;
using Godot;
using System.Reflection;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.RestSite;

namespace ChallengePoints;

[HarmonyPatch(typeof(RestSiteOption), "get_Icon")]
internal static class ChallengeFruitKnifeRestIconPatch
{
    private static bool Prefix(RestSiteOption __instance, ref Texture2D __result)
    {
        if (__instance is not ChallengeFruitKnifeRestSiteOption) return true;
        __result = PreloadManager.Cache.GetTexture2D(ChallengeFruitKnifeRestSiteOption.CookIconPath);
        return false;
    }
}

[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.OnSelect))]
internal static class ChallengeRestSiteSelectionPatch
{
    private static void Postfix(RestSiteOption __instance, Task<bool> __result)
    {
        _ = RecordAfterSelection(__instance, __result);
    }

    private static async Task RecordAfterSelection(RestSiteOption option, Task<bool> result)
    {
        try
        {
            if (!await result || AccessTools.Property(typeof(RestSiteOption), "Owner")?.GetValue(option) is not Player owner)
                return;
            owner.RunState.Modifiers.OfType<ChallengeContract>().FirstOrDefault()?.RecordRestSiteOption(option);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[ChallengePoints] rest-site selection record failed: {ex.Message}");
        }
    }
}

// Iron Wave's native setter rejects upgrades above its normal maximum of one.
// The squad has to raise that maximum both in combat and while old upgraded
// cards are reconstructed from a save (before they have an Owner).
[HarmonyPatch(typeof(CardModel), "get_MaxUpgradeLevel")]
internal static class ChallengeIronWaveUpgradeLimitPatch
{
    [ThreadStatic] internal static bool LoadingUnlimitedIronWave;

    private static bool Prefix(CardModel __instance, ref int __result)
    {
        if (__instance is not IronWave) return true;
        if (LoadingUnlimitedIronWave || __instance.IsMutable &&
            __instance.Owner?.RunState?.Modifiers.OfType<ChallengeContract>().Any(c =>
                c.ShopSchemaVersion > 0 && c.SquadRank("SQ-07") >= 4) == true)
        {
            __result = int.MaxValue;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable))]
internal static class ChallengeIronWaveLoadPatch
{
    private static void Prefix(SerializableCard save, out bool __state)
    {
        __state = ChallengeIronWaveUpgradeLimitPatch.LoadingUnlimitedIronWave;
        if (save.Id?.Entry == "IRON_WAVE" && save.CurrentUpgradeLevel > 1)
            ChallengeIronWaveUpgradeLimitPatch.LoadingUnlimitedIronWave = true;
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        ChallengeIronWaveUpgradeLimitPatch.LoadingUnlimitedIronWave = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Decrement))]
internal static class ChallengePoisonDoesNotDecayPatch
{
    private static bool Prefix(PowerModel power, ref Task __result)
    {
        if (power is PoisonPower && power.Owner.Side == CombatSide.Enemy &&
            power.Owner.CombatState?.RunState.Modifiers.OfType<ChallengeContract>().Any(c =>
                c.ShopSchemaVersion > 0 && c.SquadRank("SQ-04") >= 4) == true)
        {
            __result = Task.CompletedTask;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(AscensionHelper), nameof(AscensionHelper.GetHoverTip))]
internal static class ChallengeAscensionPortraitTextPatch
{
    private static void Prefix(int level)
    {
        try
        {
            LocManager manager = LocManager.Instance;
            LocTable table = manager.GetTable("ascension");
            bool chinese = manager.Language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true;
            Dictionary<string, string> missing = ChallengeAscensionTextFallback.MissingTitles(level, table.HasEntry, chinese);
            if (missing.Count == 0) return;
            table.MergeWith(missing);
            MainFile.Logger.Warn($"[ChallengePoints] repaired missing ascension portrait titles: {string.Join(", ", missing.Keys)}; difficulty unchanged.");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[ChallengePoints] ascension portrait text repair failed: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
internal static class ChallengeNewRunTracePatch
{
    private static void Prefix(CharacterModel __0, int __6)
    {
        string role = ChallengeCatalog.NormalizeRole(__0.Id.Entry);
        MainFile.Logger.Info($"[ChallengePoints] new run requested: character={__0.Id.Entry}, ascension={__6}, common={ChallengeSelection.Score("common")}, role={ChallengeSelection.Score(role)}.");
    }

    private static void Postfix(ref Task<RunState> __result)
    {
        __result = ChallengeStartupFlow.Observe(__result,
            () => MainFile.Logger.Info("[ChallengePoints] new run startup completed."),
            ex => MainFile.Logger.Error($"[ChallengePoints] new run startup failed before completion: {ex}"));
    }
}

[HarmonyPatch(typeof(NRun), nameof(NRun._Ready))]
internal static class ChallengeRunUiTracePatch
{
    private static void Prefix() => MainFile.Logger.Info("[ChallengePoints] run UI initialization begin.");
    private static void Postfix(NRun __instance)
    {
        MainFile.Logger.Info("[ChallengePoints] run UI initialization complete.");
        try
        {
            ChallengeContract? contract = RunManager.Instance.DebugOnlyGetState()?.Modifiers.OfType<ChallengeContract>().FirstOrDefault();
            if (contract is not null && __instance.GetNodeOrNull<ChallengeContractHud>("ChallengeContractHud") is null)
                __instance.AddChild(new ChallengeContractHud(contract));
        }
        catch (Exception ex) { MainFile.Logger.Warn($"[ChallengePoints] contract display unavailable: {ex.Message}"); }
    }
    private static Exception? Finalizer(Exception? __exception)
    {
        if (__exception is not null)
            MainFile.Logger.Error($"[ChallengePoints] run UI initialization failed: {__exception}");
        return __exception;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateMap))]
internal static class ChallengeMapTracePatch
{
    private static void Prefix() => MainFile.Logger.Info("[ChallengePoints] map generation begin.");
    private static void Postfix(ref Task __result)
    {
        __result = ChallengeStartupFlow.Observe(__result,
            () => MainFile.Logger.Info("[ChallengePoints] map generation complete."),
            ex => MainFile.Logger.Error($"[ChallengePoints] map generation failed: {ex}"));
    }
}

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
            if (ChallengeSelection.Score("common") == 0 && ChallengeSelection.Score(role) == 0 && ChallengeSelection.Spent(role) == 0) return;
            var contract = ModelDb.Modifier<ChallengeContract>().ToMutable() as ChallengeContract;
            if (contract is null) return;
            contract.CharacterRole = role;
            contract.ContractData = ChallengeSelection.Snapshot(role);
            contract.ShopSchemaVersion = 1;
            modifiers = modifiers.Append(contract).ToArray();
            MainFile.Logger.Info("[ChallengePoints] challenge contract attached to new run.");
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

// FadeIn is private in PC v107.1 and public in mobile v110.1.
[HarmonyPatch(typeof(RunManager), "FadeIn")]
internal static class ChallengeStartupRewardsFadePatch
{
    private static void Postfix(RunManager __instance, ref Task __result)
    {
        // Capture the current run. FadeIn can also be invoked while closing a
        // reward screen; the contract's one-shot/reentrancy guards handle that.
        RunState? run = __instance.DebugOnlyGetState();
        if (run is null || !run.Modifiers.Any(x => x is ChallengeContract)) return;
        __result = ChallengeStartupFlow.AfterFade(__result, async () =>
        {
            if (!ReferenceEquals(__instance.DebugOnlyGetState(), run)) return;
            MainFile.Logger.Info("[ChallengePoints] room fade-in completed; checking startup rewards.");
            foreach (ChallengeContract contract in run.Modifiers.OfType<ChallengeContract>())
            {
                try { await contract.GrantStartupRewardsAfterFadeIn(); }
                catch (Exception ex)
                {
                    // Do not strand the now-visible Neow screen if a reward
                    // supplied by another mod fails during its own callback.
                    MainFile.Logger.Error($"[ChallengePoints] startup reward failed after fade-in: {ex}");
                }
                try { await contract.ProcessShopChoicesAfterFadeIn(); }
                catch (Exception ex) { MainFile.Logger.Error($"[ChallengePoints] shop choice failed after fade-in: {ex}"); }
            }
        });
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
