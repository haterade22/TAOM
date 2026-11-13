namespace TAOM.Features.Messengers.Domain;

/// <summary>What the hourly tick does with a messenger that has reached its target, in the order the rule tests them.</summary>
public enum MessengerArrivalVerdict
{
    /// <summary>The target can no longer be reached: the "messenger lost" notice, then removal.</summary>
    Lost,
    /// <summary>The target shares the main hero's party: the "confused" notice, then removal.</summary>
    Confused,
    /// <summary>The target is nowhere on the map until the engine makes it Active again: tell the player once, then try
    /// again each hour. This wait can last days.</summary>
    WaitForTarget,
    /// <summary>The player or the target is busy: try again next hour.</summary>
    Wait,
    /// <summary>The "messenger arrived" inquiry.</summary>
    Ready,
}
