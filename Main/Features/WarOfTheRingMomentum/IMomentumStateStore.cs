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
    void ResetForNewGame();
}
