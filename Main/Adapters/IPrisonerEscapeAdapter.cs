using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of the War Chronicle's extra prisoner-escape roll: reads the captured lords of some
/// kingdoms, supplies the random roll and frees a lord, so the services never touch <c>Hero</c>,
/// <c>PartyBase</c> or <c>EndCaptivityAction</c> (ADR-007). Keyed by hero StringId.
/// </summary>
public interface IPrisonerEscapeAdapter
{
    /// <summary>
    /// A snapshot of every prisoner among the lords of the named kingdoms' clans that has a captor party.
    /// Empty with no campaign or no kingdom ids.
    /// </summary>
    IReadOnlyList<PrisonerEscapeSnapshot> GetCapturedLords(IReadOnlyCollection<string> kingdomIds);

    /// <summary>The engine's random float in [0, 1) (<c>MBRandom.RandomFloat</c>).</summary>
    float NextRoll();

    /// <summary>
    /// Frees the lord through <c>EndCaptivityAction.ApplyByEscape</c>. Re-checks that the hero still exists,
    /// is a prisoner with a captor and is not the main hero; returns false when it did nothing.
    /// </summary>
    bool Escape(string heroId);
}
