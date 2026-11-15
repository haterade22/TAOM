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

    private static ScenarioRunFacts Facts(string reason = "ok") =>
        new(reason, 10, 13, 2, 3, 5, 4, "autosave off, held", 123.45, "2026-11-15T10:00:00", "2026-11-15T10:02:03");

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
