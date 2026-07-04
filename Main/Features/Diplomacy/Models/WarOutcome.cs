namespace TAOM.Features.Diplomacy.Models;

/// <summary>
/// Final outcome of the War of the Ring. Owned by Diplomacy (war lifecycle);
/// set via IWarOfTheRingService.EndWar when the Momentum victory condition fires
/// (threshold reached or one side's kingdoms eliminated). None = war not ended.
/// </summary>
public enum WarOutcome
{
    None,
    FreeVictory,
    EvilVictory
}
