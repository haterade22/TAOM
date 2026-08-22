namespace TAOM.Features.FieldCamp;

/// <summary>
/// The armed-ambush decision. Throttled by the CALLER to a game-time cadence (the source scanned
/// every hostile with pathfinding every frame); this service only answers for one candidate.
/// </summary>
public interface ICampAmbushService
{
    /// <summary>Chance the ambush springs on this candidate: clamp01(base + (1 - spotting/maxRange)
    /// x 0.3 + scouting/300). Non-finite inputs give 0.</summary>
    float TriggerChance(float candidateSpottingDistance, float maxRange, float baseChance, float scoutingSkill);

    /// <summary>Factor applied to the ambushed party's recent-events morale (source halved it).</summary>
    float AmbushedMoraleFactor { get; }
}
