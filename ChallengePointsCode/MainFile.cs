using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
#if !STS2_V110
using MegaCrit.Sts2.Core.Saves.Runs;
#endif

namespace ChallengePoints;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "ChallengePoints";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        ChallengeCatalog.Load();
        ChallengeSelection.LoadPreset();
#if !STS2_V110
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(ChallengeContract));
#endif
        new Harmony(ModId).PatchAll();
        if ((System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_SMOKE") == "1"
                || !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_INTEGRATION")))
            && Engine.GetMainLoop() is SceneTree tree && tree.Root is not null)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new SmokeRunner());
        Logger.Info($"[ChallengePoints] loaded v0.2.3; squad shop and startup-stage guard enabled; catalog={ChallengeCatalog.All.Count} challenges.");
    }
}
