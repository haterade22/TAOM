using System.Globalization;
using TAOM.Core.Logging;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>The one <c>[Check]</c> line a finished run writes, for the in-game verdict reader (#1069).</summary>
internal static class ScenarioOutcomes
{
    internal const string DoneId = "taom.autorun.campaign-new.done";
    internal const string StoryDoneId = "taom.autorun.story-mission.done";

    internal static string Detail(ScenarioPlan plan, ScenarioRunFacts facts) =>
        plan.Kind == ScenarioPlan.KindStoryMission
            ? "reason=" + facts.Reason + " mission=" + plan.MissionId + " verdict=" + facts.Verdict + " mission_seconds=" + ScenarioAutorunDone.F1(facts.MissionSeconds)
                + " culture=" + plan.Culture
            : "reason=" + facts.Reason + " days_run=" + ScenarioRules.DaysRun(facts.DayStart, facts.DayEnd).ToString(CultureInfo.InvariantCulture)
        + " days_wanted=" + plan.Days.ToString(CultureInfo.InvariantCulture) + " culture=" + plan.Culture;

    internal static void Report(IOutcomeLog outcomes, ScenarioPlan plan, ScenarioRunFacts facts)
    {
        var id = plan.Kind == ScenarioPlan.KindStoryMission ? StoryDoneId : DoneId;
        if (facts.Reason == ScenarioAutorunDone.ReasonOk)
            outcomes.Pass(id, "", Detail(plan, facts));
        else
            outcomes.Fail(id, "", Detail(plan, facts));
    }
}
