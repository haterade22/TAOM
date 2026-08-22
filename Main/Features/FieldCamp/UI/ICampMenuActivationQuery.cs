namespace TAOM.Features.FieldCamp.UI;

/// <summary>
/// The overlay button's per-guard view of "may the camp menu open right now". Split into one
/// property per guard (not a single bool) so <c>FieldCampOverlayVMTests</c> can prove each guard
/// blocks on its own; the engine-backed implementation lives at the MapView boundary
/// (<see cref="MapScreenCampMenuActivationQuery"/>).
/// </summary>
public interface ICampMenuActivationQuery
{
    /// <summary>MapState is the active game state AND no vanilla modal sub-state is open (menu,
    /// army management, marriage/heir popups, map cheats, incidents, encyclopedia, context menu).
    /// Same guard set as Patch36_MapScreenF6: any of those modals can react to a menu push.</summary>
    bool IsMapScreenClear { get; }

    /// <summary>The main party exists and is holding still. Camping while moving would fight the
    /// move order; the move-away guard owns that conversation, not the button.</summary>
    bool IsMainPartyStationary { get; }

    bool IsMainPartyInSettlement { get; }

    /// <summary>A map event or player encounter is in progress.</summary>
    bool IsMainPartyInEncounter { get; }

    bool IsMainPartyDisorganized { get; }
}
