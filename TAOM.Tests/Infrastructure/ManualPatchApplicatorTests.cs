using System;
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

        var patches = Regex.Matches(src, @"harmony\.Patch\(").Count;
        var guards = Regex.Matches(src, @"\bTryPatch\(""").Count;

        Assert.IsTrue(patches >= 8, $"Only {patches} harmony.Patch calls found; the scan broke, it did not pass.");
        Assert.IsTrue(guards >= patches,
            $"{patches} harmony.Patch calls but {guards} TryPatch guards: an unguarded throw aborts game init.");
    }
}
