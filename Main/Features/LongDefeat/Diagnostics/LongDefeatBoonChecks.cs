using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>boon</c> and <c>reroll</c> verdicts of #1079 (phase2-plan.md section 6). Pure: the director reads the run's pick records and numbers, these turn them into
/// pass or fail. What a pick must have changed is recomputed here, apart from <c>RunModifiers</c>, so a check that shared the arithmetic could not catch a change in it.
/// </summary>
internal static class LongDefeatBoonChecks
{
    private const float Tolerance = 1e-3f;

    /// <summary>The driven property of a rider's horse: read from the mount agent, not the rider, so the adapter special-cases this name.</summary>
    internal const string MountSpeed = "MountSpeed";

    /// <summary>
    /// A pick: the last draw showed as many cards as it could (three, or what was eligible), none twice, the boon taken was among them, and the numbers read back
    /// from the run (<paramref name="rulesNow"/>, <paramref name="factorsNow"/> and the gold the boons paid) are the shipped ones changed by every boon owned, drawbacks included.
    /// </summary>
    internal static CheckVerdict Pick(BoonPickRecord record, IReadOnlyCollection<BoonRow> owned, RunRules shipped, RunRules rulesNow, GoldFactors factorsNow, long goldFromBoons,
        IReadOnlyCollection<CheckVerdict>? reads = null)
    {
        var last = record.Draws.Count == 0 ? new BoonDraw(Array.Empty<string>(), 0, 0, 0) : record.Draws[record.Draws.Count - 1];
        var wanted = Math.Min(BoonDeck.CardCount, last.Eligible);
        var cardsOk = last.Ids.Count == wanted && wanted > 0;
        var distinct = last.Ids.Distinct().Count() == last.Ids.Count;
        var taken = record.Taken != null && last.Ids.Contains(record.Taken);
        var applied = Applied(owned, shipped, rulesNow, factorsNow, goldFromBoons);
        var read = reads == null || reads.Count == 0 ? null : reads.Any(r => r.Result == CheckResult.Fail) ? "fail" : reads.Any(r => r.Result == CheckResult.Pass) ? "pass" : "skip";
        return LongDefeatChecks.Verdict(cardsOk && distinct && taken && applied && read != "fail",
            $"p={Num(record.Pick)} cards={Num(last.Ids.Count)} eligible={Num(last.Eligible)} offered={string.Join(",", last.Ids)} taken={record.Taken ?? "none"} "
            + $"draws={Num(record.Draws.Count)} distinct={Flag(distinct)} applied={Flag(applied)} gold={Num(goldFromBoons)}" + (read == null ? "" : " read=" + read));
    }

    /// <summary>
    /// The driven properties a target's soldiers are expected to hold with the boons <paramref name="owned"/>: each stat key of that target (drawbacks included) summed into one percent
    /// per property, with the arithmetic the engine's applier uses. The names are written out here on purpose, apart from <c>RunModifiers</c>.
    /// </summary>
    internal static IReadOnlyList<AffixExpect> TargetExpectations(IReadOnlyCollection<BoonRow> owned, BoonTarget target)
    {
        float Sum(string key) => owned.Sum(b => b.Effects.Concat(b.Drawback).Where(e => e.Key == key && e.Target == target).Sum(e => e.Value));
        var found = new List<AffixExpect>();
        void Add(string key, AffixMath math, params string[] stats)
        {
            if (!owned.Any(b => b.Effects.Concat(b.Drawback).Any(e => e.Key == key && e.Target == target)))
                return;

            found.AddRange(stats.Select(stat => new AffixExpect(stat, math, Sum(key))));
        }

        Add(BoonKeys.SwingSpeedPercent, AffixMath.Times, "SwingSpeedMultiplier");
        Add(BoonKeys.MoveSpeedPercent, AffixMath.Times, "MaxSpeedMultiplier", "CombatMaxSpeedMultiplier");
        Add(BoonKeys.DrawSpeedPercent, AffixMath.Times, "ThrustOrRangedReadySpeedMultiplier");
        Add(BoonKeys.ReloadSpeedPercent, AffixMath.Times, "ReloadSpeed");
        Add(BoonKeys.BlockPercent, AffixMath.Chance, "AIBlockOnDecideAbility");
        Add(BoonKeys.ParryPercent, AffixMath.Chance, "AIParryOnDecideAbility");
        Add(BoonKeys.AimErrorPercent, AffixMath.Times, "AiShooterError");
        // The horse of a rider takes the same percent as his MountSpeed (RunModifiers gives the cavalry and the lord a mount speed). The rider's own speed carries the boon whether or not his horse does.
        if (target is BoonTarget.Cavalry or BoonTarget.Player)
            Add(BoonKeys.MoveSpeedPercent, AffixMath.Times, MountSpeed);
        return found;
    }

    /// <summary>The health limit over the character's maximum a target's soldier is expected to hold: the product, over the boons owned, of each boon's own factor for that target.</summary>
    internal static float ExpectedHealth(IReadOnlyCollection<BoonRow> owned, BoonTarget target) =>
        owned.Aggregate(1f, (factor, b) => b.Effects.Concat(b.Drawback).Where(e => e.Key == BoonKeys.HealthPercent && e.Target == target).Aggregate(factor, (f, e) => f * (1f + e.Value / 100f)));

