using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

// The fortification effects the director applies (phase3-plan.md section 4). Slice 4: field care, D21.
internal sealed partial class LongDefeatDirector
{
    /// <summary>A heal the director applied and has not yet judged: its number, the share, the field's read-back around the write, and the gold the purse booked for the care it carries.</summary>
    private sealed record CareHeal(int N, float Share, LdRest Rest, int Paid, long PurseFell);

    private int _careHeals;
    private int _carePurchasesJudged;
    private long _careSpentJudged;
    private CareHeal? _careHeal;

    /// <summary>
    /// Heals every standing defender by <paramref name="share"/> of the health he is missing, through the same <see cref="ILongDefeatBattlefieldAdapter.RestDefenders"/> the clear's rest uses (D21). The
    /// run hands the share over on the tick after the buy and never on the clear's own tick, and this runs after the tick's batches, so a pack bought beside the care is in it. The line is judged on the
    /// next reading (<see cref="JudgeFieldCare"/>), which must still hold the heal.
    /// </summary>
    private void ApplyFieldCare(float share)
    {
        var rest = _field.RestDefenders(share);
        var bought = _run.FortSummary().Purchases.Where(p => p.Kind == PurchaseKind.FieldCare).Skip(_carePurchasesJudged).ToList();
        var spent = _run.Purse.SpentOn(PurseSink.FieldCare);
        _careHeals++;
        _careHeal = new CareHeal(_careHeals, share, rest, bought.Sum(p => p.Price), spent - _careSpentJudged);
        _carePurchasesJudged += bought.Count;
        _careSpentJudged = spent;
        _logger.LogInfo($"{Tag}[Care] c{_careHeals}: share {Percent(share)}, defenders {rest.Count}, mean health {Percent(rest.MeanBefore)} to {Percent(rest.MeanAfter)}");
    }

    /// <summary>Writes the pending heal's check line. <paramref name="reread"/> is the mean health of the next reading as the field gave it (a negative number when it read nobody), or null when the run ended first and there is no next reading.</summary>
    private void JudgeFieldCare(float? reread)
    {
        if (_careHeal is not { } heal)
            return;

        _careHeal = null;
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.FieldCareId, "c" + heal.N,
            LongDefeatFortChecks.FieldCare(heal.N, heal.Share, heal.Rest, reread, heal.Paid, heal.PurseFell));
    }
}
