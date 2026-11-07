using System;
using System.Collections.Generic;
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

        var ok = ManualPatchApplicator.TryPatch("[Feature] Target.Method", logger, new List<string>(),
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
        var failed = new List<string>();

        var ok = ManualPatchApplicator.TryPatch("[Feature] Target.Method", logger, failed, () => calls++);

        Assert.IsTrue(ok);
        Assert.AreEqual(1, calls);
        logger.DidNotReceiveWithAnyArgs().LogError(default!);
        Assert.AreEqual(0, failed.Count);
    }

    [TestMethod]
    public void TryPatch_ThrowingPatch_AddsItsLabelToFailed()
    {
        var failed = new List<string> { "[Feature] Earlier" };

        ManualPatchApplicator.TryPatch("[Feature] Target.Method", Substitute.For<IModLogger>(), failed,
            () => throw new InvalidOperationException("no such target"));

        CollectionAssert.AreEqual(new[] { "[Feature] Earlier", "[Feature] Target.Method" }, failed);
    }

    // ApplyAll needs the IoC container and a real Harmony, so the wiring is read from source: a guard that collects
    // into anything but the returned list drops its failure from the report with no compile error.
    [TestMethod]
    public void ApplyAll_EveryTryPatch_CollectsIntoTheReturnedList()
    {
        var src = RepoPaths.ReadSource("Main/ManualPatchApplicator.cs", stripComments: true);

        var guards = Regex.Matches(src, @"\bTryPatch\(""").Count;
        Assert.IsTrue(guards >= 9, $"Only {guards} TryPatch calls found; the scan broke, it did not pass.");
        Assert.AreEqual(guards, Regex.Matches(src, @"\bTryPatch\(""[^""]+"", logger, failed, ").Count,
            "A TryPatch does not collect into ApplyAll's list.");
        StringAssert.Contains(src, "return failed;");
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
