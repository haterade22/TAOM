using System.Collections.Generic;
using TAOM.Features.StaleCharacterRepair.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Engine boundary for <see cref="TAOM.Features.StaleCharacterRepair.IStaleCharacterRepairService"/>.
/// Everything crosses as string ids, counts and a <see cref="StubRepairOutcome"/> (ADR-007:
/// <c>CharacterObject</c>, <c>MBCharacterSkills</c> and <c>MBBodyProperty</c> never leave the adapter).
///
/// <para>A "stale character" is one the save restored under an id that current ModuleData no longer
/// defines. <c>CharacterObject</c> persists exactly two fields (<c>_heroObject</c> and
/// <c>_originCharacter</c>); every other field on it and on <c>BasicCharacterObject</c> comes from
/// XML <c>Deserialize</c>, which such an object never reaches. So it is not one null field but
/// several, and each one has an unguarded dereference waiting somewhere in the engine.</para>
/// </summary>
public interface IStaleCharacterAdapter
{
    /// <summary>
    /// String ids of every registered character carrying at least one of the null fields
    /// <see cref="TryMakeInert"/> repairs. Empty on a healthy load, and empty rather than throwing
    /// when the object manager is not available yet.
    /// </summary>
    IReadOnlyList<string> FindStaleCharacters();

    /// <summary>
    /// Fills every null field on one stale character with the value vanilla's own
    /// <c>Deserialize</c> fallbacks would have produced, so nothing downstream dereferences a null.
    /// Idempotent: a field that is already set is left alone.
    /// </summary>
    StubRepairOutcome TryMakeInert(string characterId);

    /// <summary>
    /// Queues a player-visible notice naming <paramref name="repairedCount"/>, shown once the
    /// campaign session is live.
    ///
    /// <para>Deferred rather than shown immediately because the repair runs inside
    /// <c>Campaign.OnGameLoaded</c>, before the HUD that renders messages exists. It matters that
    /// the player sees it at all: the stale ids are written back into every subsequent save while
    /// the repair is not, so a player who saves without knowing has overwritten the last file that
    /// could be recovered.</para>
    /// </summary>
    void ShowNoticeOnSessionStart(int repairedCount);
}
