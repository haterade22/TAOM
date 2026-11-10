using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.StoryMissions.Domain;

internal enum StoryOutcome
{
    Won,
    Lost,
    Undecided,
}

/// <summary>
/// What the way a Long Defeat run ended means to the player of a story mission. The run's end states are side-neutral: a keep that falls, or defenders
/// who all die, is a win for the player who attacks and a loss for the one who defends. A stalemate, a fault and a missing result decide nothing, so the
/// event that offered the mission can be offered again. A fault comes first: a faulted host abandons its run, and Abandoned alone reads as a loss.
/// </summary>
internal static class StoryVerdict
{
    public static StoryOutcome Of(StorySide side, RunResult? result, bool faulted)
    {
        if (faulted || result == null)
            return StoryOutcome.Undecided;

        switch (side)
        {
            case StorySide.Defending:
                switch (result.Reason)
                {
                    case RunEndReason.TargetReached:
                        return StoryOutcome.Won;
                    case RunEndReason.DefendersDead:
                    case RunEndReason.KeepLost:
                    case RunEndReason.Abandoned:
                        return StoryOutcome.Lost;
                }

                break;
            case StorySide.Attacking:
                switch (result.Reason)
                {
                    case RunEndReason.KeepLost:
                    case RunEndReason.DefendersDead:
                        return StoryOutcome.Won;
                    case RunEndReason.TargetReached:
                    case RunEndReason.Abandoned:
                        return StoryOutcome.Lost;
                }

                break;
        }

        return StoryOutcome.Undecided;
    }
}
