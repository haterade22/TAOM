using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.Execution;

namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// The <c>CapturedFortHold</c> section of army_targeting.json: how long a good army that has just taken a fortress digs in
/// (Hold the Walls, #871, docs/features/hold-the-walls.md). Validated on load by
/// <see cref="ArmyTargetingConfigProvider"/>; the compiled values here are the defaults a missing section or a rejected
/// field falls back to.
/// </summary>
public sealed class CapturedFortHoldConfig
{
    /// <summary>
    /// Hours the engine already pauses a capturing army leader (<c>KingdomManager.SiegeCompleted</c> calls
    /// <c>DisableForHours(10)</c>). A hold shorter than this would shorten the engine's own pause, so
    /// <see cref="MinHours"/> must stay above it.
    /// </summary>
    public const float VanillaPauseHours = 10f;

    /// <summary>
    /// The longest a hold may last from the capture. The engine disbands an army whose leader waits on Hold for 48 hours
    /// (<c>Army.CheckInactivity</c>, twice <c>HoursInDay</c>). The hold counts toward that allowance and releasing it does
    /// not reset the counter, so a long hold leaves the army only the rest of the 48 hours to march out: the shipped 30 hour
    /// ceiling leaves about 14 or more, this 32 hour code cap a little less.
    /// </summary>
    public const float CodeCapHours = 32f;

    /// <summary>Sides whose armies hold a captured fort, by <see cref="FactionSide"/> name. Free only by default.</summary>
    public List<string> Sides { get; set; } = new List<string> { "Free" };

    /// <summary>
    /// Realms whose armies hold whatever their side, by kingdom StringId (<c>empire_s</c> is Mordor). Empty by default; the
    /// shipped file lists Mordor, whose armies took forts and marched off before the hold could keep them (#1283).
    /// </summary>
    public List<string> Factions { get; set; } = new List<string>();

    /// <summary>An army holds only with at least this much cohesion (0 to 100) when the hold would start.</summary>
    public float MinCohesionToHold { get; set; } = 45f;

    /// <summary>A hold ends early when cohesion falls below this. Never above <see cref="MinCohesionToHold"/>.</summary>
    public float ReleaseCohesion { get; set; } = 40f;

    /// <summary>A hold ends early when the army's leanest party has less food than this many days.</summary>
    public float ReleaseFoodDays { get; set; } = 1f;

    /// <summary>Hours of hold for a lord with Valor and Calculating level (before the clamp).</summary>
    public float BaseHours { get; set; } = 22f;

    /// <summary>Hours added per level of Calculating above Valor, removed per level of Valor above Calculating.</summary>
    public float PerTraitPointHours { get; set; } = 5f;

    /// <summary>Shortest hold, from the capture. Above <see cref="VanillaPauseHours"/>.</summary>
    public float MinHours { get; set; } = 12f;

    /// <summary>Longest hold, from the capture. At most <see cref="CodeCapHours"/>.</summary>
    public float MaxHours { get; set; } = 30f;

    /// <summary>True when an army of realm <paramref name="kingdomId"/>, on <paramref name="side"/>, holds: its side or its realm is listed.</summary>
    public bool HoldsArmy(string? kingdomId, FactionSide side) =>
        HoldsSide(side) || (!string.IsNullOrEmpty(kingdomId) && Factions != null && Factions.Contains(kingdomId!));

    /// <summary>True when armies of <paramref name="side"/> hold. Reads the list by name, so a hand-built config works too.</summary>
    public bool HoldsSide(FactionSide side)
    {
        if (Sides == null)
            return false;
        foreach (var name in Sides)
        {
            if (EnumNames.TryParse(name, out FactionSide parsed) && parsed == side)
                return true;
        }
        return false;
    }
}
