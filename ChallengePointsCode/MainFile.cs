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

    internal static string? IntegrationMode
    {
        get
        {
            string? environment = System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_INTEGRATION");
            if (!string.IsNullOrWhiteSpace(environment)) return environment;
            string[] args = OS.GetCmdlineArgs();
            const string prefix = "--challenge-points-integration=";
            string? inline = args.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.Ordinal));
            if (inline is not null) return inline[prefix.Length..];
            int index = Array.IndexOf(args, "--challenge-points-integration");
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }

    internal static bool EmberAutopickRequested =>
        System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_EMBER_AUTOPICK") == "1"
        || OS.GetCmdlineArgs().Contains("--challenge-points-ember-autopick", StringComparer.Ordinal);

    public static void Initialize()
    {
        ChallengeCatalog.Load();
        ChallengeSelection.LoadPreset();
#if !STS2_V110
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(ChallengeContract));
#endif
        new Harmony(ModId).PatchAll();
        if ((System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_SMOKE") == "1"
                || !string.IsNullOrEmpty(IntegrationMode))
            && Engine.GetMainLoop() is SceneTree tree && tree.Root is not null)
            tree.Root.CallDeferred(Node.MethodName.AddChild, new SmokeRunner());
        Logger.Info($"[ChallengePoints] loaded v0.2.6; v111 rest-site ABI and squad triggers enabled; catalog={ChallengeCatalog.All.Count} challenges.");
    }
}
