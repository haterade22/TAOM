using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionStartGuard;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.MissionStartGuard;

/// <summary>
/// The in-game verdict reader (tools/ingame_verdict.py) judges #1028 from tools/ingame_checks/1028.toml. These tests keep the
/// ids the service writes and the ids the manifest reads the same, and legal for the outcome log.
/// </summary>
[TestClass]
public class MissionStartGuardManifestTests
{
    private const string Manifest = "tools/ingame_checks/1028.toml";

    [TestMethod]
    public void CheckIds_AreLegalOutcomeIds()
    {
        var legal = new Regex(@"^[a-z0-9]+(\.[a-z0-9-]+)+$");

        Assert.IsTrue(legal.IsMatch(MissionStartGuardChecks.DeferredAddId), MissionStartGuardChecks.DeferredAddId);
        Assert.IsTrue(legal.IsMatch(MissionStartGuardChecks.ListChangeId), MissionStartGuardChecks.ListChangeId);
    }

    [TestMethod]
    public void RealOutcomeLog_RendersTheExactLinesTheManifestReads()
    {
        // The same literals are judged against the real manifest in tools/tests/test_ingame_verdict.py (Issue1028Tests): a change
        // to the line format fails one of the two, so the verdict can never read NOT REACHED for a line that is written.
        var lines = new System.Collections.Generic.List<string>();
        var logger = Substitute.For<IModLogger>();
        logger.When(l => l.LogInfo(Arg.Any<string>())).Do(call => lines.Add((string)call[0]));
        var log = new OutcomeLog(logger);

        MissionStartGuardChecks.RecordFlush(log, "1", 2, 2);
        MissionStartGuardChecks.RecordFlush(log, "2", 2, 1);
        MissionStartGuardChecks.RecordListChange(log, "3", TAOM.Features.MissionStartGuard.Models.StartCall.AfterStart, "Mod.Foo", "MissionBehaviors", 3, 4, "Mod.Newest");

        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.mission-start-guard.deferred-add result=pass queued=2 added=2",
            "[Check] id=taom.mission-start-guard.deferred-add result=fail queued=2 added=1 reason=engine-add-threw",
            "[Check] id=taom.mission-start-guard.list-change result=fail call=AfterStart owner=Mod.Foo list=MissionBehaviors before=3 after=4 newest=Mod.Newest",
        }, lines);
    }

    [TestMethod]
    public void Manifest_ReadsTheDeferredAddCheckAndTreatsAListChangeAsAFail()
    {
        var toml = ReadSource(Manifest);

        StringAssert.Contains(toml, "issue = 1028");
        StringAssert.Contains(toml, "id = \"" + MissionStartGuardChecks.DeferredAddId + "\"");
        StringAssert.Contains(toml, MissionStartGuardChecks.DeferredAddId.Replace(".", "\\."),
            "the reached and pass patterns name the deferred-add id");
        StringAssert.Contains(toml, MissionStartGuardChecks.ListChangeId.Replace(".", "\\."),
            "a list change inside the walk is the failure #1028 describes");
    }
}
