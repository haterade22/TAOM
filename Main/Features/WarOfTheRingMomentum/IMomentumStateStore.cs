using System;
using System.Collections.Generic;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Owns the war's momentum state + the player's recorded events, and converts them
/// to/from the primitive-dict SyncData payload (Messengers pattern — no
/// SaveableTypeDefiner). Also the seam the UI subscribes to for refresh.
/// </summary>
public interface IMomentumStateStore
{
    MomentumWarState State { get; }

    /// <summary>Raw storage for the player victory-gate events; PlayerMomentumService owns the policy (cap, count).</summary>
    List<MomentumActionType> PlayerEvents { get; }

    /// <summary>Raised after any momentum-affecting processing; UI refresh seam.</summary>
    event Action MomentumChanged;
    void NotifyMomentumChanged();

    Dictionary<string, string> Serialize();
    void Deserialize(Dictionary<string, string> data);

    /// <summary>
    /// The war state without <see cref="PlayerEvents"/>, for a co-op server to send to its clients.
    /// The server's own player events belong to no player, so they never travel.
    /// </summary>
    Dictionary<string, string> CaptureWarState();

    /// <summary>
    /// Replace the war state with one from <see cref="CaptureWarState"/> and keep this peer's
    /// <see cref="PlayerEvents"/>. A co-op client calls it with the server's state; it writes only
    /// TAOM's own copy, never an engine object. Returns false, and keeps the prior state, for an
    /// empty snapshot or one of another version.
    /// </summary>
    bool ApplyWarState(Dictionary<string, string> data);
    void ResetForNewGame();
}
