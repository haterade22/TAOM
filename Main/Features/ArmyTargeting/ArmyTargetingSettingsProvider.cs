using TAOM.Core.Validation;

namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// MCM boundary for ArmyTargeting.
///
/// <para><b>Every float is validated here, not passed through.</b> MCM values live in a per-user
/// json2 file outside the save, are hand-editable, and survive a mod update, so a corrupt or
/// out-of-range value reaches the AI exactly as a bad config file would. The consequence is not
/// cosmetic: <c>ArmyBorderProximityFloor</c> is assigned straight into <c>bestDistanceScore</c>,
/// which the engine multiplies into the final behaviour score, so an infinity there makes one
/// settlement dominate every candidate on the map. This is the "enforce the same invariants at BOTH
/// surfaces" clause of csharp-architecture.md's config rule, applied to the surface that had none.
/// </para>
///
/// <para>Each bound matches the range declared on the corresponding <c>TaomSettings</c> attribute,
/// and each fallback is that property's compiled default rather than a neutral value, so a rejected
/// setting restores intended behaviour instead of silently disabling a lever.</para>
/// </summary>
public class ArmyTargetingSettingsProvider : IArmyTargetingSettingsProvider
{
    private const float DefaultCommitmentMultiplier = 4.0f;
    private const float DefaultMaxPriorityBoost = 3.0f;
    private const float DefaultEvilAggressionScale = 1.0f;
    private const float DefaultBorderProximityFloor = 0.15f;
    private const float DefaultReachRadiusInTownGaps = 6.0f;
    private const float DefaultDefenderPriorityMultiplier = 1.6f;

    public bool EnableArmyStrategicIntelligence => TaomSettings.Instance?.EnableArmyStrategicIntelligence ?? true;

    public bool EnableWarTheaters => TaomSettings.Instance?.EnableWarTheaters ?? true;

    public float CommitmentMultiplier =>
        Sane(TaomSettings.Instance?.ArmyCommitmentMultiplier, 1.0f, 10.0f, DefaultCommitmentMultiplier);

    public float MaxPriorityBoost =>
        Sane(TaomSettings.Instance?.ArmyPriorityBoost, 1.0f, 5.0f, DefaultMaxPriorityBoost);

    public float EvilAggressionScale =>
        Sane(TaomSettings.Instance?.EvilFactionAggressionScale, 0.5f, 3.0f, DefaultEvilAggressionScale);

    public float BorderProximityFloor =>
        Sane(TaomSettings.Instance?.ArmyBorderProximityFloor, 0f, 1.0f, DefaultBorderProximityFloor);

    public float ReachRadiusInTownGaps =>
        Sane(TaomSettings.Instance?.ArmyReachRadiusInTownGaps, 1.0f, 20.0f, DefaultReachRadiusInTownGaps);

    public float DefenderPriorityMultiplier =>
        Sane(TaomSettings.Instance?.ArmyDefenderPriority, 1.0f, 5.0f, DefaultDefenderPriorityMultiplier);

    /// <summary>
    /// Returns the value when it is finite and inside the declared range, otherwise the compiled
    /// default. Deliberately silent: this is read hundreds of times per AI tick, so warning here
    /// would flood the log. <c>FiniteFloatValidator</c> handles the NaN case that a bare range
    /// comparison would let through, because every NaN comparison evaluates false.
    /// </summary>
    private static float Sane(float? value, float min, float max, float fallback) =>
        value.HasValue && FiniteFloatValidator.IsFiniteInRange(value.Value, min, max)
            ? value.Value
            : fallback;
}
