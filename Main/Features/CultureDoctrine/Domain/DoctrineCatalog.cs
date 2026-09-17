using System;
using System.Collections.Generic;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// Every doctrine the config authored, keyed by culture StringId (case-insensitive), plus the
/// default every other culture receives. <see cref="VanillaEquivalent"/> reproduces exactly what
/// <c>MissionCombatantsLogic.EarlyStart</c> registers (`MissionCombatantsLogic.cs:169-196`), so
/// a missing or broken file changes nothing about how battles play.
/// </summary>
public sealed class DoctrineCatalog
{
    public const string DefaultKey = "default";

    private readonly Dictionary<string, Doctrine> _byCulture;

    public DoctrineCatalog(bool enabled, Doctrine @default, IEnumerable<Doctrine> cultures)
        : this(enabled, @default, cultures, EngagementTunables.Default)
    {
    }

    public DoctrineCatalog(bool enabled, Doctrine @default, IEnumerable<Doctrine> cultures, EngagementTunables engagement)
    {
        Enabled = enabled;
        Engagement = engagement;
        Default = @default ?? throw new ArgumentNullException(nameof(@default));
        _byCulture = new Dictionary<string, Doctrine>(StringComparer.OrdinalIgnoreCase);
        HasFormationRouting = !Default.Formations.IsEmpty;
        foreach (var doctrine in cultures ?? Array.Empty<Doctrine>())
        {
            _byCulture[doctrine.CultureId] = doctrine;
            HasFormationRouting |= !doctrine.Formations.IsEmpty;
        }
    }

    public bool Enabled { get; }
    public Doctrine Default { get; }

    /// <summary>The engagement distances every tactic and foot behaviour is installed with.</summary>
    public EngagementTunables Engagement { get; }
    public IReadOnlyCollection<string> CultureIds => _byCulture.Keys;

    /// <summary>Any doctrine routes a troop into a formation of its own; without one the
    /// mission never subscribes to <c>GetAgentTroopClass_Override</c>.</summary>
    public bool HasFormationRouting { get; }

    public Doctrine Resolve(string? cultureId) =>
        cultureId != null && _byCulture.TryGetValue(cultureId, out var doctrine) ? doctrine : Default;

    public static DoctrineCatalog VanillaEquivalent() =>
        new DoctrineCatalog(enabled: true, VanillaDefault(), Array.Empty<Doctrine>());

    /// <summary>Vanilla's registration as doctrine rows: Charge always; at Tactics 20
    /// FullScaleAttack plus DefensiveEngagement and DefensiveLine (defender) or
    /// RangedHarrassmentOffensive (attacker); at 50 FrontalCavalryCharge plus DefensiveRing and
    /// HoldChokePoint (defender) or CoordinatedRetreat (attacker). Every multiplier is 1.</summary>
    public static Doctrine VanillaDefault() => new Doctrine(DefaultKey, new[]
    {
        new TacticEntry(DoctrineTactic.Charge, 1f, 0, DoctrineSide.Any),
        new TacticEntry(DoctrineTactic.FullScaleAttack, 1f, 20, DoctrineSide.Any),
        new TacticEntry(DoctrineTactic.DefensiveEngagement, 1f, 20, DoctrineSide.Defender),
        new TacticEntry(DoctrineTactic.DefensiveLine, 1f, 20, DoctrineSide.Defender),
        new TacticEntry(DoctrineTactic.RangedHarrassmentOffensive, 1f, 20, DoctrineSide.Attacker),
        new TacticEntry(DoctrineTactic.FrontalCavalryCharge, 1f, 50, DoctrineSide.Any),
        new TacticEntry(DoctrineTactic.DefensiveRing, 1f, 50, DoctrineSide.Defender),
        new TacticEntry(DoctrineTactic.HoldChokePoint, 1f, 50, DoctrineSide.Defender),
        new TacticEntry(DoctrineTactic.CoordinatedRetreat, 1f, 50, DoctrineSide.Attacker),
    }, isDefault: true);
}
