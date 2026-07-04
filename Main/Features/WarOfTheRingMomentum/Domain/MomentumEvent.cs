namespace TAOM.Features.WarOfTheRingMomentum.Domain;

/// <summary>
/// One time-limited momentum contribution. Immutable. EndTimeHours is the campaign
/// time (CampaignTime.ToHours) after which the value decays back out — converted at
/// the behavior boundary so the domain stays TaleWorlds-free (ADR-007).
/// Description is a resolved display string (localized at creation time).
/// </summary>
public class MomentumEvent
{
    public int Value { get; }
    public string Description { get; }
    public MomentumActionType Type { get; }
    public double EndTimeHours { get; }

    public MomentumEvent(int value, string description, MomentumActionType type, double endTimeHours)
    {
        Value = value;
        Description = description ?? string.Empty;
        Type = type;
        EndTimeHours = endTimeHours;
    }
}
