using System.Globalization;
using TAOM.Core.Logging;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>The one <c>[Check]</c> line a finished run writes, for the in-game verdict reader (#1069).</summary>
internal static class ScenarioOutcomes
{
    internal const string DoneId = "taom.autorun.campaign-new.done";

    internal static string Detail(ScenarioPlan plan, ScenarioRunFacts facts) =>
        "reason=" + facts.Reason + " days_run=" + ScenarioRules.DaysRun(facts.DayStart, facts.DayEnd).ToString(CultureInfo.InvariantCulture)
        + " days_wanted=" + plan.Days.ToString(CultureInfo.InvariantCulture) + " culture=" + plan.Culture;

    internal static void Report(IOutcomeLog outcomes, ScenarioPlan plan, ScenarioRunFacts facts)
    {
        if (facts.Reason == ScenarioAutorunDone.ReasonOk)
            outcomes.Pass(DoneId, "", Detail(plan, facts));
        else
            outcomes.Fail(DoneId, "", Detail(plan, facts));
    }
}
