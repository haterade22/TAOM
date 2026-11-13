namespace TAOM.Features.Messengers.Domain;

/// <summary>What the hourly tick does with a messenger that has reached its target, in the order the rule tests them.</summary>
public enum MessengerArrivalVerdict
{
    /// <summary>The target can no longer be reached: the "messenger lost" notice, then removal.</summary>
    Lost,
    /// <summary>The target shares the main hero's party: the "confused" notice, then removal.</summary>
    Confused,
    /// <summary>The player or the target is busy: try again next hour.</summary>
    Wait,
    /// <summary>The "messenger arrived" inquiry.</summary>
    Ready,
}
