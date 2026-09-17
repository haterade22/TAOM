using System;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// Test-side reproduction of the nine vanilla <c>GetTacticWeight</c> formulas (v1.5.3 dump,
/// re-read 2026-09-16) at their NOMINAL scene scores: no casualties, every scene-position score
/// at 1, the high ground within 50 m. It exists so the shipped doctrine multipliers can be
/// checked against the weights they compete with, machine-readably, instead of by eye.
///
/// <list type="bullet">
///   <item><c>TacticCharge.cs:59-88</c>: with no casualties the first term is 0 and the weight is
///   <c>LinearExtrapolation(0, 1.6 * num2, RemainingPowerRatio * num3 * 0.5)</c> with
///   <c>num2 = max(Inf, Rng, Cav) + (defender ? 0.33 : 0)</c>, <c>num3 = defender ? 0.33 : 0.5</c>.</item>
///   <item><c>TacticFullScaleAttack.cs:179-183</c>: <c>Inf * M / (M - RC * M) * sqrt(p)</c>.</item>
///   <item><c>TacticFrontalCavalryCharge.cs:175-179</c>: <c>Cav * M / (M - RC * M) * sqrt(p)</c>.</item>
///   <item><c>TacticRangedHarrassmentOffensive.cs:179-191</c>: <c>Rng * M / (M - RC * M) * clamp(ownRanged / enemyRanged, 0.5, 2) * sqrt(p)</c>.</item>
///   <item><c>TacticDefensiveEngagement.cs:173-192</c>: <c>(Inf + Rng) * 1.1 * adv * num2 / sqrt(p)</c>, num2 = 1 within 50 m of the high ground; 0 for an attacker.</item>
///   <item><c>TacticDefensiveLine.cs:204</c>: <c>(Inf + Rng) * 1.2 * score * adv / sqrt(p)</c>, score 1; 0 for an attacker or without a scene position.</item>
///   <item><c>TacticDefensiveRing.cs:115-137</c>: <c>min(Inf, Rng) * 3 * score * adv / sqrt(p)</c>, score 1; 0 for an attacker or without an insurmountable position.</item>
///   <item><c>TacticHoldChokePoint.cs:114</c>: <c>(Inf + min(Inf, Rng)) * clamp(E / M, 0.33, 3) * score * adv * 1.3 / sqrt(p)</c>, score 1; 0 for an attacker or without a choke point.</item>
///   <item><c>TacticCoordinatedRetreat.cs:159</c>: <c>LinearExtrapolation(0, max ratio, clamp(Total / Remaining, 0, 4) / 2) * num4 * min(1, sqrt(p))</c>, num4 = 1 at cavalry parity; attacker only.</item>
/// </list>
/// The scene-dependent tactics are evaluated at score 1 (a real scene position) AND at 0 (no
/// entity placed); callers take whichever case they want to assert against.
/// </summary>
public static class VanillaTacticWeightReference
{
    /// <summary>The vanilla weight for one tactic at the snapshot, with scene positions scored
    /// at <paramref name="sceneScore"/> (1 = a nominal entity exists, 0 = none placed).</summary>
    public static float Weight(DoctrineTactic tactic, in TeamQuerySnapshot s, float sceneScore)
    {
        var members = Math.Max(1f, s.MemberCount);
        var nonHorseArchers = Math.Max(1f, members - s.RangedCavalryRatio * members);
        var root = (float)Math.Sqrt(Math.Max(0.01f, s.RemainingPowerRatio));
        var adv = s.NotEngagingAdvantage;
        var defender = s.IsDefender;
        switch (tactic)
        {
            case DoctrineTactic.Charge:
            {
                var num2 = Math.Max(s.InfantryRatio, Math.Max(s.RangedRatio, s.CavalryRatio)) + (defender ? 0.33f : 0f);
                var num3 = defender ? 0.33f : 0.5f;
                return 1.6f * num2 * (s.RemainingPowerRatio * num3 * 0.5f);
            }
            case DoctrineTactic.FullScaleAttack:
                return s.InfantryRatio * members / nonHorseArchers * root;
            case DoctrineTactic.FrontalCavalryCharge:
                return s.CavalryRatio * members / nonHorseArchers * root;
            case DoctrineTactic.RangedHarrassmentOffensive:
            {
                if (!s.HasArchers) return 0f;
                // Enemy ranged share unknown to the snapshot: parity assumed (clamp term 1).
                return s.RangedRatio * members / nonHorseArchers * 1f * root;
            }
            case DoctrineTactic.DefensiveEngagement:
                return defender && s.HasInfantry ? (s.InfantryRatio + s.RangedRatio) * 1.1f * adv / root : 0f;
            case DoctrineTactic.DefensiveLine:
                return defender ? (s.InfantryRatio + s.RangedRatio) * 1.2f * sceneScore * adv / root : 0f;
            case DoctrineTactic.DefensiveRing:
                return defender && s.HasInfantry && s.HasArchers && s.RingFits
                    ? Math.Min(s.InfantryRatio, s.RangedRatio) * 3f * sceneScore * adv / root
                    : 0f;
            case DoctrineTactic.HoldChokePoint:
            {
                if (!defender) return 0f;
                var numbers = Clamp(s.EnemyUnitCount / members, 0.33f, 3f);
                return (s.InfantryRatio + Math.Min(s.InfantryRatio, s.RangedRatio)) * numbers * sceneScore * adv * 1.3f / root;
            }
            case DoctrineTactic.CoordinatedRetreat:
            {
                if (defender) return 0f;
                var maxRatio = Math.Max(s.InfantryRatio, Math.Max(s.RangedRatio, s.CavalryRatio));
                // Total / Remaining power is 1 with no casualties: t = 0.5.
                return maxRatio * 0.5f * 1f * Math.Min(1f, root);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(tactic), tactic, "not a vanilla tactic");
        }
    }

    /// <summary>The TAOM tactic's own weight at the snapshot, through the shipped functions.</summary>
    public static float TaomWeight(DoctrineTactic tactic, in TeamQuerySnapshot s)
    {
        switch (tactic)
        {
            case DoctrineTactic.ShieldWall: return DoctrineWeights.ShieldWall(in s);
            case DoctrineTactic.InfantryMass: return DoctrineWeights.InfantryMass(in s);
            case DoctrineTactic.CavalryDominance: return DoctrineWeights.CavalryDominance(in s);
            case DoctrineTactic.ArcherRing: return DoctrineWeights.ArcherRing(in s);
            case DoctrineTactic.TwoLineWall: return DoctrineWeights.TwoLineWall(in s);
            case DoctrineTactic.Envelop: return DoctrineWeights.Envelop(in s);
            case DoctrineTactic.DisciplinedLine: return DoctrineWeights.DisciplinedLine(in s);
            case DoctrineTactic.ArcherAdvance: return DoctrineWeights.ArcherAdvance(in s);
            case DoctrineTactic.EoredScreen: return DoctrineWeights.EoredScreen(in s);
            case DoctrineTactic.HitAndRun: return DoctrineWeights.HitAndRun(in s);
            case DoctrineTactic.MumakVanguard: return DoctrineWeights.MumakVanguard(in s);
            default: throw new ArgumentOutOfRangeException(nameof(tactic), tactic, "not a TAOM tactic");
        }
    }

    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
}
