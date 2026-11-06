using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.UI;
using TAOM.Tests.Features.LoadTimeStamps;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Issue #971 (#1072): the [Check] lines the career button's click and the career screen's OnInitialize write,
/// read by tools/ingame_checks/971.toml. The sites call the static Outcomes facade, so these tests render the same
/// calls through a real OutcomeLog and never touch the facade, whose state OutcomesTests resets.
/// </summary>
[TestClass]
public class CareerScreenChecksTests
{
    private const string OpenPass = "[Check] id=taom.career-screen.open result=pass source=button";
    private const string OpenNotOnTop = "[Check] id=taom.career-screen.open result=fail source=button reason=not-on-top";
    private const string OpenThrew = "[Check] id=taom.career-screen.open result=fail source=button reason=NullReferenceException";
    private const string InitNormal = "[Check] id=taom.career-screen.init result=pass mode=normal";
    private const string InitSwitch = "[Check] id=taom.career-screen.init result=pass mode=switch";

    [TestMethod]
    public void CheckIds_AreValid()
    {
        Assert.IsTrue(OutcomeLog.IsValidId(CareerScreenChecks.OpenId), CareerScreenChecks.OpenId);
        Assert.IsTrue(OutcomeLog.IsValidId(CareerScreenChecks.InitId), CareerScreenChecks.InitId);
    }

    [TestMethod]
    public void FailReason_NamesTheInnermostException()
    {
        // Activator.CreateInstance wraps a throw in the screen's constructor.
        Assert.AreEqual("NullReferenceException",
            CareerScreenChecks.FailReason(new TargetInvocationException(new NullReferenceException())));
        Assert.AreEqual("InvalidOperationException", CareerScreenChecks.FailReason(new InvalidOperationException()));
    }

    [TestMethod]
    public void Mode_IsNormalForTheButton_AndSwitchForTheDialogue()
    {
        Assert.AreEqual("normal", CareerScreenChecks.Mode(false));
        Assert.AreEqual("switch", CareerScreenChecks.Mode(true));
    }

    [TestMethod]
    public void RenderedLines_AreTheOnesThe971ManifestAndFixturesRead()
    {
        var logger = Substitute.For<IModLogger>();
        var log = new OutcomeLog(logger, new FakeStampClock(), () => 0);
        var threw = CareerScreenChecks.FailReason(new TargetInvocationException(new NullReferenceException()));

        // The calls exactly as ViewModel_ExecuteCommand_CareerScreen_Patch and GauntletCareerScreen make them.
        log.Pass(CareerScreenChecks.OpenId, "", CareerScreenChecks.OpenPassDetail);
        log.Fail(CareerScreenChecks.OpenId, CareerScreenChecks.NotOnTop, CareerScreenChecks.OpenFailDetail(CareerScreenChecks.NotOnTop));
        log.Fail(CareerScreenChecks.OpenId, threw, CareerScreenChecks.OpenFailDetail(threw));
        log.Pass(CareerScreenChecks.InitId, CareerScreenChecks.Mode(false), CareerScreenChecks.InitDetail(false));
        log.Pass(CareerScreenChecks.InitId, CareerScreenChecks.Mode(true), CareerScreenChecks.InitDetail(true));

        CollectionAssert.AreEqual(new[] { OpenPass, OpenNotOnTop, OpenThrew, InitNormal, InitSwitch }, InfoLines(logger));

        // The Python fixtures render these lines; they must not drift from the format.
        var fixtures = new Dictionary<string, string[]>
        {
            ["career_open_pass.txt"] = new[] { InitNormal, OpenPass },
            ["career_open_threw.txt"] = new[] { OpenThrew },
            ["career_open_not_on_top.txt"] = new[] { OpenNotOnTop },
            ["career_open_no_init.txt"] = new[] { OpenPass },
            ["career_switch_only.txt"] = new[] { InitSwitch },
        };
        foreach (var fixture in fixtures)
        {
            var path = RepoPath("tools", "tests", "fixtures", "ingame_verdict", fixture.Key);
            Assert.IsTrue(File.Exists(path), path);
            var lines = File.ReadAllLines(path);
            foreach (var line in fixture.Value)
                Assert.IsTrue(lines.Any(l => l.EndsWith("] [INFO] " + line, StringComparison.Ordinal)),
                    fixture.Key + " lacks the rendered line: " + line);
        }
    }

    private static string[] InfoLines(IModLogger logger) =>
        logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0])
            .ToArray();
}
