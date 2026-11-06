using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>How a percentage acts on a driven property: a multiple, a time divided by it, or a probability multiplied and held within 0 to 1.</summary>
internal enum AffixMath
{
    Times,
    Over,
    Chance,
}

/// <summary>One driven property an affix is expected to change, and by what percent.</summary>
internal readonly record struct AffixExpect(string Stat, AffixMath Math, float Percent);

/// <summary>
/// The <c>affix</c> and <c>affix-effect</c> verdicts of #1079 (phase2-plan.md section 6). Pure: the director reads the roll and the engine, these turn
/// the numbers into pass, fail or skip. The arithmetic is written out here on purpose, apart from <c>RaceAbilityService</c>, so a check that shared it
/// could not catch a change in it. The stat powers and <c>hide</c> are read back from a sampled attacker (<see cref="Effect"/>); the Hammer of the Underworld and Winged Terror have their own verdicts (<see cref="Hammer"/>, <see cref="Terror"/>), because their effect is on the engines and on the defenders' morale, not on an attacker's driven properties; Morgul Blades joins with its own slice.
/// </summary>
internal static class LongDefeatAffixChecks
{
    private const float RelativeTolerance = 0.01f;
    private const float AbsoluteTolerance = 1e-4f;

    /// <summary>
    /// The wave's roll matches the schedule: as many powers as the schedule gives the wave, less what the Legend boons owned cut (<paramref name="cut"/>, zero or
    /// negative, never below none), each a known power, none twice, and a name for each in the attacker culture's table.
    /// </summary>
    internal static CheckVerdict Affix(AffixPlan plan, int wave, IReadOnlyList<string> rolled, int cut = 0)
    {
        var scheduled = Math.Max(0, plan.CountFor(wave) + cut);
        var known = rolled.All(AffixPlan.Ids.Contains);
        var distinct = rolled.Distinct().Count() == rolled.Count;
        var unnamed = rolled.Where(id => plan.NameOf(id) == null).ToList();
        var ok = rolled.Count == scheduled && known && distinct && unnamed.Count == 0;
        return LongDefeatChecks.Verdict(ok, $"w={Num(wave)} count={Num(rolled.Count)} scheduled={Num(scheduled)} powers={string.Join(",", rolled)} "
            + $"known={Flag(known)} distinct={Flag(distinct)} unnamed={(unnamed.Count == 0 ? "none" : string.Join(",", unnamed))} cut={Num(cut)}");
    }

    /// <summary>The driven properties a power is expected to change, with the arithmetic the engine's applier uses. Empty for a power with no stat effect (yet).</summary>
    internal static IReadOnlyList<AffixExpect> Expectations(AffixPlan plan, string power)
    {
        switch (power)
        {
            case AffixPlan.Lash:
                return new[]
                {
                    new AffixExpect("SwingSpeedMultiplier", AffixMath.Times, plan.Value(power, "swingSpeedPercent")),
                    new AffixExpect("AIAttackOnDecideChance", AffixMath.Chance, plan.Value(power, "attackEagernessPercent")),
                };
            case AffixPlan.Drums:
                return new[]
                {
                    new AffixExpect("MaxSpeedMultiplier", AffixMath.Times, plan.Value(power, "moveSpeedPercent")),
                    new AffixExpect("CombatMaxSpeedMultiplier", AffixMath.Times, plan.Value(power, "moveSpeedPercent")),
                    new AffixExpect("TopSpeedReachDuration", AffixMath.Over, plan.Value(power, "accelerationPercent")),
                };
            case AffixPlan.Eye:
                return new[]
                {
                    new AffixExpect("AiShooterError", AffixMath.Times, plan.Value(power, "aimErrorPercent")),
                    new AffixExpect("ThrustOrRangedReadySpeedMultiplier", AffixMath.Times, plan.Value(power, "drawSpeedPercent")),
                    new AffixExpect("ReloadSpeed", AffixMath.Times, plan.Value(power, "reloadSpeedPercent")),
                    new AffixExpect("MissileSpeedMultiplier", AffixMath.Times, plan.Value(power, "missileSpeedPercent")),
                };
            case AffixPlan.Discipline:
                return new[]
                {
                    new AffixExpect("AIBlockOnDecideAbility", AffixMath.Chance, plan.Value(power, "blockAbilityPercent")),
                    new AffixExpect("AIParryOnDecideAbility", AffixMath.Chance, plan.Value(power, "parryAbilityPercent")),
                };
            case AffixPlan.Hide:
                var armour = plan.Value(power, "armourPercent");
                return new[]
                {
                    new AffixExpect("ArmorHead", AffixMath.Times, armour),
                    new AffixExpect("ArmorTorso", AffixMath.Times, armour),
                    new AffixExpect("ArmorLegs", AffixMath.Times, armour),
                    new AffixExpect("ArmorArms", AffixMath.Times, armour),
                };
            case AffixPlan.Hammer:
            case AffixPlan.Terror:
                // The row exists so the two are decided here, not forgotten: neither changes a driven property of an attacker, so the stat read has nothing to ask of them and a wave
                // with only these reads no soldier. Each is proven by its own verdict (Hammer, Terror) from the engines and the pulses, recorded under the same affix-effect id.
                return Array.Empty<AffixExpect>();
            default:
                return Array.Empty<AffixExpect>();
        }
    }

