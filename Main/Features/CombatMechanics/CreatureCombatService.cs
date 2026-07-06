using System;
using System.Collections.Generic;

namespace TAOM.Features.CombatMechanics;

// Spec mechanics 4 (creature cleave) + 7 (creature unstoppable) + the per-race stagger threshold
// multiplier (the combat-mechanics spec). Per-hit HOT PATH: config
// lookups are precomputed once in the constructor — every id is expanded to base + the three
// LOTRLOME settlement variants at construction (CrushThroughService pattern), so the per-call
// path is a bare HashSet/Dictionary lookup with zero allocation. Settings getters are read per
// call because MCM toggles change mid-session. No TaleWorlds types (ADR-007).
public class CreatureCombatService : ICreatureCombatService
{
    private static readonly string[] SettlementSuffixes =
    {
        // Longer suffixes first so "_settlement_fast" never strips to a dangling "_fast".
        "_settlement_fast",
        "_settlement_slow",
        "_settlement",
    };

    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly IRaceCombatModifiersResolver _raceModifiers;
    private readonly HashSet<string> _cleaveMonsterIds;
    private readonly Dictionary<string, int> _unstoppableDamageThresholds;
    private readonly float _cleaveRemainingMomentumFactor;

    public CreatureCombatService(
        ICombatMechanicsConfigProvider configProvider,
        ICombatMechanicsSettingsProvider settings,
        IRaceCombatModifiersResolver raceModifiers)
    {
        _settings = settings;
        _raceModifiers = raceModifiers;

        var creatures = configProvider.GetConfig().Creatures ?? new CreatureCombatConfig();

        // Config entries are normalized to the base id first (a suffixed entry like
        // "cave_troll_settlement" means the same creature), then expanded to all variants.
        _cleaveMonsterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in creatures.CleaveMonsterIds ?? new List<string>())
        {
            var baseId = NormalizeMonsterId(id);
            if (baseId == null) continue;
            _cleaveMonsterIds.Add(baseId);
            foreach (var suffix in SettlementSuffixes)
                _cleaveMonsterIds.Add(baseId + suffix);
        }

        _unstoppableDamageThresholds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in creatures.UnstoppableDamageThresholds ?? new Dictionary<string, int>())
        {
            var baseId = NormalizeMonsterId(pair.Key);
            if (baseId == null) continue;
            _unstoppableDamageThresholds[baseId] = pair.Value;
            foreach (var suffix in SettlementSuffixes)
                _unstoppableDamageThresholds[baseId + suffix] = pair.Value;
        }

        _cleaveRemainingMomentumFactor = creatures.CleaveRemainingMomentumFactor;
    }

    public float? CalculateCleaveMomentum(string attackerMonsterId, float originalMomentum, bool isColliderAgent)
    {
        // NaN guard: a NaN momentum must never replace the base model's value — NaN × factor is
        // NaN, which would then defeat the > 0f cooperation guard downstream.
        if (!_settings.CreatureCleaveEnabled || !isColliderAgent || float.IsNaN(originalMomentum))
            return null;

        if (attackerMonsterId == null || !_cleaveMonsterIds.Contains(attackerMonsterId))
            return null;

        return originalMomentum * _cleaveRemainingMomentumFactor;
    }

    public bool ShouldForceSliceThrough(string attackerMonsterId, float momentumRemaining, bool isColliderAgent, int inflictedDamage)
    {
        // Cooperation guard as a POSITIVE requirement (momentum must be > 0f): written this way
        // so NaN fails the gate — the `<= 0f` form lets NaN through (NaN comparisons are all
        // false), the NaN-gate bug class that has shipped three times before (csharp-architecture
        // "Config Providers MUST Validate" rule 4). Scalar guards run before any string work.
        if (!_settings.CreatureCleaveEnabled || !isColliderAgent || !(momentumRemaining > 0f) || inflictedDamage <= 0)
            return false;

        return attackerMonsterId != null && _cleaveMonsterIds.Contains(attackerMonsterId);
    }

    public bool IsUnstoppable(string victimMonsterId, int inflictedDamage)
    {
        if (!_settings.CreatureUnstoppableEnabled || victimMonsterId == null)
            return false;

        return _unstoppableDamageThresholds.TryGetValue(victimMonsterId, out var threshold)
            && inflictedDamage <= threshold;
    }

    public float ApplyStaggerThresholdMultiplier(int? victimRaceId, float baseThreshold)
    {
        // Resolver returns Neutral (multiplier 1) for disabled/invalid/unknown — no extra gating.
        return baseThreshold * _raceModifiers.Resolve(victimRaceId).StaggerThresholdMultiplier;
    }

    // Construction-time only (the per-call path uses the pre-expanded lookups).
    private static string NormalizeMonsterId(string monsterId)
    {
        if (string.IsNullOrEmpty(monsterId))
            return null;

        foreach (var suffix in SettlementSuffixes)
        {
            if (monsterId.EndsWith(suffix, StringComparison.Ordinal))
                return monsterId.Substring(0, monsterId.Length - suffix.Length);
        }

        return monsterId;
    }
}
