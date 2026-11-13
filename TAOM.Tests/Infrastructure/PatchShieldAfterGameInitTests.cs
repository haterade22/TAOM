using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// TAOM.Dependencies runs PatchShield's second pass in its own OnGameInitializationFinished, which the engine calls
/// before TAOM's, so the pass never saw TAOM's once-per-process game-initialization batch: those methods stayed
/// unshielded for the first game of each session, and the next game start's pass attached them. TAOM now runs a pass
/// itself right after that batch and its manual patches. PatchShield.Install attaches only methods it has not seen
/// and checks the co-op and flag skips itself. The attach cost has not been measured for this pass: it moves to the
/// first load, so a session of one game pays it once at that load. The checks read OnGameInitializationFinished's
/// body only, in source order.
/// </summary>
[TestClass]
public class PatchShieldAfterGameInitTests
{
    private const string Pass = "TAOM.Dependencies.Foundation.PatchShield.Install();";
    private const string Marker = "PatchShield.Install (pass after TAOM GameInit batch)";

    [TestMethod]
    public void OnGameInitializationFinished_ShieldsAfterTheBatchAndTheManualPatches_BeforeTheCensus()
    {
        var body = GameInitBody();
        int once = Index(body, "_gameInitPatchesApplied = true;");
        int batch = Index(body, "FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);");
        int manual = Index(body, "ManualPatchApplicator.ApplyAll(_harmony);");
        int pass = Index(body, Pass);
        int census = Index(body, "HarmonyCensusWriter.Write(");

        Assert.IsTrue(once < batch && batch < manual && manual < pass && pass < census,
            "the pass sits in the once-per-process block, after the batch and the manual patches, before the census");
    }

    [TestMethod]
    public void OnGameInitializationFinished_RunsExactlyOnePass_AndSpellsItOnceInAnyForm()
    {
        var body = GameInitBody();
        Assert.AreEqual(1, Regex.Matches(body, @"PatchShield\s*\.\s*Install\s*\(\s*\)").Count, "exactly one pass");
    }

    [TestMethod]
    public void OnGameInitializationFinished_NothingIsPatchedBetweenThePassAndTheEndOfTheMethod()
    {
        var body = GameInitBody();
        var after = body.Substring(Index(body, Pass) + Pass.Length);
        foreach (var patching in new[] { "TryPatchCategory", "ManualPatchApplicator.", ".Patch(", "RunPhase(", "_harmony" })
            Assert.AreEqual(-1, after.IndexOf(patching, StringComparison.Ordinal), "a patch applied after the pass is unshielded in the first game: " + patching);
    }

    [TestMethod]
    public void OnGameInitializationFinished_NoReturnOrConditionBetweenTheBatchAndThePass()
    {
        var body = GameInitBody();
        var between = body.Substring(Index(body, "_gameInitPatchesApplied = true;"), Index(body, Pass) - Index(body, "_gameInitPatchesApplied = true;"));
        Assert.IsFalse(Regex.IsMatch(between, @"\breturn\b"), "an early return would skip the pass");
        int manual = Index(body, "onceStamp?.Mark(\"manual_patches\");");
        var guarded = body.Substring(manual, Index(body, Pass) - manual);
        Assert.IsFalse(Regex.IsMatch(guarded, @"\b(if|while|for|foreach|switch)\s*\("), "the pass is not conditional");
    }

    [TestMethod]
    public void ThePass_IsWrappedSoAShieldFaultNeverStopsTheGameLoading()
    {
        var body = GameInitBody();
        int pass = Index(body, Pass);
        StringAssert.Contains(body.Substring(Math.Max(0, pass - 200), Math.Min(200, pass)), "try");
        StringAssert.Contains(body.Substring(pass, 200), "catch");
    }

    [TestMethod]
    public void ThePass_WritesItsDiagMarkerBeforeTheCall_LikePassesOneAndTwo()
    {
        var body = GameInitBody();
        int marker = Index(body, Marker);
        int pass = Index(body, Pass);
        Assert.IsTrue(marker < pass, "the marker is written before the pass runs");
        StringAssert.Contains(body.Substring(marker - 80, 80), "DiagLog.Log(");
    }

    private static int Index(string text, string anchor)
    {
        int at = text.IndexOf(anchor, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, "OnGameInitializationFinished no longer contains: " + anchor);
        return at;
    }

    private static string GameInitBody()
    {
        const string signature = "public override void OnGameInitializationFinished(Game game)";
        var code = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SubModule.cs no longer declares " + signature);

        var open = code.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            if (code[i] == '{') depth++;
            else if (code[i] == '}' && --depth == 0)
                return code.Substring(open, i - open + 1);
        }

        Assert.Fail("Unbalanced braces after " + signature);
        return string.Empty;
    }
}
