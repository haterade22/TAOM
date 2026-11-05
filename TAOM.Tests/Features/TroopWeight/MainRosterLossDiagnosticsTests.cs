using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight.Diagnostics;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TroopWeight;

[TestClass]
public class MainRosterLossDiagnosticsTests
{
    private const string Category = "[HarmonyPatchCategory(\"Patch_MainRosterLossDiag\")]";

    [TestMethod]
    public void FormatRemoval_NamesTickTroopRowRegularsAndChain()
    {
        var frames = new[] { "Caller.A", "Caller.B" };

        string line = MainRosterLossDiagnostics.FormatRemoval(1234, "gondor_recruit", 3, 10, 60, frames);

        Assert.AreEqual(
            "[RosterLoss] tick=1234 -3 gondor_recruit (row 10 -> 7, regulars 60 -> 57) via Caller.A <- Caller.B",
            line);
    }

    [TestMethod]
    public void FormatRemoval_NullFrames_SaysNoManagedCaller()
    {
        string line = MainRosterLossDiagnostics.FormatRemoval(1, "t", 1, 5, 20, null!);

        StringAssert.EndsWith(line, "via (no managed caller)");
    }

    [TestMethod]
    public void FormatRemoval_OnlyFilteredFrames_SaysNoManagedCaller()
    {
        var frames = new[] { "HarmonyLib.Foo.Bar", "System.Linq.Enumerable.Select" };

        string line = MainRosterLossDiagnostics.FormatRemoval(1, "t", 1, 5, 20, frames);

        StringAssert.EndsWith(line, "via (no managed caller)");
    }

    [TestMethod]
    public void FormatCallerChain_NullList_IsEmpty()
    {
        Assert.AreEqual(0, MainRosterLossDiagnostics.FormatCallerChain(null!).Count);
    }

    [TestMethod]
    public void FormatCallerChain_DropsNoiseFrames()
    {
        var frames = new List<string>
        {
            null!, "",
            "TAOM.Features.TroopWeight.Diagnostics.TroopRoster_AddToCountsAtIndex_Patch.Prefix",
            "HarmonyLib.Thing.Run",
            "MonoMod.Utils.X",
            "System.Linq.Enumerable.Select",
            "Real.Method",
        };

        var chain = MainRosterLossDiagnostics.FormatCallerChain(frames);

        CollectionAssert.AreEqual(new[] { "Real.Method" }, chain.ToList());
    }

    [TestMethod]
    public void FormatCallerChain_KeepsEngineAndTaomFramesThatLookLikeNoise()
    {
        var frames = new List<string>
        {
            "TaleWorlds.CampaignSystem.Roster.TroopRoster.AddToCountsAtIndex_Patch1",
            "TAOM.Features.TroopWeight.Hooks.Something.Method",
            "TAOM.Features.AlignmentDesertion.Hooks.AlignmentDesertionBehavior.OnDailyTickParty",
        };

        var chain = MainRosterLossDiagnostics.FormatCallerChain(frames);

        CollectionAssert.AreEqual(frames, chain.ToList());
    }

    [TestMethod]
    public void FormatCallerChain_CapsAtEight()
    {
        var frames = Enumerable.Range(1, 12).Select(i => "Real" + i + ".M").ToList();

        var chain = MainRosterLossDiagnostics.FormatCallerChain(frames);

        CollectionAssert.AreEqual(frames.Take(8).ToList(), chain.ToList());
    }

    [TestMethod]
    public void FormatCallerChain_NoiseBeforeTenRealFrames_KeepsEightReal()
    {
        // A real capture starts with the diagnostic's own frames, so the filter must run before the cap.
        var real = Enumerable.Range(1, 10).Select(i => "Real" + i + ".M").ToList();
        var frames = new List<string>
        {
            "TAOM.Features.TroopWeight.Diagnostics.TroopRoster_AddToCountsAtIndex_Patch.LogMainRemoval",
            "TAOM.Features.TroopWeight.Diagnostics.TroopRoster_AddToCountsAtIndex_Patch.Prefix",
            "HarmonyLib.Something.Method",
        };
        frames.AddRange(real);

        var chain = MainRosterLossDiagnostics.FormatCallerChain(frames);

        CollectionAssert.AreEqual(real.Take(8).ToList(), chain.ToList());
    }

    [TestMethod]
    public void SubModule_AppliesTheRosterLossPatch()
    {
        string source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(source, "TryPatchCategory(\"Patch_MainRosterLossDiag\")");
        StringAssert.Contains(source, "TroopRoster_AddToCountsAtIndex_Patch.Initialize(");
    }

    [TestMethod]
    public void BothPatchFiles_JoinTheDiagnosticCategory()
    {
        string dir = "Main/Features/TroopWeight/Diagnostics/";

        StringAssert.Contains(
            RepoPaths.ReadSource(dir + "TroopRoster_AddToCountsAtIndex_Patch.cs", stripComments: true), Category);
        StringAssert.Contains(
            RepoPaths.ReadSource(dir + "TroopRoster_SetElementNumber_Patch.cs", stripComments: true), Category);
    }
}
