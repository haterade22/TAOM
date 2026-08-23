using System.Collections.Generic;
using TAOM.Features.Refuge.Domain;

namespace TAOM.Features.Refuge;

/// <summary>Why a refuge action is blocked; None means allowed.</summary>
public enum RefugeBlockReason
{
    None = 0,
    FeatureDisabled = 1,
    NoReadyCampHere = 2,
    WrongCampType = 3,
    AtRefugeLimit = 4,
    TooCloseToTown = 5,
    NoWardenAvailable = 6,
    NotEnoughGold = 7,
    NoRefugeInReach = 8,
    StillBuilding = 9,
    AlreadyTopTier = 10,
    RefugeAlreadyHere = 11,
    Enlisted = 12,
}

/// <summary>
/// The refuge book and its lifecycle: found from a ready field/fortified camp, raise, manage,
/// upgrade to stronghold, dismantle. Owns the persisted dictionary; the campaign behavior hands it
/// through Load/Save at SyncData time. Engine work sits at this boundary behind protected-virtual
/// seams (the CampService/SupplyOrderService pattern) so decisions stay testable.
/// </summary>
public interface IRefugeService
{
    /// <summary>All standing refuges (data rows; parties resolved at the boundary).</summary>
    IReadOnlyCollection<RefugeData> AllRefuges { get; }

    /// <summary>Clan-tier-scaled cap: min(1 + clanTier/2, hard cap from settings).</summary>
    int RefugeLimit(int clanTier);

    /// <summary>Whether a refuge can be founded from the player's current camp.</summary>
    RefugeBlockReason CanFound();

    /// <summary>Founds a refuge here with the given warden: spawns the party, breaks the underlying
    /// field camp, charges gold, starts the raise. Null on failure (reason out).</summary>
    RefugeData Found(string wardenHeroId, out RefugeBlockReason reason);

    /// <summary>The nearest ready refuge within manage range of the main party that is not in a
    /// map event, or null. A refuge mid-battle is a live participant: managing or dismantling it
    /// would mutate the event's rosters behind the engine's back.</summary>
    RefugeData NearestManageable();

    /// <summary>The nearest refuge within manage range that can be dismantled: ready refuges plus
    /// orphan-adopted rows (see <see cref="RefugeData.IsOrphanAdopted"/>), never one in a map
    /// event. Superset of <see cref="NearestManageable"/> so orphans have an exit.</summary>
    RefugeData NearestDismantlable();

    RefugeBlockReason CanUpgrade(RefugeData refuge);

    /// <summary>Starts the stronghold upgrade (charges gold, enters the rebuild phase).</summary>
    bool Upgrade(RefugeData refuge);

    /// <summary>Dismantles: rosters and stash merge back to the main party, the warden lifecycle
    /// resolves (see IWardenService; nobody is killed), the party is destroyed, visuals removed.</summary>
    void Dismantle(RefugeData refuge);

    /// <summary>Frame-cadence: build advancement, the hold-nearby rule while a refuge raises
    /// (with a player-facing message the source never showed), visual retries. Internally throttled.</summary>
    void FrameTick();

    /// <summary>Hourly: raid rolls when enabled (off by default).</summary>
    void HourlyTick();

    /// <summary>Militia rally on a refuge entering a map event; delta recorded in RefugeData.</summary>
    void OnMapEventStarted(string partyId);

    /// <summary>Militia stand-down: removes min(recorded, present - pre-rally baseline) of the
    /// recorded troop, so pre-existing garrison of the same type survives (casualties are
    /// attributed to militia first).</summary>
    void OnMapEventEnded(string partyId);

    /// <summary>The engine destroyed a refuge party (lost defense, disband): drop the book row and
    /// visuals immediately and tell the player. A promoted warden stays a clan companion; his
    /// battle fate (death, capture) is the engine's own accounting.</summary>
    void OnPartyDestroyed(string partyId);

    /// <summary>Peace involving the player's faction: release eligible hero prisoners held in
    /// refuges, mirroring vanilla's PrisonerReleaseCampaignBehavior (which never enumerates
    /// custom party components).</summary>
    void OnPeaceMade();

    /// <summary>SyncData plumbing.</summary>
    void LoadFrom(Dictionary<string, RefugeData> refuges, int counter);

    void SaveInto(out Dictionary<string, RefugeData> refuges, out int counter);

    /// <summary>Post-load: reconcile book vs parties, re-pin AI on every refuge party.</summary>
    void OnGameLoaded();

    /// <summary>Clears the book and transients for a session with no saved record; see
    /// ISupplyOrderService.ResetForNewSession.</summary>
    void ResetForNewSession();
}
