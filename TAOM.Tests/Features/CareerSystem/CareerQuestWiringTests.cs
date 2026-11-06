using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Pins the three call sites in <c>CareerQuestCampaignBehavior</c> that carry the career quest cancel
/// fixes. The service tests cover the decisions, but the behavior is an engine entry point, and the
/// Codex re-review of 2026-10-27 showed each call site reverting with every service test still green:
/// the quest id (a reused id merges the new quest into the cancelled quest's journal record), the
/// session-launch flag (coop-compat-gap-ledger R1b) and vanilla's reason text. These read source text
/// only, so they carry no RequiresGame tag.
/// </summary>
[TestClass]
public class CareerQuestWiringTests
{
    private static string Behavior() =>
        RepoPaths.ReadSource("Main/Features/CareerSystem/Quests/CareerQuestCampaignBehavior.cs", stripComments: true);

    [TestMethod]
    public void OnAccept_BuildsEachQuestIdWithNewQuestStringId()
    {
        var src = Behavior();

        StringAssert.Matches(src, new Regex(@"var\s+questId\s*=\s*CareerQuestService\.NewQuestStringId\("));
        StringAssert.Matches(src, new Regex(@"new\s+CareerQuest\(\s*questId\s*,"));
        Assert.IsFalse(Regex.IsMatch(src, "\"taom_cq_\"\\s*\\+"),
            "The behavior must not build a quest id from the definition alone: the engine keys the journal record by StringId.");
    }

    [TestMethod]
    public void SessionLaunchAndDailyTick_PassTheirOwnSessionLaunchFlag()
    {
        var src = Behavior();

        StringAssert.Matches(src, new Regex(@"OnSessionLaunched\([^)]*\)\s*=>\s*TryOfferNext\(\s*atSessionLaunch:\s*true\s*\)"));
        StringAssert.Matches(src, new Regex(@"OnDailyTick\(\)\s*=>\s*TryOfferNext\(\s*atSessionLaunch:\s*false\s*\)"));
        StringAssert.Matches(src, new Regex(@"IsFormerPlayerHeroQuest\([^;]*atSessionLaunch\)"));
    }

    [TestMethod]
    public void Cancel_PassesVanillasPlayerChangeReason()
    {
        StringAssert.Matches(Behavior(), new Regex(@"CompleteQuestWithCancel\(\s*new\s+TextObject\(\s*""\{=bYdhYidf\}"));
    }
}
