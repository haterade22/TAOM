using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>The troop counts the deployment plan is given before any soldier spawns. They set each formation's width and depth, so they are sized for the fullest field, not the first wave.</summary>
internal readonly record struct LdPlanSizes(int DefenderFoot, int DefenderRanged, int AttackerFoot, int AttackerRanged);

/// <summary>What the engine reports once the garrison and the player are in: the facts the boot and general checks judge.</summary>
internal sealed record LdBootFacts(
    bool DeploymentFinished,
    bool SiegeTeamAi,
    bool HasMainAgent,
    bool IsFriendlyMission,
    bool GeneralIsMain,
    bool IsPlayerGeneral,
    bool OrderOwnerIsMain);

/// <summary>
/// Where one spawned batch landed. <c>Far</c> counts soldiers well off the height their frame authored, <c>OffMesh</c> soldiers
/// with no navmesh under them, <c>OnPoint</c> soldiers within a metre of the frame point (a pile).
/// </summary>
internal sealed record LdSpawnReport(IReadOnlyList<LdPoint> Points, int Far, int OffMesh, int OnPoint)
{
    public int Spawned => Points.Count;
}

/// <summary>
/// One look at the field. Standing counts live humans that are not running away; Fleeing counts the ones that are. The keep
/// counts are only meaningful when the read asked for them. <c>MainBody</c> is 0 with no player agent, otherwise a number the
/// adapter changes whenever the player's body changes, so the run never keys on an engine agent index. The last three are
/// diagnostics only: defenders running away, and how many of each side stand at or above the wall line.
/// </summary>
internal readonly record struct LdReading(
    int AttackersStanding,
    int AttackersFleeing,
    int DefendersStanding,
    int AttackersAtKeep,
    int DefendersAtKeep,
    int MainBody,
    int SiegeBattleSize,
    int DefendersFleeing = 0,
    int AttackersHigh = 0,
    int DefendersHigh = 0);

internal enum LdRemovalKind
{
    None,
    Attacker,
    Defender,
    Player,
}

/// <summary>What a removed agent was to this mission. <c>None</c> is a stranger, a horse, or a removal that is not a loss.</summary>
internal readonly record struct LdRemoval(LdRemovalKind Kind, string TroopId);

/// <summary>A rest between waves: how many defenders it reached, and their mean health as a share of their limit, before and after.</summary>
internal readonly record struct LdRest(int Count, float MeanBefore, float MeanAfter);
