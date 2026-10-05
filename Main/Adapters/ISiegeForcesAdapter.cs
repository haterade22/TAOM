using System;
using System.Collections.Generic;
using TAOM.Features.SiegeForces.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of the siege troop picker: what the player's wall battle looks like, how the troop selection
/// screen opens, and which map event is the player's right now. Every method is total: it returns null or false on
/// any fault instead of throwing, and reads no campaign static unguarded (Custom Battle has no campaign).
/// </summary>
public interface ISiegeForcesAdapter
{
    /// <summary>
    /// The player's side of the battle he is about to fight, or null unless the player's map event is a siege assault
    /// and the siege is on the walls. A sally-out, a relief force and the lord's hall are other map events or states
    /// and return null.
    /// </summary>
    SiegeForcesSnapshot? CaptureWallBattle();

    /// <summary>
    /// Opens the vanilla "Manage Troops" screen, the one the lord's hall fight and hideouts use, with the request's
    /// rows. <paramref name="onDone"/> is called with character id to picked count when the player presses Done, and
    /// never when he cancels. Returns true only when the screen was opened; false means nothing was shown.
    /// </summary>
    bool TryOpenPicker(PickerRequest request, Action<IReadOnlyDictionary<string, int>> onDone);

    /// <summary>
    /// The player's current map event as an opaque token, or null when there is no campaign or no map event. Null-safe
    /// all the way down: the totals prefix runs for every battle, Custom Battle included.
    /// </summary>
    object? ReadMapEventToken();

    /// <summary>
    /// True when TAOM's spawn-totals prefix is attached to <c>DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase</c>.
    /// The picker is offered only then: without the fit, a selection that leaves troops out stalls deployment. False on
    /// any fault. Harmony's patch registry is not a campaign static, so this is safe in Custom Battle too.
    /// </summary>
    bool IsSpawnTotalsFitAttached();
}
