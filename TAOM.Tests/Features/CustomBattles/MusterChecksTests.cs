using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Muster;
using TAOM.Tests.Features.LoadTimeStamps;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Issue #1184: the [Check] lines the muster screen's Start writes when the player serves as a captain, read by
/// tools/ingame_checks/1184.toml. The screen calls the static Outcomes facade, so these tests render the same calls
/// through a real OutcomeLog and never touch the facade.
/// </summary>
[TestClass]
public class MusterChecksTests
{
    private const string StartedBattle = "[Check] id=taom.muster.captain-start result=pass type=Battle general=lord_1_2";
    private const string StartedSiege = "[Check] id=taom.muster.captain-start result=pass type=Siege general=none";
    private const string Threw = "[Check] id=taom.muster.captain-start result=fail type=Battle reason=NullReferenceException";
    private const string SiegeThrew = "[Check] id=taom.muster.captain-start result=fail type=Siege reason=InvalidOperationException";

    [TestMethod]
    public void CheckId_IsValid()
        => Assert.IsTrue(OutcomeLog.IsValidId(MusterChecks.CaptainStartId), MusterChecks.CaptainStartId);

    [TestMethod]
    public void StartedDetail_NamesTheGameTypeAndTheGeneral()
    {
        Assert.AreEqual("type=Battle general=lord_1_2", MusterChecks.StartedDetail("Battle", "lord_1_2"));
        Assert.AreEqual("type=Siege general=none", MusterChecks.StartedDetail("Siege", null));
    }

    [TestMethod]
    public void FailedDetail_NamesTheInnermostException()
        => Assert.AreEqual("type=Battle reason=NullReferenceException",
            MusterChecks.FailedDetail("Battle", new InvalidOperationException("outer", new NullReferenceException())));

    [TestMethod]
    public void RenderedLines_AreTheOnesThe1184ManifestAndFixturesRead()
    {
        var logger = Substitute.For<IModLogger>();
        var log = new OutcomeLog(logger, new FakeStampClock(), () => 0);

        // The calls exactly as TcbVM.ExecuteStart makes them.
        log.Pass(MusterChecks.CaptainStartId, "Battle", MusterChecks.StartedDetail("Battle", "lord_1_2"));
        log.Pass(MusterChecks.CaptainStartId, "Siege", MusterChecks.StartedDetail("Siege", null));
        log.Fail(MusterChecks.CaptainStartId, "Battle",
            MusterChecks.FailedDetail("Battle", new NullReferenceException()));
        log.Fail(MusterChecks.CaptainStartId, "Siege",
            MusterChecks.FailedDetail("Siege", new InvalidOperationException()));

        CollectionAssert.AreEqual(new[] { StartedBattle, StartedSiege, Threw, SiegeThrew }, InfoLines(logger));

        // The Python fixtures hold these lines; they must not drift from the format.
        var fixtures = new Dictionary<string, string[]>
        {
            ["muster_captain_pass.txt"] = new[] { StartedBattle },
            ["muster_captain_threw.txt"] = new[] { Threw },
            ["muster_captain_siege_only.txt"] = new[] { StartedSiege },
            ["muster_captain_pass_and_fail.txt"] = new[] { StartedBattle, SiegeThrew },
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

    /// <summary>
    /// The view model is game-tested UI, so its calls are pinned by source text, in order: the pass line is written
    /// only after the engine's start call has returned, and the fail line only from the catch around it.
    /// </summary>
    [TestMethod]
    public void TcbVM_WritesThePassAfterTheStartCall_AndTheFailFromTheCatch()
    {
        var source = File.ReadAllText(RepoPath("Client", "Features", "CustomBattles", "Muster", "TcbVM.cs"));

        var start = source.IndexOf("CustomBattleHelper.StartGame(data);", StringComparison.Ordinal);
        var pass = source.IndexOf("Outcomes.Pass(MusterChecks.CaptainStartId,", StringComparison.Ordinal);
        var caught = source.IndexOf("catch (Exception e)", start < 0 ? 0 : start, StringComparison.Ordinal);
        var fail = source.IndexOf("Outcomes.Fail(MusterChecks.CaptainStartId,", StringComparison.Ordinal);

        Assert.IsTrue(start >= 0 && pass >= 0 && caught >= 0 && fail >= 0, "the start call, both check lines and the catch");
        Assert.IsTrue(start < pass && pass < caught && caught < fail, "order: start, pass, catch, fail");
    }

    private static string[] InfoLines(IModLogger logger) =>
        logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0])
            .ToArray();
}
