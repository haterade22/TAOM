namespace TAOM.Features.FieldCamp;

/// <summary>
/// The armed-ambush decision. Throttled by the CALLER to a game-time cadence (the source scanned
/// every hostile with pathfinding every frame); this service only answers for one candidate.
/// </summary>
public interface ICampAmbushService
{
    /// <summary>Chance the ambush springs on this candidate: clamp01(base + (1 - spotting/maxRange)
    /// x 0.3 + scouting/300). Non-finite inputs give 0.
    ///
    /// <para><paramref name="playerSpottingRange"/> is the PLAYER party's spotting range, exactly
    /// as the source module wired it (its <c>ComputeAmbushSuccess(spottingRange, ...)</c> was fed
    /// <c>GetPartySpottingRange(main)</c>), NOT the candidate's distance: a wider sight radius
    /// erodes the concealment term. Kept as source behaviour; the parameter is named honestly so
    /// nobody "fixes" the caller against a distance reading of the contract again.</para></summary>
    float TriggerChance(float playerSpottingRange, float maxRange, float baseChance, float scoutingSkill);

    /// <summary>Factor applied to the ambushed party's recent-events morale (source halved it).</summary>
    float AmbushedMoraleFactor { get; }
}
