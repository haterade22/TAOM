using System.Collections.Generic;
using TAOM.Core.Logging;
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
/// settlement dominate every candidate on the map.</para>
///
/// <para>Each bound matches the range declared on the corresponding <c>TaomSettings</c> attribute,
/// and each fallback is that property's compiled default rather than a neutral value, so a rejected
/// setting restores intended behaviour instead of silently disabling a lever.</para>
///
/// <para><b>Rejections warn once per property.</b> csharp-architecture.md requires a warning and a
/// summary, but these properties are read hundreds of times per AI tick, so warning on every read
/// would flood the log and bury the thing it is trying to surface. The first rejection of each
/// property logs, and the first rejection overall logs the summary line.</para>
/// </summary>
public class ArmyTargetingSettingsProvider : IArmyTargetingSettingsProvider
{
    private const float DefaultCommitmentMultiplier = 4.0f;
    private const float DefaultMaxPriorityBoost = 3.0f;
    private const float DefaultEvilAggressionScale = 1.0f;
    private const float DefaultBorderProximityFloor = 0.15f;
    private const float DefaultBorderRescueRadius = 3.2f;
    private const float DefaultDefenderPriorityMultiplier = 1.6f;

    private readonly IModLogger _logger;
    private readonly HashSet<string> _warned = new HashSet<string>();
    private bool _warnedSummary;

    public ArmyTargetingSettingsProvider(IModLogger logger)
    {
        _logger = logger;
    }

    public bool EnableArmyStrategicIntelligence => TaomSettings.Instance?.EnableArmyStrategicIntelligence ?? true;

    public bool EnableWarTheaters => TaomSettings.Instance?.EnableWarTheaters ?? true;

    public float CommitmentMultiplier =>
        Sane("ArmyCommitmentMultiplier", TaomSettings.Instance?.ArmyCommitmentMultiplier, 1.0f, 10.0f, DefaultCommitmentMultiplier);

    public float MaxPriorityBoost =>
        Sane("ArmyPriorityBoost", TaomSettings.Instance?.ArmyPriorityBoost, 1.0f, 5.0f, DefaultMaxPriorityBoost);

    public float EvilAggressionScale =>
        Sane("EvilFactionAggressionScale", TaomSettings.Instance?.EvilFactionAggressionScale, 0.5f, 3.0f, DefaultEvilAggressionScale);

    public float BorderProximityFloor =>
        Sane("ArmyBorderProximityFloor", TaomSettings.Instance?.ArmyBorderProximityFloor, 0f, 1.0f, DefaultBorderProximityFloor);

    public float BorderRescueRadiusInTownGaps =>
        Sane("ArmyBorderRescueRadius", TaomSettings.Instance?.ArmyBorderRescueRadius, 1.0f, 20.0f, DefaultBorderRescueRadius);

    public float DefenderPriorityMultiplier =>
        Sane("ArmyDefenderPriority", TaomSettings.Instance?.ArmyDefenderPriority, 1.0f, 5.0f, DefaultDefenderPriorityMultiplier);

    /// <summary>
    /// Returns the value when it is finite and inside the declared range, otherwise the compiled
    /// default. <c>FiniteFloatValidator</c> handles the NaN case that a bare range comparison would
    /// let through, because every NaN comparison evaluates false.
    /// </summary>
    private float Sane(string name, float? value, float min, float max, float fallback)
    {
        if (value.HasValue && FiniteFloatValidator.IsFiniteInRange(value.Value, min, max))
            return value.Value;

        Warn(name, value, min, max, fallback);
        return fallback;
    }

    private void Warn(string name, float? value, float min, float max, float fallback)
    {
        if (_logger == null) return;
        if (!_warned.Add(name)) return;

        _logger.LogWarning(
            $"ArmyTargetingSettingsProvider: MCM setting {name}={(value.HasValue ? value.Value.ToString() : "null")} " +
            $"must be a finite value in [{min},{max}], using the compiled default {fallback}. Logged once per setting.");

        if (_warnedSummary) return;
        _warnedSummary = true;
        _logger.LogWarning("ArmyTargetingSettingsProvider: one or more MCM settings were rejected. See prior warnings for details.");
    }
}
