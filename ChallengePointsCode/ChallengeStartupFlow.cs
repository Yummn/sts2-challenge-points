namespace ChallengePoints;

/// <summary>Keep interactive rewards outside the room-enter/black-veil phase.</summary>
internal static class ChallengeStartupFlow
{
    internal static async Task AfterFade(Task fade, Func<Task> grantRewards)
    {
        await fade;
        await grantRewards();
    }

    internal static async Task Observe(Task task, Action complete, Action<Exception> failed)
    {
        try { await task; complete(); }
        catch (Exception ex) { failed(ex); throw; }
    }

    internal static async Task<T> Observe<T>(Task<T> task, Action complete, Action<Exception> failed)
    {
        try { T result = await task; complete(); return result; }
        catch (Exception ex) { failed(ex); throw; }
    }
}
