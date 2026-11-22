using System.Collections.Generic;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The training yard of one mission, as the wave fight sees it (docs/features/training-waves.md): the scene, the vanilla practice
/// controller that runs in it, the player's body and the wave's fighters. One instance for each mission, built by the mission logic.
/// Plain values only (ADR-007); the engine types stay inside <see cref="ArenaWaveArena"/>. Every method that writes runs from the
/// mission tick (a spawn or a fade is never made from an engine callback).
/// </summary>
internal interface IArenaWaveArena
{
    /// <summary>The number of <c>sp_arena</c> spawn frames in the scene. Fewer than two and the offer stays hidden.</summary>
    int FrameCount { get; }

    /// <summary>A conversation is open (the player is still talking to the master).</summary>
    bool InConversation { get; }

    /// <summary>The mission has its result or is ending: a mission tweak stands down.</summary>
    bool MissionEnded { get; }

    /// <summary>The player has no body, or his body is no longer active.</summary>
    bool PlayerDown { get; }

    /// <summary>The wave's fighters still on their feet.</summary>
    int ActiveFighters { get; }

    /// <summary>One more agent fits under the engine's limit (a direct <c>Mission.SpawnAgent</c> has no check of its own, #1107).</summary>
    bool AgentRoom { get; }

    /// <summary>What vanilla's controller counts as spawned now (30 after <see cref="StandVanillaDown"/>, which stops its own spawns); -1 when unreadable.</summary>
    int VanillaSpawnedCount { get; }

    /// <summary>The town's own troops that vanilla's practice fight draws from, with their tiers.</summary>
    IReadOnlyList<PoolTroop> TownTroops();

    /// <summary>Fades vanilla's practice fighters out and stops its controller from spawning more. False, with nothing changed, when it cannot be done.</summary>
    bool StandVanillaDown(out int faded);

    /// <summary>Replaces the player's body with one in the practice kit of the given weapon stage. False when it cannot be built.</summary>
    bool SpawnPlayer(int stage);

    /// <summary>Spawns one fighter of the town troop in the practice kit of the given weapon stage on the wave's team. False when it cannot be built or no room is left.</summary>
    bool SpawnFighter(string troopId, int stage);

    /// <summary>
    /// Fades the surviving fighters, puts vanilla's yard back as it stands between fights, stands the player at the master and opens
    /// the conversation. False when the master cannot be found.
    /// </summary>
    bool ReturnToMaster();

    void Announce(WaveAnnouncement kind, int wave, int amount);
}
