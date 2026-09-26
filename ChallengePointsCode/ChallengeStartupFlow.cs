namespace ChallengePoints;

/// <summary>Keep interactive rewards outside the room-enter/black-veil phase.</summary>
internal static class ChallengeStartupFlow
{
    internal static async Task AfterFade(Task fade, Func<Task> grantRewards)
    {
        await fade;
        await grantRewards();
    }
}
