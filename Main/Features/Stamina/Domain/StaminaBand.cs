namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// How tired a soldier is (#1497, docs/features/stamina.md). The band, not the stamina number, is what the stat
/// models, the input strip and the HUD read. The order is the order of tiredness: a higher value is more tired.
/// </summary>
public enum StaminaBand
{
    Fresh = 0,
    Winded = 1,
    Tired = 2,

    /// <summary>Stamina reached zero: no attack, no block, no run, no jump, until it is back at the recovery level.</summary>
    Exhausted = 3,
}
