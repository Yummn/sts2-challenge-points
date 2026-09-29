from pathlib import Path
import json


ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "ChallengePointsCode" / "Patches.cs").read_text(encoding="utf-8")
MANIFEST = json.loads((ROOT / "ChallengePoints.json").read_text(encoding="utf-8"))


def require(text: str, label: str) -> None:
    if text not in SOURCE:
        raise AssertionError(f"missing {label}: {text}")


require("ChallengeNeowCompatibilityPatch", "Neow compatibility patch")
require('"GenerateInitialOptions"', "Neow generator target")
require("modifier is ChallengeContract", "ChallengeContract filter")
require("Array.Empty<ModifierModel>()", "temporary vanilla modifier list")
require("OriginalMethod.Invoke(__instance, null)", "vanilla Neow option generation")
require("SetModifiersMethod.Invoke(owner.RunState", "modifier list restoration")
require("restored vanilla Neow blessing options", "diagnostic log")

version = tuple(int(part) for part in MANIFEST["version"].lstrip("v").split("."))
if version < (0, 1, 3):
    raise AssertionError("manifest version must be at least v0.1.3")

CONTRACT = (ROOT / "ChallengePointsCode" / "ChallengeContract.cs").read_text(encoding="utf-8")
room_hook = CONTRACT.split("public override Task AfterRoomEntered", 1)[1].split("internal async Task GrantStartupRewardsAfterFadeIn", 1)[0]
assert "await GiveRelic" not in room_hook
assert "await GivePotion" not in room_hook
assert "return Task.CompletedTask;" in room_hook
rewards = CONTRACT.split("internal async Task GrantStartupRewardsAfterFadeIn", 1)[1].split("public override bool TryModifyRewards", 1)[0]
assert "if (StartupRewardsGranted || _grantingStartupRewards) return;" in rewards
assert rewards.index("StartupRewardsGranted = true;") < rewards.index("await GiveRelic")
assert "finally { _grantingStartupRewards = false; }" in rewards
require("ChallengeStartupRewardsFadePatch", "post fade-in reward hook")
require('[HarmonyPatch(typeof(RunManager), "FadeIn")]', "private/public compatible fade target")
require("ChallengeStartupFlow.AfterFade", "await fade before rewards")
FLOW = (ROOT / "ChallengePointsCode" / "ChallengeStartupFlow.cs").read_text(encoding="utf-8")
assert FLOW.index("await fade;") < FLOW.index("await grantRewards();")
require("ChallengeAscensionPortraitTextPatch", "ascension portrait missing-text guard")
require("table.HasEntry", "preserve existing localized and fallback entries")
require("table.MergeWith(missing)", "merge only missing portrait titles")
require("ChallengeNewRunTracePatch", "new-game startup diagnostics")
require("ChallengeRunUiTracePatch", "run UI startup diagnostics")
require("ChallengeMapTracePatch", "map startup diagnostics")
require("return __exception;", "preserve actual startup failures")
assert "AscensionLevel =" not in SOURCE
assert "ref int level" not in SOURCE

print("ChallengePoints Neow compatibility checks passed.")
