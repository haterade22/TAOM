using System.Collections.Generic;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Features.FieldCamp;

/// <summary>Why a camp cannot be pitched here; None means it can.</summary>
public enum CampBlockReason
{
    None = 0,
    FeatureDisabled = 1,
    Moving = 2,
    InSettlement = 3,
    TooCloseToTown = 4,
    TerrainUnsuitable = 5,
    AlreadyCamped = 6,
    Enlisted = 7,

    /// <summary>Blocked by an <see cref="ICampOverlayContributor"/> (Refuge standing here).</summary>
    External = 8,
}

/// <summary>
/// The camp book and its state machine. Owns the persisted dictionary (party StringId to state);
/// the campaign behavior hands it through Load/Save at SyncData time. Engine reads (terrain,
/// morale, roster grain, gold) happen at this boundary so the pure decisions stay in the terrain
/// and ambush services.
/// </summary>
public interface ICampService
{
    /// <summary>The main party's camp, or null.</summary>
    CampState PlayerCamp { get; }

    /// <summary>Whether the main party could pitch this camp type here right now.</summary>
    CampBlockReason CanEstablish(CampType type);

    /// <summary>Pitches the camp (holds the party, starts the raise). False when blocked.</summary>
    bool Establish(CampType type);

    /// <summary>Breaks the player camp: removes visuals, cancels supply orders PLACED FROM THE
    /// CAMP (town-placed orders keep advancing), frees movement. Safe to call with no camp.</summary>
    void BreakPlayerCamp();

    /// <summary>Upgrades a READY field camp to fortified for the configured gold. A real second
    /// raise at double the setup hours (source behaviour): the camp regresses to raising and its
    /// morale/forage pause until the palisade stands. The foraging flag, accumulator and tally
    /// survive the upgrade (the source silently wiped them). False when not applicable or
    /// unaffordable.</summary>
    bool Fortify();

    /// <summary>Toggles foraging on a ready field/fortified camp. False when not applicable.</summary>
    bool ToggleForaging();

    /// <summary>Hourly: morale + forage on a ready player camp; breaks the camp cleanly when the
    /// party has entered a settlement or been taken captive. The cleanup folds run even while the
    /// master toggle is off; only the gameplay effects are gated.</summary>
    void HourlyTick();

    /// <summary>Frame-cadence upkeep, internally throttled: the move-away guard (hold + confirm
    /// inquiry), ambush proximity checks on a game-time accumulator, visual recreation retries.</summary>
    void FrameTick();

    /// <summary>SyncData plumbing.</summary>
    void LoadFrom(Dictionary<string, CampState> camps);

    void SaveInto(out Dictionary<string, CampState> camps);

    /// <summary>Post-load: re-show ready-camp visuals once the map scene is live.</summary>
    void OnGameLoaded();

    /// <summary>Clears the book and every transient (ambush clock, move latch, visual retries)
    /// for a session with no saved record; see ISupplyOrderService.ResetForNewSession.</summary>
    void ResetForNewSession();
}
