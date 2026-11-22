namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// The ids of the in-game outcome lines of the wave fight (docs/features/ingame-verdicts.md), under <c>taom.training-waves.</c>. The
/// service writes <c>offer</c>, <c>stake</c>, <c>payout</c>, <c>forfeit</c>, <c>debrief</c> and <c>run-end</c>; the mission side writes
/// <c>stand-down</c>, <c>wave</c> and <c>return</c>.
/// </summary>
internal static class TrainingWavesChecks
{
    /// <summary>The offer was shown, or skipped with the reason.</summary>
    internal const string Offer = "taom.training-waves.offer";

    /// <summary>The player's gold fell by exactly the stake.</summary>
    internal const string Stake = "taom.training-waves.stake";

    /// <summary>Vanilla's practice was stood down: <c>faded=</c> and <c>vanilla_spawned=30</c>.</summary>
    internal const string StandDown = "taom.training-waves.stand-down";

    /// <summary>A wave spawned exactly its count and never more than its at-once limit.</summary>
    internal const string Wave = "taom.training-waves.wave";

    /// <summary>The player's gold rose by exactly the payout.</summary>
    internal const string Payout = "taom.training-waves.payout";

    /// <summary>The master's conversation is open again and the player is active.</summary>
    internal const string Return = "taom.training-waves.return";

    /// <summary>Vanilla's <c>AfterPractice</c> was false when the master's debrief was taken, so its prize could not pay.</summary>
    internal const string Debrief = "taom.training-waves.debrief";

    /// <summary>The gold change since the run's first stake equals the run's net.</summary>
    internal const string RunEnd = "taom.training-waves.run-end";

    /// <summary>The stake of a fight left unfinished went to the town.</summary>
    internal const string Forfeit = "taom.training-waves.forfeit";
}
