using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.StoryMissions;

/// <summary>
/// What a story mission's logic asks on each tick (#1105): has the Long Defeat run a result yet, what does it mean to the player, and has the end cue
/// lasted long enough to leave. Writes one <c>[Check]</c> line for the mission: a pass for a win or a loss, a skip for a stalemate or a mission left
/// before its run had a result, a fail for a fault; a mission left after a result (a retreat) writes that result's verdict. Plain state over a result and a flag, so it needs no engine; the logic feeds it the session's values.
/// </summary>
internal sealed class StoryEndWatch
{
    internal const string EndedId = "taom.story-missions.ended";

    /// <summary>How long the end cue shows before the mission ends: long enough to read the message.</summary>
    internal const float EndDelaySeconds = 5f;

    private readonly StorySide _side;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly string _key;
    private bool _recorded;
    private bool _done;
    private float _seconds;

    /// <param name="missionKey">One value per mission: the outcome log latches each line once per id, result and key for the whole process.</param>
    public StoryEndWatch(StorySide side, IModLogger logger, IOutcomeLog outcomes, string missionKey)
    {
        _side = side;
        _logger = logger;
        _outcomes = outcomes;
        _key = missionKey;
    }

    /// <summary>The verdict once the run has one or the mission was left, else null.</summary>
    public StoryOutcome? Verdict { get; private set; }

    /// <summary>
    /// One mission tick. True once, when the end cue has lasted <see cref="EndDelaySeconds"/> and the mission should end. A faulted run never asks, because
    /// the Long Defeat host is already leaving. A bad <paramref name="dt"/> (zero, negative, NaN, infinite) is not counted.
    /// </summary>
    public bool Step(float dt, RunResult? result, bool faulted)
    {
        if (_done)
            return false;
        if (!_recorded)
        {
            if (!faulted && result == null)
                return false;

            Record(result, faulted);
        }

        if (faulted)
            return false;

        if (dt > 0f && !float.IsInfinity(dt))
            _seconds += dt;
        if (_seconds < EndDelaySeconds)
            return false;

        _done = true;
        return true;
    }

    /// <summary>
    /// The mission ended by an exit this watch did not ask for (the player left, a retreat, the host left after a fault). Safe from both exits, and from a mission
    /// that already ended. The arguments are the session's <c>Result</c> (which survives its <c>Clear</c>; a retreat leaves Abandoned) and whether it faulted.
    /// Every vanilla exit ends through <c>Mission.EndMission</c>, Exit to Main Menu included (<c>MBGameManager.EndGame</c> calls it while a mission is open).
    /// So <c>OnEndMission</c> runs in registration order, and the host has shut the run down before this runs: a run it abandoned arrives as Abandoned, which a
    /// defender reads as a loss. <c>OnRemoveBehavior</c> runs later, in reverse, and finds the watch done. With neither a result nor a fault, the run never started.
    /// </summary>
    public void Left(RunResult? result, bool faulted)
    {
        if (_done)
            return;

        _done = true;
        if (_recorded)
            return;

        if (faulted || result != null)
        {
            Record(result, faulted);
            return;
        }

        _recorded = true;
        Verdict = StoryOutcome.Undecided;
        _outcomes.Skip(EndedId, _key, $"verdict=Undecided side={_side} reason=left");
        _logger.LogInfo($"[StoryMissions] {_key} left before the run ended: Undecided");
    }

    private void Record(RunResult? result, bool faulted)
    {
        var verdict = StoryVerdict.Of(_side, result, faulted);
        Verdict = verdict;
        _recorded = true;
        _logger.LogInfo($"[StoryMissions] {_key} {_side}: {verdict} ({(faulted ? "fault" : result!.Reason.ToString())})");
        if (faulted)
            _outcomes.Fail(EndedId, _key, $"verdict=Undecided side={_side} reason=fault");
        else if (verdict == StoryOutcome.Undecided)
            _outcomes.Skip(EndedId, _key, Detail(verdict, result!));
        else
            _outcomes.Pass(EndedId, _key, Detail(verdict, result!));
    }

    private string Detail(StoryOutcome verdict, RunResult result) =>
        $"verdict={verdict} side={_side} reason={result.Reason} waves={result.WavesCleared}";
}
