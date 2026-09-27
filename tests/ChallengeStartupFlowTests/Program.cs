using System.Reflection;
using System.Runtime.Loader;

string modPath = Path.GetFullPath(args[0]);
string references = Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(references, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
Assembly mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(modPath);
MethodInfo afterFade = mod.GetType("ChallengePoints.ChallengeStartupFlow", true)!
    .GetMethod("AfterFade", BindingFlags.Static | BindingFlags.NonPublic)!;
Task Invoke(Task fade, Func<Task> reward) => (Task)afterFade.Invoke(null, [fade, reward])!;
void Require(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}

// Run against the actual packaged assembly, not a copied implementation.
var fadeGate = new TaskCompletionSource();
var rewardGate = new TaskCompletionSource();
var rewardStarted = new TaskCompletionSource();
bool visible = false;
Task flow = Invoke(fadeGate.Task, () =>
{
    Require(visible, "interactive reward opened before screen became visible");
    rewardStarted.SetResult();
    return rewardGate.Task;
});
Require(!rewardStarted.Task.IsCompleted && !flow.IsCompleted, "reward ran during fade");
visible = true;
fadeGate.SetResult();
await rewardStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
Require(!flow.IsCompleted, "flow did not wait for reward interaction");
rewardGate.SetResult();
await flow.WaitAsync(TimeSpan.FromSeconds(5));

int calls = 0;
Func<Task> count = () => { calls++; return Task.CompletedTask; };
try { await Invoke(Task.FromException(new InvalidOperationException("fade failed")), count); }
catch (InvalidOperationException) { }
Require(calls == 0, "failed fade still granted rewards");
try { await Invoke(Task.FromCanceled(new CancellationToken(true)), count); }
catch (OperationCanceledException) { }
Require(calls == 0, "cancelled fade still granted rewards");
await Invoke(Task.CompletedTask, count);
Require(calls == 1, "completed fade did not grant rewards");

Assembly game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(references, "sts2.dll"));
Type manager = game.GetType("MegaCrit.Sts2.Core.Runs.RunManager", true)!;
MethodInfo target = manager.GetMethod("FadeIn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
Require(target is not null && target.ReturnType == typeof(Task), "FadeIn hook target missing or incompatible");
Require(manager.GetMethod("DebugOnlyGetState", BindingFlags.Instance | BindingFlags.Public) is not null,
    "run state getter missing");
Type helper = game.GetType("MegaCrit.Sts2.Core.Helpers.AscensionHelper", true)!;
MethodInfo hover = helper.GetMethod("GetHoverTip")!;
Require(hover.GetParameters().Any(p => p.Name == "level" && p.ParameterType == typeof(int)),
    "portrait fallback Harmony parameter missing");
Type nGame = game.GetType("MegaCrit.Sts2.Core.Nodes.NGame", true)!;
MethodInfo start = nGame.GetMethod("StartNewSingleplayerRun")!;
Require(start.GetParameters()[0].ParameterType.FullName == "MegaCrit.Sts2.Core.Models.CharacterModel"
    && start.GetParameters()[6].ParameterType == typeof(int)
    && start.ReturnType.IsGenericType && start.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
    "new-run diagnostic hook signature incompatible");
Require(manager.GetMethod("GenerateMap")!.ReturnType == typeof(Task), "map diagnostic hook incompatible");
Require(game.GetType("MegaCrit.Sts2.Core.Nodes.NRun", true)!.GetMethod("_Ready")!.ReturnType == typeof(void),
    "run UI diagnostic hook incompatible");

MethodInfo missingTitles = mod.GetType("ChallengePoints.ChallengeAscensionTextFallback", true)!
    .GetMethod("MissingTitles", BindingFlags.NonPublic | BindingFlags.Static)!;
Dictionary<string, string> Missing(int level, Func<string, bool> has, bool chinese) =>
    (Dictionary<string, string>)missingTitles.Invoke(null, [level, has, chinese])!;
var existing = Enumerable.Range(1, 10).Select(i => $"LEVEL_{i:D2}.title").ToHashSet();
Dictionary<string, string> replacements = Missing(20, existing.Contains, true);
Require(replacements.Count == 10 && replacements.ContainsKey("LEVEL_11.title"), "missing LEVEL_11 not repaired");
Require(existing.All(k => !replacements.ContainsKey(k)), "existing official titles overwritten");
existing.UnionWith(replacements.Keys);
Require(Missing(20, existing.Contains, true).Count == 0, "fallback not idempotent");
Require(Missing(10, existing.Contains, true).Count == 0, "valid ascension modified");
Require(Missing(0, _ => false, true).Count == 0 && Missing(-1, _ => false, true).Count == 0,
    "zero/negative ascension incorrectly populated");
Require(Missing(11, k => k != "LEVEL_11.title", false)["LEVEL_11.title"].Contains("unavailable"),
    "English fallback missing");

MethodInfo observe = afterFade.DeclaringType!.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
    .Single(m => m.Name == "Observe" && !m.IsGenericMethod);
int completeCalls = 0;
Exception? logged = null;
var expected = new InvalidOperationException("simulated UI init error");
Task failed = (Task)observe.Invoke(null, [Task.FromException(expected), (Action)(() => completeCalls++),
    (Action<Exception>)(ex => logged = ex)])!;
try { await failed; throw new Exception("diagnostic observer swallowed original error"); }
catch (InvalidOperationException ex) { Require(ReferenceEquals(ex, expected), "original error replaced"); }
Require(ReferenceEquals(logged, expected) && completeCalls == 0, "diagnostic callbacks incorrect");
await (Task)observe.Invoke(null, [Task.CompletedTask, (Action)(() => completeCalls++),
    (Action<Exception>)(ex => logged = ex)])!;
Require(completeCalls == 1, "diagnostic success callback missing");
MethodInfo genericObserve = afterFade.DeclaringType!.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
    .Single(m => m.Name == "Observe" && m.IsGenericMethod).MakeGenericMethod(typeof(int));
int observedResult = await (Task<int>)genericObserve.Invoke(null,
    [Task.FromResult(42), (Action)(() => completeCalls++), (Action<Exception>)(ex => logged = ex)])!;
Require(observedResult == 42 && completeCalls == 2, "new-run observer changed result");
Console.WriteLine($"PASS: {Path.GetFileName(modPath)} flow ordering, interactive gate, failed/cancelled fades, game hook signatures.");
Console.WriteLine("PASS: missing LEVEL_11 fallback, official text preservation, repeat calls, zero-level, English, startup diagnostics preserve result/error.");

Type saveManager = game.GetType("MegaCrit.Sts2.Core.Saves.SaveManager", true)!;
MethodInfo saveRun = saveManager.GetMethod("SaveRun")!;
Require(saveRun.ReturnType == typeof(Task) && saveRun.GetParameters().Length == 2
    && saveRun.GetParameters()[0].ParameterType.FullName == "MegaCrit.Sts2.Core.Rooms.AbstractRoom"
    && saveRun.GetParameters()[1].ParameterType == typeof(bool), "startup persistence save ABI incompatible");
Console.WriteLine("PASS: startup persistence save ABI is Task SaveRun(AbstractRoom, bool).");