    /// <summary>
    /// A sampled attacker with no live race ability reads as its baseline changed by the power's percentages, within 1%. A sample that could not show the
    /// effect (every baseline at the expected value already: zero armour, a zero percentage) is a skip, so it never reads as a pass it did not earn.
    /// </summary>
    internal static CheckVerdict Effect(string power, IReadOnlyList<AffixExpect> expected, LdStatSample sample)
    {
        if (!sample.HasAgent)
            return new CheckVerdict(CheckResult.Fail, $"power={power} reason=no-sample");

        var compared = Compare(expected, sample);
        var detail = $"power={power} troop={sample.TroopId} {string.Join("; ", compared.Parts)}";
        if (!compared.Ok)
            return new CheckVerdict(CheckResult.Fail, detail);

        return compared.Informative ? new CheckVerdict(CheckResult.Pass, detail) : new CheckVerdict(CheckResult.Skip, detail + " reason=no-signal");
    }

    /// <summary>What a read-back found against what was expected: every property within 1%, whether any of them could have shown a change, and a text for each.</summary>
    internal readonly record struct StatComparison(bool Ok, bool Informative, IReadOnlyList<string> Parts);

    /// <summary>Each expected property of the sample against its baseline changed by the expected percent. A property the sample lacks is wrong.</summary>
    internal static StatComparison Compare(IReadOnlyList<AffixExpect> expected, LdStatSample sample)
    {
        var ok = true;
        var informative = false;
        var parts = new List<string>();
        foreach (var e in expected)
        {
            var reading = sample.Readings.Where(x => x.Stat == e.Stat).Cast<LdStatReading?>().FirstOrDefault();
            if (reading is not { } r)
            {
                ok = false;
                parts.Add($"{e.Stat}=missing");
                continue;
            }

            var want = Expected(r.Base, e);
            var near = Math.Abs(r.Actual - want) <= RelativeTolerance * Math.Abs(want) + AbsoluteTolerance;
            ok &= near;
            informative |= Math.Abs(want - r.Base) > AbsoluteTolerance;
            parts.Add($"{e.Stat}={F3(r.Base)}->{F3(r.Actual)} want {F3(want)}{(near ? "" : " WRONG")}");
        }

        return new StatComparison(ok, informative, parts);
    }

    private static float Expected(float baseline, AffixExpect e)
    {
        var factor = 1f + e.Percent / 100f;
        switch (e.Math)
        {
            case AffixMath.Over:
                return baseline / factor;
            case AffixMath.Chance:
                return Math.Min(1f, Math.Max(0f, baseline * factor));
            default:
                return baseline * factor;
        }
    }

    /// <summary>A pulse that takes this long or longer is a visible frame (risk 14), and fails the terror check.</summary>
    internal const double PulseBudgetMilliseconds = 16d;

    /// <summary>
    /// The hammer's write, proven on the engines as the adapter read them after it: every live ram and tower holds the vanilla hit points of its type times the factor, within 1%. The gate's
    /// state is logged beside it and judges nothing, since the hammer does little once the gate has fallen. No live engine, or a factor of 1 that no write could show, is a skip.
    /// </summary>
    internal static CheckVerdict Hammer(float factor, LdEngineReport report)
    {
        var gate = report.Gates == 0 ? "none" : report.GatesDestroyed > 0 ? "destroyed" : "standing";
        var lead = $"power=hammer engines={Num(report.Engines.Count)} want={F3(factor)}";
        if (report.Engines.Count == 0)
            return new CheckVerdict(CheckResult.Skip, $"{lead} reason=no-engine gate={gate} gates={Num(report.Gates)} destroyed={Num(report.GatesDestroyed)}");

        var ok = true;
        var parts = new List<string>();
        foreach (var engine in report.Engines)
        {
            var ratio = engine.Vanilla > 0f ? engine.MaxHitPoints / engine.Vanilla : float.NaN;
            var near = Math.Abs(ratio - factor) <= RelativeTolerance * factor;
            ok &= near;
            parts.Add($"{engine.Kind}={F3(ratio)}{(near ? "" : " WRONG")}");
        }

        var detail = $"{lead} got={string.Join(", ", parts)} gate={gate} gates={Num(report.Gates)} destroyed={Num(report.GatesDestroyed)}";
        if (!ok)
            return new CheckVerdict(CheckResult.Fail, detail);

        return Math.Abs(factor - 1f) > AbsoluteTolerance ? new CheckVerdict(CheckResult.Pass, detail) : new CheckVerdict(CheckResult.Skip, detail + " reason=no-signal");
    }

    /// <summary>
    /// A wave's Winged Terror pulses, read from the pulses' own counts: fails when any defender was left below the floor (<c>pushed_below</c>) or the slowest pulse took the whole frame budget; a wave
    /// with no pulse, or where no pulse drained anyone, could not show the effect and is a skip.
    /// </summary>
    internal static CheckVerdict Terror(int wave, float floor, LdTerrorTally tally)
    {
        var detail = $"power=terror w={Num(wave)} pulses={Num(tally.Pulses)} drained={Num(tally.Drained)} lost={F1(tally.Lost)} pushed_below={Num(tally.PushedBelow)} "
            + $"max_ms={F1((float)tally.MaxMilliseconds)} budget_ms={F1((float)PulseBudgetMilliseconds)} floor={F1(floor)}";
        if (tally.PushedBelow > 0 || tally.MaxMilliseconds >= PulseBudgetMilliseconds)
            return new CheckVerdict(CheckResult.Fail, detail);
        if (tally.Pulses == 0)
            return new CheckVerdict(CheckResult.Skip, detail + " reason=no-pulse");

        return tally.Drained > 0 ? new CheckVerdict(CheckResult.Pass, detail) : new CheckVerdict(CheckResult.Skip, detail + " reason=no-signal");
    }

    private static string Flag(bool v) => v ? "true" : "false";

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

    private static string F3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
}
