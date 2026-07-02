using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Per-hit hot path (TaomCombatMechanicsModel.DecideCrushedThrough → here): all config lookups are
// precomputed in the constructor; only the MCM-backed settings getters (toggles + max-chance
// slider, which change mid-session) are read per call. No LINQ, no allocation per call.
// Mechanics 1-3 of docs/reviews/adopt-tor-combat-mechanics-2026-07-02.md (the normative spec).
public class CrushThroughService : ICrushThroughService
{
    // The engine appends these to Monster.StringId for settlement variants. Membership is
    // precomputed as base id + each suffixed form so the per-call check is one hash lookup —
    // behaviorally identical to "strip the trailing suffix (longest first), then look up" for
    // these three suffixes, without the per-hit Substring allocation.
    private static readonly string[] SettlementSuffixes =
    {
        "_settlement_fast",
        "_settlement_slow",
        "_settlement",
    };

    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly IRaceCombatModifiersResolver _resolver;
    private readonly IRaceManager _raceManager;
    private readonly CrushThroughConfig _config;
    private readonly HashSet<string> _monsterCrushIds;
    private readonly HashSet<string> _orcShieldCrushRaces;

    // 1 − e^(−(targetDelta − deadZone) × exponent), fixed at construction (the config singleton
    // never changes mid-process). Uses the same float shape as the per-call numerator so
    // delta == targetDelta yields a ratio of exactly 1.0. Non-positive = misconfigured curve —
    // the skill path disables itself (warned once below, never per hit).
    private readonly double _curveDenominator;

    public CrushThroughService(
        ICombatMechanicsConfigProvider configProvider,
        ICombatMechanicsSettingsProvider settings,
        IRaceCombatModifiersResolver resolver,
        IRaceManager raceManager,
        IModLogger logger)
    {
        _settings = settings;
        _resolver = resolver;
        _raceManager = raceManager;
        _config = configProvider.GetConfig().CrushThrough;

        _monsterCrushIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var monsterId in _config.MonsterCrushMonsterIds)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                logger.LogWarning("CrushThroughService: empty monsterCrushMonsterIds entry skipped");
                continue;
            }

            _monsterCrushIds.Add(monsterId);
            foreach (var suffix in SettlementSuffixes)
                _monsterCrushIds.Add(monsterId + suffix);
        }

        _orcShieldCrushRaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raceName in _config.OrcShieldCrushRaces)
        {
            if (string.IsNullOrEmpty(raceName))
            {
                logger.LogWarning("CrushThroughService: empty orcShieldCrushRaces entry skipped");
                continue;
            }

            _orcShieldCrushRaces.Add(raceName);
        }

        float curveSpan = _config.SkillTargetDelta - _config.SkillDeadZone;
        float denominatorArg = curveSpan * _config.SkillCurveExponent;
        _curveDenominator = 1.0 - Math.Exp(-denominatorArg);
        if (_curveDenominator <= 0.0)
            logger.LogWarning("CrushThroughService: skill curve denominator is non-positive (skillTargetDelta must exceed skillDeadZone with a positive skillCurveExponent) — skill-based crush-through is disabled");
    }

    public bool? DecideCrushThrough(in CrushThroughContext context)
    {
        // Mechanic 2 — monster auto-CTB: a listed monster crushes any non-shield block outright
        // (no swing/skill eligibility); a shield block falls through to the orc/skill paths.
        if (_settings.MonsterCrushThroughEnabled
            && context.AttackerMonsterId != null
            && _monsterCrushIds.Contains(context.AttackerMonsterId)
            && !context.DefendItemIsShield)
        {
            return true;
        }

        // Mechanics 1 + 3 share the swing eligibility gate.
        if (!context.HasMeleeWeapon || context.IsPassiveUsage || !context.IsSwing)
            return null;

        bool orcQualified = _settings.OrcShieldCrushEnabled
            && context.IsAiControlled
            && IsOrcShieldCrushRace(context.AttackerRaceId);

        if (!orcQualified && !_settings.SkillCrushThroughEnabled)
            return null;

        // Shield blocks are excluded from the plain skill path (mechanic 1); only an
        // orc-qualified attack (mechanic 3) may crush through a shield block.
        if (context.DefendItemIsShield && !orcQualified)
            return null;

        return EvaluateSkillCurve(in context, orcQualified);
    }

    // The one shared curve for mechanics 1 + 3 — the orc path differs only by the shield gate
    // (handled by the caller) and by skipping the non-overhead penalty.
    private bool? EvaluateSkillCurve(in CrushThroughContext context, bool orcQualified)
    {
        var attackerMods = _resolver.Resolve(context.AttackerRaceId);
        var defenderMods = _resolver.Resolve(context.DefenderRaceId);

        float threshold = _config.ExtraCrushThroughEnergyThreshold;
        float effectiveEnergy = context.TotalAttackEnergy * (1f + attackerMods.SwingEnergyBonusFactor);
        if (effectiveEnergy <= threshold)
            return null;

        float delta = context.AttackerWeaponSkill - context.DefenderWeaponSkill
            + attackerMods.CtbAttackBonus - defenderMods.CtbDefenseBonus;
        if (delta <= _config.SkillDeadZone)
            return null;

        if (_curveDenominator <= 0.0)
            return null;

        float numeratorArg = (delta - _config.SkillDeadZone) * _config.SkillCurveExponent;
        double normalized = (1.0 - Math.Exp(-numeratorArg)) / _curveDenominator;

        // Max chance comes from the SETTINGS provider (MCM slider over JSON), not the config.
        float chance = _settings.CrushThroughMaxChance * Clamp01((float)normalized);
        chance *= Clamp01((effectiveEnergy - threshold) / (threshold * _config.EnergyRampMarginFactor));

        if (!context.IsOverhead && !orcQualified && !attackerMods.RemoveNonOverheadPenalty)
            chance *= _config.NonOverheadPenaltyFactor;

        chance = Clamp01(chance);

        // Strict < : a roll equal to the chance does NOT crush. Null (never false) on a miss —
        // TAOM only ADDS crush-throughs; the engine's base 58f path still gets its say.
        return context.RandomRoll < chance ? true : (bool?)null;
    }

    private bool IsOrcShieldCrushRace(int? raceId)
    {
        // Validate BEFORE the name lookup — GetRaceNameFromId coerces unknown ids to "human"
        // (csharp-architecture.md "Lookup Functions With Fallbacks"); the fallback must never
        // qualify an attacker for shield-crushing.
        if (!raceId.HasValue || !_raceManager.IsValidRaceId(raceId.Value))
            return false;

        var raceName = _raceManager.GetRaceNameFromId(raceId.Value);
        return raceName != null && _orcShieldCrushRaces.Contains(raceName);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;
        return value > 1f ? 1f : value;
    }
}
