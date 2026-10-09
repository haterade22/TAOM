using System.Collections.Generic;

namespace TAOM.Features.WarChronicle.Effects;

/// <summary>
/// The shared registry of timed War of the Ring effects (docs/features/war-chronicle.md, Part A).
/// Producers (the chronicle, the rally, the console) write; consumers (the volunteer model, the
/// escape pass) read one multiplier per kingdom and kind.
/// </summary>
public interface IWarEffectService
{
    /// <summary>
    /// Adds the effect, or refreshes the one with the same (SourceId, KingdomId, Kind). Rejects, with a
    /// warning, an empty id, an undefined kind and a non-finite magnitude or end time.
    /// </summary>
    void Apply(WarEffect effect);

    /// <summary>Removes every effect the source wrote, across kingdoms and kinds.</summary>
    void RemoveSource(string sourceId);

    /// <summary>
    /// The baked multiplier, 1 for a null or unknown kingdom or kind. One dictionary lookup and one
    /// array read, no allocation: safe on a per-settlement daily path.
    /// </summary>
    float GetMultiplier(string? kingdomId, WarEffectKind kind);

    /// <summary>
    /// Removes effects whose end time is at or before <paramref name="nowHours"/>, then re-bakes every
    /// multiplier with the current MCM strength. A non-finite now does nothing.
    /// </summary>
    void Expire(double nowHours);

    /// <summary>A copy of the active effects, for saving and the console.</summary>
    IReadOnlyList<WarEffect> Snapshot();

    /// <summary>Replaces the registry with the saved rows, skipping invalid ones.</summary>
    void RestoreFromSave(IEnumerable<WarEffect> effects);

    /// <summary>Empties the registry: called from the campaign behavior's constructor (the session-reset rule).</summary>
    void ResetForNewSession();
}
