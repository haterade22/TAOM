namespace TAOM.Features.FordsOfWar;

/// <summary>Why the mission should return the player to the main menu now.</summary>
internal enum FordsExit
{
    None,
    AfterResult,
    AfterFailedStart,
}

/// <summary>
/// Decides when a Fords of War mission closes itself. The engine never does: <c>Mission.CheckMissionEnd</c> returns at once while the
/// mission state is Continuing and <c>MissionEnded</c> is set, so a finished (or failed) mission stays open until something calls
/// <c>EndMission</c>. The clock counts the mission time its caller passes, which stops while the engine is paused, and says
/// <see cref="FordsExit.AfterResult"/> a while after the match has a winner (long enough to read the result panel) or
/// <see cref="FordsExit.AfterFailedStart"/> shortly after a start that failed. It answers once, then stays silent. Pure.
/// </summary>
internal sealed class FordsExitClock
{
    internal const float AfterResultSeconds = 25f;
    internal const float AfterFailedStartSeconds = 3f;

    private float _resultSeconds;
    private float _failedSeconds;
    private bool _done;

    public FordsExit Tick(float dt, bool failedStart, bool matchEnded)
    {
        if (_done || !(dt > 0f))
            return FordsExit.None;

        if (failedStart)
        {
            _failedSeconds += dt;
            return Finish(_failedSeconds >= AfterFailedStartSeconds, FordsExit.AfterFailedStart);
        }

        if (!matchEnded)
            return FordsExit.None;

        _resultSeconds += dt;
        return Finish(_resultSeconds >= AfterResultSeconds, FordsExit.AfterResult);
    }

    private FordsExit Finish(bool due, FordsExit reason)
    {
        _done = due;
        return due ? reason : FordsExit.None;
    }
}
