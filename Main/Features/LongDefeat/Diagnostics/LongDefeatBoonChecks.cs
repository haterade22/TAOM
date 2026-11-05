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

    /// <summary>
    /// A pick: the last draw showed as many cards as it could (three, or what was eligible), none twice, the boon taken was among them, and the numbers read back
    /// from the run (<paramref name="rulesNow"/>, <paramref name="factorsNow"/> and the gold the boons paid) are the shipped ones changed by every boon owned, drawbacks included.
    /// </summary>
    internal static CheckVerdict Pick(BoonPickRecord record, IReadOnlyCollection<BoonRow> owned, RunRules shipped, RunRules rulesNow, GoldFactors factorsNow, long goldFromBoons)
    {
        var last = record.Draws.Count == 0 ? new BoonDraw(Array.Empty<string>(), 0, 0, 0) : record.Draws[record.Draws.Count - 1];
        var wanted = Math.Min(BoonDeck.CardCount, last.Eligible);
        var cardsOk = last.Ids.Count == wanted && wanted > 0;
        var distinct = last.Ids.Distinct().Count() == last.Ids.Count;
        var taken = record.Taken != null && last.Ids.Contains(record.Taken);
        var applied = Applied(owned, shipped, rulesNow, factorsNow, goldFromBoons);
        return LongDefeatChecks.Verdict(cardsOk && distinct && taken && applied,
            $"p={Num(record.Pick)} cards={Num(last.Ids.Count)} eligible={Num(last.Eligible)} offered={string.Join(",", last.Ids)} taken={record.Taken ?? "none"} "
            + $"draws={Num(record.Draws.Count)} distinct={Flag(distinct)} applied={Flag(applied)} gold={Num(goldFromBoons)}");
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
