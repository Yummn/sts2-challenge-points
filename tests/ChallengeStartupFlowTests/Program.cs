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
Console.WriteLine($"PASS: {Path.GetFileName(modPath)} flow ordering, interactive gate, failed/cancelled fades, game hook signatures.");
