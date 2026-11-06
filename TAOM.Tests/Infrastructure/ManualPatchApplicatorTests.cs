using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// A category that fails is isolated by PatchCategoryApplier. A manual patch runs in the same callback, and a throw
/// from harmony.Patch would leave through MBGameManager's submodule loop (no catch on v1.4.8) and skip the Harmony
/// census after it, so each one needs the same guard.
/// </summary>
[TestClass]
public class ManualPatchApplicatorTests
{
    [TestMethod]
    public void TryPatch_ThrowingPatch_IsContainedAndNamedInTheLog()
    {
        var logger = Substitute.For<IModLogger>();

        var ok = ManualPatchApplicator.TryPatch("[Feature] Target.Method", logger,
            () => throw new InvalidOperationException("no such target"));

        Assert.IsFalse(ok);
        logger.Received(1).LogError(Arg.Is<string>(s =>
            s.StartsWith("[PatchApply]") && s.Contains("[Feature] Target.Method") && s.Contains("no such target")));
    }

    [TestMethod]
    public void TryPatch_CleanPatch_RunsOnceAndLogsNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var calls = 0;

        var ok = ManualPatchApplicator.TryPatch("[Feature] Target.Method", logger, () => calls++);

        Assert.IsTrue(ok);
        Assert.AreEqual(1, calls);
        logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void EveryManualPatch_RunsInsideTheGuard()
    {
        var src = RepoPaths.ReadSource("Main/ManualPatchApplicator.cs", stripComments: true);

        // Comparing two totals leaves a slack of one, because the setup guard patches nothing. Each guard's text runs
        // to the next guard, so a patch taken out of its guard lands in the section before it and moves that count.
        const string setupGuard = "[SettlementGuards] Initialize";
        var sections = Regex.Split(src, @"\bTryPatch\(""");
        var patches = Regex.Matches(src, @"harmony\.Patch\(").Count;

        Assert.IsTrue(patches >= 8, $"Only {patches} harmony.Patch calls found; the scan broke, it did not pass.");
        Assert.AreEqual(0, Regex.Matches(sections[0], @"harmony\.Patch\(").Count,
            "A harmony.Patch before the first TryPatch: an unguarded throw aborts game init.");

        foreach (var section in sections.Skip(1))
        {
            var label = section.Substring(0, section.IndexOf('"'));
            Assert.AreEqual(label == setupGuard ? 0 : 1, Regex.Matches(section, @"harmony\.Patch\(").Count,
                $"Wrong harmony.Patch count after TryPatch \"{label}\": an unguarded throw aborts game init.");
        }
    }
}
