using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// TAOM.Dependencies runs PatchShield's second pass in its own OnGameInitializationFinished, which the engine calls
/// before TAOM's, so the pass never saw TAOM's once-per-process game-initialization batch: about 240 methods stayed
/// unshielded for the first game of each session, and the next game start's pass attached them (about 0.56 s,
/// perf audit 2026-11-13 row 10). TAOM now runs a pass itself right after that batch and its manual patches.
/// PatchShield.Install attaches only methods it has not seen and checks the co-op and flag skips itself.
/// </summary>
[TestClass]
public class PatchShieldAfterGameInitTests
{
    private const string Pass = "TAOM.Dependencies.Foundation.PatchShield.Install();";

    [TestMethod]
    public void OnGameInitializationFinished_ShieldsAfterTheBatchAndTheManualPatches_BeforeTheCensus()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int batch = src.IndexOf("FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);", StringComparison.Ordinal);
        int manual = src.IndexOf("ManualPatchApplicator.ApplyAll(_harmony);", StringComparison.Ordinal);
        int pass = src.IndexOf(Pass, StringComparison.Ordinal);
        int census = src.IndexOf("HarmonyCensusWriter.Write(", StringComparison.Ordinal);

        Assert.IsTrue(batch >= 0 && manual >= 0 && census >= 0, "the anchors are still present");
        Assert.IsTrue(pass >= 0, "TAOM runs a PatchShield pass of its own");
        Assert.AreEqual(-1, src.IndexOf(Pass, pass + Pass.Length, StringComparison.Ordinal), "exactly one pass");
        Assert.IsTrue(batch < manual && manual < pass && pass < census,
            "the pass follows the game-initialization batch and the manual patches, and precedes the census");
    }

    [TestMethod]
    public void ThePass_IsWrappedSoAShieldFaultNeverStopsTheGameLoading()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int pass = src.IndexOf(Pass, StringComparison.Ordinal);
        Assert.IsTrue(pass >= 0);
        var before = src.Substring(Math.Max(0, pass - 40), Math.Min(40, pass));
        StringAssert.Contains(before, "try");
        StringAssert.Contains(src.Substring(pass, 200), "catch");
    }
}
