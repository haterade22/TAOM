using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>The boon proof (phase2-plan.md section 6): a line when a pick opens or lapses, and the <c>boon</c> and <c>reroll</c> checks when a card is taken or a reroll spent.</summary>
internal sealed partial class LongDefeatDirector
{
    private void OnBoonEvent(RunEvent e)
    {
        var pick = PickRecord(e.Detail);
        switch (e.Kind)
        {
            case RunEventKind.BoonOffered when pick != null:
                _logger.LogInfo($"{Tag}[Boon] pick {pick.Pick} offered after wave {pick.Wave}: {Cards(pick.Draws[pick.Draws.Count - 1].Ids)} (eligible {pick.Draws[pick.Draws.Count - 1].Eligible}, banked {_run.GetView().Rerolls})");
                break;
            case RunEventKind.BoonLapsed:
                _logger.LogInfo($"{Tag}[Boon] pick {e.Detail.Replace("pick=", "")} lapses after wave {e.Wave}: no boon is eligible");
                break;
            case RunEventKind.BoonTaken when pick != null:
                var verdict = LongDefeatBoonChecks.Pick(pick, _run.OwnedBoons, _plan.Rules, _run.Rules, _run.Factors, _run.Purse.EarnedFrom(PurseSource.Boon));
                LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BoonId, "p" + pick.Pick, verdict);
                _logger.LogInfo($"{Tag}[Boon] pick {pick.Pick} taken {pick.Taken}, acts from wave {pick.TakenBeforeWave}: {verdict.Detail}");
                break;
            case RunEventKind.Rerolled when pick != null:
                var reroll = LongDefeatBoonChecks.Reroll(pick, pick.Draws.Count - 1);
                LongDefeatChecks.Record(_outcomes, LongDefeatChecks.RerollId, "p" + pick.Pick, reroll);
                _logger.LogInfo($"{Tag}[Boon] pick {pick.Pick} rerolled: {reroll.Detail}");
                break;
        }
    }

    /// <summary>The pick an event's <c>pick=N</c> detail names.</summary>
    private BoonPickRecord? PickRecord(string detail)
    {
        var start = detail.IndexOf("pick=", System.StringComparison.Ordinal);
        if (start < 0)
            return null;

        var digits = new string(detail.Substring(start + 5).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? _run.BoonSummary().Picks.FirstOrDefault(p => p.Pick == number)
            : null;
    }

    private string Cards(System.Collections.Generic.IReadOnlyList<string> ids) => string.Join(", ", ids);
}
