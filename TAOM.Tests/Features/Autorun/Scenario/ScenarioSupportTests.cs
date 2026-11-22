using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Autorun;
using TAOM.Features.Autorun.Scenario;

namespace TAOM.Tests.Features.Autorun.Scenario;

[TestClass]
public class ScenarioSupportTests
{
    private static ScenarioPlan Plan(string extra = "") =>
        ScenarioPlan.Resolve(n => n == ScenarioPlan.EnvVar ? @"E:\arm.txt" : null, _ => true, _ => "kind=campaign-new\nout=E:\\o\n" + extra);

    private static ScenarioPlan StoryPlan() =>
        ScenarioPlan.Resolve(n => n == ScenarioPlan.EnvVar ? @"E:\arm.txt" : null, _ => true,
            _ => "kind=story-mission\nout=E:\\o\nmission=helms_deep_defend\n");

    private static ScenarioRunFacts Facts(string reason = "ok") =>
        new(reason, 10, 13, 2, 3, 5, 4, "autosave off, held", 123.45, "2026-11-15T10:00:00", "2026-11-15T10:02:03");

    private static ScenarioRunFacts StoryFacts(string reason = "ok", string verdict = "Won") =>
        Facts(reason) with { Verdict = verdict, MissionSeconds = 241.26 };

    [TestMethod]
    public void Text_WritesEveryKeyInOrder()
    {
        var keys = ScenarioAutorunDone.Text(Plan("days=3\n"), Facts()).TrimEnd('\n').Split('\n').Select(l => l.Split('=')[0]).ToArray();

        CollectionAssert.AreEqual(new[]
        {
            "reason", "kind", "scenario", "culture", "creation", "day_start", "day_end", "days_run", "days_wanted", "inquiries_hidden",
            "notices_hidden", "event_windows_closed", "time_resets", "prepared", "wall_seconds", "started", "ended",
        }, keys);
    }

    [TestMethod]
    public void Text_StoryKind_AppendsMissionVerdictAndSecondsAndDropsTheDays()
    {
        var text = ScenarioAutorunDone.Text(StoryPlan(), StoryFacts());
        var keys = text.TrimEnd('\n').Split('\n').Select(l => l.Split('=')[0]).ToArray();

        CollectionAssert.AreEqual(new[]
        {
            "reason", "kind", "scenario", "culture", "creation", "day_start", "day_end", "inquiries_hidden",
            "notices_hidden", "event_windows_closed", "time_resets", "prepared", "wall_seconds", "started", "ended",
            "mission", "verdict", "mission_seconds",
        }, keys);
        StringAssert.Contains(text, "kind=story-mission\n");
        StringAssert.Contains(text, "mission=helms_deep_defend\n");
        StringAssert.Contains(text, "verdict=Won\n");
        StringAssert.Contains(text, "mission_seconds=241.3\n");
        Assert.IsFalse(text.Contains("mission_opened") || text.Contains("returned"));
    }

    private static ScenarioPlan OfferPlan() =>
        ScenarioPlan.Resolve(n => n == ScenarioPlan.EnvVar ? @"E:\arm.txt" : null, _ => true,
            _ => "kind=story-offer\nout=E:\\o\nevent=hornburg\nchoice=defend\n");

    [TestMethod]
    public void Text_StoryOfferKind_AppendsEventChoiceVerdictAndSecondsAndDropsTheDays()
    {
        var text = ScenarioAutorunDone.Text(OfferPlan(), StoryFacts());
        var keys = text.TrimEnd('\n').Split('\n').Select(l => l.Split('=')[0]).ToArray();

        CollectionAssert.AreEqual(new[]
        {
            "reason", "kind", "scenario", "culture", "creation", "day_start", "day_end", "inquiries_hidden",
            "notices_hidden", "event_windows_closed", "time_resets", "prepared", "wall_seconds", "started", "ended",
            "event", "choice", "verdict", "mission_seconds",
        }, keys);
        StringAssert.Contains(text, "kind=story-offer\n");
        StringAssert.Contains(text, "event=hornburg\nchoice=defend\nverdict=Won\n");
    }

    [TestMethod]
    public void Detail_StoryOfferKind_NamesEventChoiceVerdictSecondsAndCulture()
    {
        Assert.AreEqual("reason=offer_refused event=hornburg choice=defend verdict=- mission_seconds=0.0 culture=gondor",
            ScenarioOutcomes.Detail(OfferPlan(), Facts("offer_refused") with { MissionSeconds = 0.0 }));
    }

