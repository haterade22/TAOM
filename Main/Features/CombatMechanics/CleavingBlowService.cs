using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Per-hit hot path (TaomCombatMechanicsModel.CalculateRemainingMomentum and DecideWeaponCollisionReaction, through
// CombatMechanicsHooks): the config is read once in the constructor; only the three MCM-backed getters are read per call.
// No LINQ, no allocation per call beyond one chain per attacker.
//
// The rule: carry = classFactor x (1 - armourShare) + killBonus + skillBonus x skillCurve, capped at maxCarryFactor; the next
// momentum is the original times that carry, 0 below the floor or once the swing has struck maxTargets enemies.
public sealed class CleavingBlowService : ICleavingBlowService
{
    // Assumed, not verified (combat-mechanics.md UNVERIFIED 1): one per attacker object. The engine hands back the momentum we returned, so a hit whose incoming momentum equals the
    // last value we carried is the next hit of the same swing; a fresh swing starts at 1.0, which a carried value never is.
    private sealed class Chain
    {
        public float LastCarried;
        public int Hits;
    }

    private static readonly ConditionalWeakTable<object, Chain>.CreateValueCallback NewChain = _ => new Chain();

    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly Dictionary<string, float> _classCarry;
    private readonly HashSet<string> _aiClasses;
    private readonly float _killCarryBonus;
    private readonly float _skillCarryBonus;
    private readonly float _maxCarryFactor;
    private readonly float _minRemainingMomentum;
    private readonly bool _includeMounted;
    private readonly int _skillDeadZone;
    private readonly float _skillCurveExponent;
    private readonly double _skillCurveDenominator;

    // Keyed on the attacker's object, never on an agent index: a deleted agent's index goes to whoever spawns next. Entries die
    // with their agent, so nothing clears between missions.
    private readonly ConditionalWeakTable<object, Chain> _chains = new ConditionalWeakTable<object, Chain>();

    public CleavingBlowService(ICombatMechanicsConfigProvider configProvider, ICombatMechanicsSettingsProvider settings)
    {
        _settings = settings;
        var config = configProvider.GetConfig();
        var cleave = config.CleavingBlows;

        _classCarry = new Dictionary<string, float>(cleave.WeaponClassCarry, StringComparer.Ordinal);
        _aiClasses = new HashSet<string>(cleave.AiWeaponClasses, StringComparer.Ordinal);
        _killCarryBonus = cleave.KillCarryBonus;
        _skillCarryBonus = cleave.SkillCarryBonus;
        _maxCarryFactor = cleave.MaxCarryFactor;
        _minRemainingMomentum = cleave.MinRemainingMomentum;
        _includeMounted = cleave.IncludeMountedAttackers;

        // The skill window is crush-through's: one definition of "clearly the better fighter".
        var crush = config.CrushThrough;
        _skillDeadZone = crush.SkillDeadZone;
        _skillCurveExponent = crush.SkillCurveExponent;
        _skillCurveDenominator = SkillGapCurve.Denominator(crush.SkillDeadZone, crush.SkillTargetDelta, crush.SkillCurveExponent);
    }

    public float? CalculateMomentum(in CleavingBlowContext context, float originalMomentum)
    {
        // Positive requirements: NaN fails them. Infinity would poison the products below.
        if (!(originalMomentum > 0f) || float.IsInfinity(originalMomentum))
            return null;
        if (!TryQualify(in context, out float classFactor))
            return null;

        float absorbed = context.AbsorbedByArmor;
        float inflicted = context.InflictedDamage;
        float armourShare = absorbed / (absorbed + inflicted);

        float carry = classFactor * (1f - armourShare);
        if (context.IsFatal)
            carry += _killCarryBonus;
        // On a kill the victim's weapon may already be dropped, so his skill would read 0 and hand every kill the full skill term:
        // the kill bonus covers kills.
        else
        {
            carry += _skillCarryBonus * SkillGapCurve.Normalized(
                (float)context.AttackerWeaponSkill - context.VictimWeaponSkill,
                _skillDeadZone,
                _skillCurveExponent,
                _skillCurveDenominator);
        }
        if (carry > _maxCarryFactor)
            carry = _maxCarryFactor;

        float next = originalMomentum * carry;
        if (!(next >= _minRemainingMomentum))
            next = 0f;

        int maxTargets = _settings.CleavingBlowsMaxTargets;
        var chain = _chains.GetValue(context.AttackerIdentity!, NewChain);
        lock (chain)
        {
            int hit = chain.LastCarried > 0f && originalMomentum == chain.LastCarried ? chain.Hits + 1 : 1;
            if (hit >= maxTargets)
                next = 0f;

            chain.Hits = hit;
            chain.LastCarried = next;
        }

        return next;
    }

    public bool ShouldSliceThrough(in CleavingBlowContext context, float momentumRemaining)
        => momentumRemaining > 0f && TryQualify(in context, out _);

    // Scalar gates first, then who swings and the settings, then the weapon class. Each gate is a positive requirement, so a
    // NaN reads as a failure.
    private bool TryQualify(in CleavingBlowContext context, out float classFactor)
    {
        classFactor = 0f;

        if (context.AttackerIdentity == null
            || !context.IsSwing
            || context.IsAlternativeAttack
            || context.IsPassiveAttack
            || context.IsHorseCharge
            || !context.IsColliderAgent
            || context.BlockedByShield
            || context.HitWithAnotherBone
            || context.VictimShruggedOff
            || context.InflictedDamage <= 0
            || !(context.AbsorbedByArmor >= 0f)
            || float.IsInfinity(context.AbsorbedByArmor)
            || !context.AttackerIsHuman
            || (context.AttackerIsMounted && !_includeMounted))
        {
            return false;
        }

        var className = context.WeaponClassName;
        if (className == null)
            return false;

        switch (context.AttackerKind)
        {
            case CleaveAttackerKind.Player:
                if (!_settings.CleavingBlowsEnabled)
                    return false;
                break;
            case CleaveAttackerKind.Ai:
                if (!_settings.CleavingBlowsAiEnabled || !_aiClasses.Contains(className))
                    return false;
                break;
            default:
                return false;
        }

        return _classCarry.TryGetValue(className, out classFactor);
    }
}
