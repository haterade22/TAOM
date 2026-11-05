using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// Folds the run's modifiers into the numbers the engine and the run read (phase2-plan.md section 1): a wave's affixes into the table the engine's hooks read
/// (the stat powers into a <see cref="RaceAbilityEffects"/> record, <c>hide</c> into an armour percent), and the boons owned into the effective rules, the gold factors, the affix cut,
/// and for each defender group and the lord a stat record, two damage factors and a health factor. Pure.
/// </summary>
internal static class RunModifiers
{
    /// <summary>
    /// The table for a wave with these affixes and this attacker damage factor, and the troop and class boons <paramref name="owned"/>: a new record on every call so a table
    /// the engine's threads already hold is never mutated.
    /// </summary>
    public static LdStatTable StatTable(AffixPlan plan, IReadOnlyList<string> affixes, float attackerDamage, IReadOnlyCollection<BoonRow>? owned = null)
    {
        var fx = new RaceAbilityEffects();
        var armour = 0f;
        var stats = false;
        foreach (var id in affixes)
        {
            switch (id)
            {
                case AffixPlan.Lash:
                    fx.SwingSpeedPercent += plan.Value(id, "swingSpeedPercent");
                    fx.AttackEagernessPercent += plan.Value(id, "attackEagernessPercent");
                    stats = true;
                    break;
                case AffixPlan.Drums:
                    fx.MoveSpeedPercent += plan.Value(id, "moveSpeedPercent");
                    fx.AccelerationPercent += plan.Value(id, "accelerationPercent");
                    stats = true;
                    break;
                case AffixPlan.Eye:
                    fx.AimErrorPercent += plan.Value(id, "aimErrorPercent");
                    fx.DrawSpeedPercent += plan.Value(id, "drawSpeedPercent");
                    fx.ReloadSpeedPercent += plan.Value(id, "reloadSpeedPercent");
                    fx.MissileSpeedPercent += plan.Value(id, "missileSpeedPercent");
                    stats = true;
                    break;
                case AffixPlan.Discipline:
                    fx.BlockAbilityPercent += plan.Value(id, "blockAbilityPercent");
                    fx.ParryAbilityPercent += plan.Value(id, "parryAbilityPercent");
                    stats = true;
                    break;
                case AffixPlan.Hide:
                    armour += plan.Value(id, "armourPercent");
                    break;
            }
        }

        fx.MountSpeedPercent = fx.MoveSpeedPercent;
        var boons = owned ?? Array.Empty<BoonRow>();
        return new LdStatTable(attackerDamage, stats ? fx : null, armour)
        {
            Infantry = TargetStats(boons, BoonTarget.Infantry),
            Archers = TargetStats(boons, BoonTarget.Archers),
            Cavalry = TargetStats(boons, BoonTarget.Cavalry),
            Player = TargetStats(boons, BoonTarget.Player),
        };
    }

    /// <summary>
    /// The health factor in force for a target: the product of each boon's own factor, so a standing soldier scaled once by every boon's own factor (<see cref="OwnHealthFactor"/>)
    /// holds what a soldier spawned later gets. It is a product, not a sum, because the second boon scales soldiers the first already scaled.
    /// </summary>
    public static float HealthFactor(IReadOnlyCollection<BoonRow> owned, BoonTarget target) =>
        owned.Aggregate(1f, (factor, boon) => factor * OwnHealthFactor(boon, target));

    /// <summary>What one boon, drawbacks included, multiplies the health of a target by: 1 for a boon that does not touch it.</summary>
    public static float OwnHealthFactor(BoonRow boon, BoonTarget target) =>
        Aimed(boon, BoonKeys.HealthPercent, target).Aggregate(1f, (factor, e) => factor * (1f + e.Value / 100f));

    /// <summary>The targets a boon's stat keys change: their standing agents must be refreshed, since a driven property is derived once and held.</summary>
    public static IReadOnlyList<BoonTarget> StatTargets(BoonRow boon) =>
        BoonTargets.All.Where(t => BoonKeys.StatKeys.Any(key => Aimed(boon, key, t).Any())).ToList();

    /// <summary>The targets a boon's health keys change: their standing agents are scaled by <see cref="OwnHealthFactor"/>.</summary>
    public static IReadOnlyList<BoonTarget> HealthTargets(BoonRow boon) =>
        BoonTargets.All.Where(t => Aimed(boon, BoonKeys.HealthPercent, t).Any()).ToList();