    [TestMethod]
    public void Report_StoryOfferKind_UsesTheOfferDoneId()
    {
        var log = Substitute.For<IOutcomeLog>();

        ScenarioOutcomes.Report(log, OfferPlan(), StoryFacts("ok"));
        ScenarioOutcomes.Report(log, OfferPlan(), StoryFacts("offer_not_shown", "-"));

        Assert.AreEqual("taom.autorun.story-offer.done", ScenarioOutcomes.StoryOfferDoneId);
        log.Received(1).Pass(ScenarioOutcomes.StoryOfferDoneId, "", NSubstitute.Arg.Any<string>());
        log.Received(1).Fail(ScenarioOutcomes.StoryOfferDoneId, "", NSubstitute.Arg.Is<string>(s => s.StartsWith("reason=offer_not_shown")));
    }

    [TestMethod]
    public void Text_CampaignNew_HasNoMissionKeys()
    {
        var text = ScenarioAutorunDone.Text(Plan(), Facts());

        Assert.IsFalse(text.Contains("mission") || text.Contains("verdict"));
    }

    [TestMethod]
    public void Text_Values_AreTheFactsWithPreparedKeepingItsSpaces()
    {
        var text = ScenarioAutorunDone.Text(Plan("scenario=smoke\n"), Facts());

        StringAssert.Contains(text, "reason=ok\n");
        StringAssert.Contains(text, "kind=campaign-new\n");
        StringAssert.Contains(text, "scenario=smoke\n");
        StringAssert.Contains(text, "culture=gondor\n");
        StringAssert.Contains(text, "creation=skipped\n");
        StringAssert.Contains(text, "days_run=3\n");
        StringAssert.Contains(text, "notices_hidden=3\n");
        StringAssert.Contains(text, "event_windows_closed=5\n");
        StringAssert.Contains(text, "time_resets=4\n");
        StringAssert.Contains(text, "prepared=autosave off, held\n");
        StringAssert.Contains(text, "wall_seconds=123.5\n");
        StringAssert.Contains(text, "ended=2026-11-15T10:02:03\n");
    }

    [TestMethod]
    public void Text_UnderAGermanCulture_StillUsesTheInvariantCulture()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var text = ScenarioAutorunDone.Text(Plan(), Facts());