    /// <summary>
    /// A soldier of a target read back after a pick (<paramref name="when"/> is <c>standing</c> for one that stood at the pick, <c>spawned</c> for one built after it): its health limit
    /// over the character's maximum is the combined health factor and each property is its baseline changed by the summed percent, within 1%. No soldier of the group is a skip, so is a
    /// read that could not show a change; neither reads as a pass it did not earn.
    /// </summary>
    internal static CheckVerdict TargetRead(int pick, string when, BoonTarget target, IReadOnlyCollection<BoonRow> owned, LdTargetSample sample)
    {
        var head = $"p={Num(pick)} read={when} target={target.ToString().ToLowerInvariant()}";
        if (!sample.HasAgent)
            return new CheckVerdict(CheckResult.Skip, head + " reason=no-soldier");

        var wantHealth = ExpectedHealth(owned, target);
        var ratio = sample.CharacterMax > 0f ? sample.HealthLimit / sample.CharacterMax : float.NaN;
        var healthOk = Math.Abs(ratio - wantHealth) <= 0.01f * wantHealth;
        // A soldier on foot has no horse to read; the adapter adds a MountSpeed reading only when one stands under him.
        var expected = TargetExpectations(owned, target).Where(e => e.Stat != MountSpeed || sample.Readings.Any(r => r.Stat == MountSpeed)).ToList();
        var compared = LongDefeatAffixChecks.Compare(expected, new LdStatSample(sample.TroopId, sample.Readings));
        var informative = compared.Informative || Math.Abs(wantHealth - 1f) > 1e-4f;
        var parts = new List<string> { $"health={ratio.ToString("F3", CultureInfo.InvariantCulture)} want {wantHealth.ToString("F3", CultureInfo.InvariantCulture)}{(healthOk ? "" : " WRONG")}" };
        parts.AddRange(compared.Parts);
        var detail = $"{head} troop={sample.TroopId} {string.Join("; ", parts)}";
        if (!healthOk || !compared.Ok)
            return new CheckVerdict(CheckResult.Fail, detail);

        return informative ? new CheckVerdict(CheckResult.Pass, detail) : new CheckVerdict(CheckResult.Skip, detail + " reason=no-signal");
    }

    /// <summary>
    /// A reroll (draw <paramref name="draw"/> of the pick, 1 or more): the bank fell by one, three new cards (or what was eligible) none twice, and none of the cards
    /// just shown when three or more others were eligible.
    /// </summary>
    internal static CheckVerdict Reroll(BoonPickRecord record, int draw)
    {
        if (draw < 1 || draw >= record.Draws.Count)
            return new CheckVerdict(CheckResult.Fail, $"p={Num(record.Pick)} draw={Num(draw)} reason=no-such-draw");

        var now = record.Draws[draw];
        var before = record.Draws[draw - 1];
        var repeated = now.Ids.Count(before.Ids.Contains);
        var bankFell = now.BankAfter == now.BankBefore - 1;
        var cardsOk = now.Ids.Count == Math.Min(BoonDeck.CardCount, now.Eligible) && now.Ids.Count > 0 && now.Ids.Distinct().Count() == now.Ids.Count;
        var avoided = now.Others < BoonDeck.CardCount || repeated == 0;
        return LongDefeatChecks.Verdict(bankFell && cardsOk && avoided,
            $"p={Num(record.Pick)} draw={Num(draw)} bank={Num(now.BankBefore)}>{Num(now.BankAfter)} cards={Num(now.Ids.Count)} offered={string.Join(",", now.Ids)} "
            + $"repeated={Num(repeated)} others={Num(now.Others)}");
    }

    private static bool Applied(IReadOnlyCollection<BoonRow> owned, RunRules shipped, RunRules now, GoldFactors factors, long goldFromBoons)
    {
        float Sum(string key) => owned.Sum(b => b.Effects.Concat(b.Drawback).Where(e => e.Key == key).Sum(e => e.Value));
        float Mult(string key) => Math.Max(0f, 1f + Sum(key) / 100f);
        bool Near(float a, float b) => Math.Abs(a - b) <= Tolerance * Math.Max(1f, Math.Abs(b));

        var paid = (long)owned.Sum(b => b.Effects.Where(e => e.Key == BoonKeys.Gold).Sum(e => e.Value));
        return now.GarrisonCap == Math.Max(1, shipped.GarrisonCap + (int)Sum(BoonKeys.GarrisonCap))
            && Near(now.IntermissionSeconds, Math.Max(5f, shipped.IntermissionSeconds + Sum(BoonKeys.IntermissionSeconds)))
            && Near(now.RestShare, Math.Min(1f, Math.Max(0f, shipped.RestShare + Sum(BoonKeys.RestShare))))
            && Near(now.KeepHoldSeconds, Math.Max(5f, shipped.KeepHoldSeconds * Mult(BoonKeys.KeepHoldPercent)))
            && now.PackBonus == shipped.PackBonus + (int)Sum(BoonKeys.PackBonus)
            && Near(factors.Bounty, Mult(BoonKeys.BountyPercent)) && Near(factors.WaveBonus, Mult(BoonKeys.WaveBonusPercent))
            && Near(factors.EarlyGold, Mult(BoonKeys.EarlyGoldPercent)) && Near(factors.Price, Mult(BoonKeys.PricePercent))
            && goldFromBoons == paid;
    }

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool v) => v ? "true" : "false";
}
