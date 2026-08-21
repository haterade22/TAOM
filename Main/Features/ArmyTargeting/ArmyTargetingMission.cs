namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// TAOM's own mission classification, mapped from <c>Army.ArmyTypes</c> at the model boundary.
///
/// Why not pass the engine enum: the service layer stays free of TaleWorlds types so it is unit
/// testable without a live campaign (ADR-007 boundary rule). The previous shape used a bare
/// <c>bool isBesieger</c>, which could not express the Defender case the home-defence lever needs.
/// </summary>
public enum ArmyTargetingMission
{
    /// <summary>Anything that is not one of the cases below. Passes through unmodified.</summary>
    Other = 0,

    /// <summary>Army.ArmyTypes.Besieger — the only mission that receives priority, theater and reach terms.</summary>
    Besieger = 1,

    /// <summary>Army.ArmyTypes.Raider. Vanilla already hard-zeroes raiders past 5 town gaps, so TAOM leaves it alone.</summary>
    Raider = 2,

    /// <summary>Army.ArmyTypes.Defender — receives the home-defence multiplier.</summary>
    Defender = 3,
}
