using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

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
/// adapter changes whenever the player's body changes, so the run never keys on an engine agent index. The next three are
/// diagnostics only: defenders running away, and how many of each side stand at or above the wall line. <c>DefenderHealthMean</c> is the mean of health over limit across every active human of the
/// defending team with a limit, runners included (the people <c>RestDefenders</c> heals), or -1 when none was read; the <c>field-care</c> check compares it with the heal's own read-back.
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
    int DefendersHigh = 0,
    float DefenderHealthMean = -1f);

internal enum LdRemovalKind
{
    None,
    Attacker,
    Defender,
    Player,
}

/// <summary>
/// What a removed agent was to this mission. <c>None</c> is a stranger, a horse, or a removal that is no loss, except the commander's: an attacker comes with the <c>Role</c> the registry holds
/// for him and whether the stalemate rule's <c>Fell</c> marked him (<c>Felled</c>), and a commander removed with no loss (a rout; a deletion only if native raises a removal for it, risk 2) is reported with <c>Loss</c> false so the run stops him.
/// </summary>
internal readonly record struct LdRemoval(LdRemovalKind Kind, string TroopId, AttackerRole Role = AttackerRole.Roster, bool Felled = false, bool Loss = true, AgentKey Key = default)
{
    /// <summary>Whether the registry held the agent, so the run must forget its key (a removal of a kind <see cref="LdRemovalKind.None"/> can still carry one: a routed soldier, a deleted horse's rider).</summary>
    public bool HasKey => Key.Value > 0;
}

/// <summary>A rest between waves: how many defenders it reached, and their mean health as a share of their limit, before and after.</summary>
internal readonly record struct LdRest(int Count, float MeanBefore, float MeanAfter);

/// <summary>
/// One standing attacker as the effects adapter read it: its troop, its health limit, the maximum its character gives it (their ratio is the
/// health factor in force), and how many blows the damage factor has raised since the wave's factor was published. An empty <c>TroopId</c> means no attacker stood.
/// </summary>
internal readonly record struct LdScaleSample(string TroopId, float HealthLimit, float CharacterMax, long AmplifiedBlows)
{
    public bool HasAgent => TroopId.Length > 0;

    /// <summary>The health limit over the character's maximum; NaN when the maximum is not positive.</summary>
    public float Ratio => CharacterMax > 0f ? HealthLimit / CharacterMax : float.NaN;
}

/// <summary>One driven property of a sampled attacker: what the engine holds now, and what the stat model gives the same agent with the affixes left out.</summary>
internal readonly record struct LdStatReading(string Stat, float Base, float Actual);

/// <summary>
/// One standing human attacker with no live race ability, read for the affix proof: its troop and one reading per property asked for. An empty
/// <c>TroopId</c> means no such attacker stood.
/// </summary>
internal sealed record LdStatSample(string TroopId, IReadOnlyList<LdStatReading> Readings)
{
    public static LdStatSample None { get; } = new("", System.Array.Empty<LdStatReading>());

    public bool HasAgent => TroopId.Length > 0;
}

/// <summary>
/// One standing human of the player's side as the effects adapter read it for the boon proof: its troop, its health limit and the maximum its character gives it (their ratio is the
/// health factor in force), and one reading per driven property asked for, each against the stat model's own answer with the boons left out. An empty <c>TroopId</c> means no
/// such soldier stood: none of the group, or only runners and soldiers with a live race ability.
/// </summary>
internal sealed record LdTargetSample(string TroopId, float HealthLimit, float CharacterMax, IReadOnlyList<LdStatReading> Readings)
{
    public static LdTargetSample None { get; } = new("", 0f, 0f, System.Array.Empty<LdStatReading>());

    public bool HasAgent => TroopId.Length > 0;
}

/// <summary>
/// What one Morgul Blades tick did to a defender: the health it took (<c>Damage</c>), whether it killed him, whether it was withheld because no attacker stood to own the kill (he keeps 1 health),
/// whether the owner it chose was not active at the call (<c>OwnerMissing</c>, which must stay 0, and then nothing is killed) and whether the owner was not the poisoner (<c>Substituted</c>).
/// All false and 0 for a victim already gone.
/// </summary>
internal readonly record struct LdPoisonResult(int Damage, bool Killed, bool Withheld, bool OwnerMissing, bool Substituted);

/// <summary>What an effects-adapter call reached: how many agents, and the milliseconds it took (risk 11 reads the pick's refresh from the log).</summary>
internal readonly record struct LdTouched(int Count, double Milliseconds);