            StringAssert.Contains(text, "wall_seconds=123.5\n");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = old;
        }
    }

    [TestMethod]
    public void Status_WritesPhaseWallDayScreenAndTimeLock()
    {
        var text = ScenarioAutorunDone.Status("Running", 12.34, 7, AutorunScreen.MapMenu, true);

        Assert.AreEqual("phase=Running\nwall=12.3\nday=7\nscreen=MapMenu\ntime_locked=1\n", text);
    }

    [TestMethod]
    public void Detail_NamesReasonDaysAndCulture()
    {
        Assert.AreEqual("reason=stalled days_run=3 days_wanted=3 culture=gondor", ScenarioOutcomes.Detail(Plan(), Facts("stalled")));
    }

    [TestMethod]
    public void Detail_StoryKind_NamesMissionVerdictSecondsAndCulture()
    {
        Assert.AreEqual("reason=mission_undecided mission=helms_deep_defend verdict=Undecided mission_seconds=241.3 culture=gondor",
            ScenarioOutcomes.Detail(StoryPlan(), StoryFacts("mission_undecided", "Undecided")));
    }

    [TestMethod]
    public void Report_StoryKind_UsesTheStoryDoneId()
    {
        var log = Substitute.For<IOutcomeLog>();

        ScenarioOutcomes.Report(log, StoryPlan(), StoryFacts("ok"));
        ScenarioOutcomes.Report(log, StoryPlan(), StoryFacts("mission_limit", "-"));

        Assert.AreEqual("taom.autorun.story-mission.done", ScenarioOutcomes.StoryDoneId);
        log.Received(1).Pass(ScenarioOutcomes.StoryDoneId, "", NSubstitute.Arg.Any<string>());
        log.Received(1).Fail(ScenarioOutcomes.StoryDoneId, "", NSubstitute.Arg.Is<string>(s => s.StartsWith("reason=mission_limit")));
        log.DidNotReceive().Pass(ScenarioOutcomes.DoneId, NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>());
    }

    [TestMethod]
    public void Report_OnlyOkPasses()
    {
        var log = Substitute.For<IOutcomeLog>();

        ScenarioOutcomes.Report(log, Plan(), Facts("ok"));
        ScenarioOutcomes.Report(log, Plan(), Facts("wall_limit"));

        log.Received(1).Pass(ScenarioOutcomes.DoneId, "", NSubstitute.Arg.Any<string>());
        log.Received(1).Fail(ScenarioOutcomes.DoneId, "", NSubstitute.Arg.Is<string>(s => s.StartsWith("reason=wall_limit")));
    }

    [TestMethod]
    public void Driver_ArmedPlan_BuildsTheDriver()
    {
        var logger = Substitute.For<IModLogger>();
        var built = 0;

        var driver = ScenarioAutorunStart.Driver(Plan(), () =>
        {
            built++;
            return new ScenarioAutorunDriver(Plan(), new NullCampaignAutorunHost(), logger, Substitute.For<IOutcomeLog>());
        }, logger);

        Assert.IsNotNull(driver);
        Assert.AreEqual(1, built);
    }

    [TestMethod]
    public void Driver_UnsetVariable_IsNullAndSilent()
    {
        var logger = Substitute.For<IModLogger>();

        var driver = ScenarioAutorunStart.Driver(new ScenarioPlan(), () => throw new InvalidOperationException("built"), logger);

        Assert.IsNull(driver);
        Assert.AreEqual(0, logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Driver_ProblemPlan_IsNullAndLogsTheProblemOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var plan = ScenarioPlan.Resolve(n => n == ScenarioPlan.EnvVar ? "x" : null, _ => false, _ => "");

        var driver = ScenarioAutorunStart.Driver(plan, () => throw new InvalidOperationException("built"), logger);

        Assert.IsNull(driver);
        logger.Received(1).LogWarning(NSubstitute.Arg.Is<string>(s => s.StartsWith("[Autorun][Scenario] not armed: ") && s.Contains("missing file")));
    }

    [TestMethod]
    public void NullHost_DoesNothingAndAnswersNo()
    {
        var host = new NullCampaignAutorunHost();

        Assert.AreEqual(AutorunScreen.Other, host.Screen);
        Assert.IsNull(host.HideInquiry());
        Assert.IsFalse(host.SceneNotificationActive);
        host.HideSceneNotification();
        Assert.IsFalse(host.CloseEventWindow());
        Assert.IsFalse(host.StartNewCampaign());
        Assert.IsFalse(host.FinishVideo());
        Assert.IsFalse(host.TryFinishCreation("gondor", out var problem));
        Assert.IsNull(problem);
        Assert.AreEqual("", host.PrepareMap());
        Assert.IsFalse(host.KeepFastForward(15f));
        Assert.IsFalse(host.TimeLocked);
        Assert.IsTrue(double.IsNaN(host.ElapsedDays));
        host.RestoreFocusSetting();
        host.EndCampaign();
        host.WriteFile("a", "b");
        host.Quit();
    }

    [TestMethod]
    public void NullHost_StoryMembers_RefuseAndReportNothing()
    {
        var host = new NullCampaignAutorunHost();

        Assert.IsFalse(host.OpenStoryMenu("helms_deep_defend", out var message));
        Assert.AreEqual("no campaign", message);
        Assert.IsFalse(host.StoryMenuShown);
        Assert.IsNull(host.OpenMenuId);
        Assert.IsFalse(host.ChooseOpenMission());
        Assert.IsFalse(host.ChooseBackToMap());
        Assert.IsNull(host.StoryVerdict);
        Assert.IsFalse(host.EventWindowOpenOrWaiting);
    }

    [TestMethod]
    public void NullHost_OfferMembers_RefuseAndReportNothing()
    {
        var host = new NullCampaignAutorunHost();

        Assert.IsNull(host.PendingOfferEvent);
        Assert.AreEqual("", host.PendingOfferChoices);
        Assert.IsFalse(host.InquiryActive);
        Assert.IsFalse(host.AcceptOfferPopup(out var title));
        Assert.AreEqual("", title);
        Assert.IsFalse(host.OfferMenuShown);
        Assert.IsNull(host.ChooseOfferOption(new[] { "taom_story_offer_defend" }, true));
    }

    [TestMethod]
    public void Module_RegistersTheDriverWithTheNullHostAsSingletons()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        var module = new AutorunModule();

        module.RegisterServices(container);

        Assert.AreEqual("Autorun", module.Id);
        Assert.IsInstanceOfType(container.Resolve<ICampaignAutorunHost>(), typeof(NullCampaignAutorunHost));
        Assert.AreSame(container.Resolve<ScenarioAutorunDriver>(), container.Resolve<ScenarioAutorunDriver>());
        Assert.AreSame(container.Resolve<ScenarioPlan>(), container.Resolve<ScenarioPlan>());
    }
}