    private static LdTargetStats TargetStats(IReadOnlyCollection<BoonRow> owned, BoonTarget target)
    {
        var fx = new RaceAbilityEffects
        {
            SwingSpeedPercent = SumFor(owned, BoonKeys.SwingSpeedPercent, target),
            MoveSpeedPercent = SumFor(owned, BoonKeys.MoveSpeedPercent, target),
            DrawSpeedPercent = SumFor(owned, BoonKeys.DrawSpeedPercent, target),
            ReloadSpeedPercent = SumFor(owned, BoonKeys.ReloadSpeedPercent, target),
            BlockAbilityPercent = SumFor(owned, BoonKeys.BlockPercent, target),
            ParryAbilityPercent = SumFor(owned, BoonKeys.ParryPercent, target),
            AimErrorPercent = SumFor(owned, BoonKeys.AimErrorPercent, target),
        };
        if (target is BoonTarget.Cavalry or BoonTarget.Player)
            fx.MountSpeedPercent = fx.MoveSpeedPercent;

        var any = owned.Any(b => BoonKeys.StatKeys.Any(key => Aimed(b, key, target).Any()));
        return new LdTargetStats(any ? fx : null,
            Math.Max(0f, 1f + SumFor(owned, BoonKeys.DamagePercent, target) / 100f),
            Math.Max(0f, 1f + SumFor(owned, BoonKeys.DamageTakenPercent, target) / 100f));
    }

    private static IEnumerable<BoonEffect> Aimed(BoonRow boon, string key, BoonTarget target) =>
        boon.Effects.Concat(boon.Drawback).Where(e => e.Key == key && e.Target == target);

    private static float SumFor(IReadOnlyCollection<BoonRow> owned, string key, BoonTarget target) =>
        owned.Sum(b => Aimed(b, key, target).Sum(e => e.Value));

    /// <summary>The shipped rules with the fortress boons owned folded in. A drawback adds like an effect. The floors keep a shop open (5 s), a garrison (1) and a share a share (0 to 1).</summary>
    public static RunRules Rules(RunRules shipped, IReadOnlyCollection<BoonRow> owned)
    {
        if (owned.Count == 0)
            return shipped;

        return shipped with
        {
            GarrisonCap = Math.Max(1, shipped.GarrisonCap + (int)Sum(owned, BoonKeys.GarrisonCap)),
            IntermissionSeconds = Math.Max(MinSeconds, shipped.IntermissionSeconds + Sum(owned, BoonKeys.IntermissionSeconds)),
            RestShare = Math.Max(0f, Math.Min(1f, shipped.RestShare + Sum(owned, BoonKeys.RestShare))),
            KeepHoldSeconds = Math.Max(MinSeconds, shipped.KeepHoldSeconds * Factor(owned, BoonKeys.KeepHoldPercent)),
            PackBonus = shipped.PackBonus + (int)Sum(owned, BoonKeys.PackBonus),
        };
    }

    /// <summary>The war chest percents of the boons owned, each summed into one factor (1 plus the sum over 100, never below 0).</summary>
    public static GoldFactors Gold(IReadOnlyCollection<BoonRow> owned) => owned.Count == 0
        ? GoldFactors.One
        : new GoldFactors(Factor(owned, BoonKeys.BountyPercent), Factor(owned, BoonKeys.WaveBonusPercent),
            Factor(owned, BoonKeys.EarlyGoldPercent), Factor(owned, BoonKeys.PricePercent));

    /// <summary>The gold the boons owned paid at their picks, in all.</summary>
    public static int GoldPaid(IReadOnlyCollection<BoonRow> owned) => (int)owned.Sum(b => b.Effects.Where(e => e.Key == BoonKeys.Gold).Sum(e => e.Value));

    /// <summary>The affix count the boons owned add to every later wave's schedule: zero or negative.</summary>
    public static int AffixCut(IReadOnlyCollection<BoonRow> owned) => (int)Sum(owned, BoonKeys.AffixCount);

    private const float MinSeconds = 5f;

    private static float Sum(IReadOnlyCollection<BoonRow> owned, string key) =>
        owned.Sum(b => b.Effects.Concat(b.Drawback).Where(e => e.Key == key).Sum(e => e.Value));

    private static float Factor(IReadOnlyCollection<BoonRow> owned, string key) => Math.Max(0f, 1f + Sum(owned, key) / 100f);
}
