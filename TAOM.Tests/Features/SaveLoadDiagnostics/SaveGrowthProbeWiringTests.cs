using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SaveLoadDiagnostics.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SaveLoadDiagnostics;

// Main/SubModule.cs cannot run in a test, and a missed Initialize or category string is a dead probe
// with no error, so its wiring is pinned by source text (comments stripped).
[TestClass]
public class SaveGrowthProbeWiringTests
{
    private static string SubModuleSource() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    [TestMethod]
    public void SubModule_OnGameEnd_NotesTheEndedCampaign()
    {
        StringAssert.Contains(SubModuleSource(), ".NoteGameEnded(endedCampaign)");
    }

    [TestMethod]
    public void SubModule_StampSaveLoadPhase_UsesProcessContext()
    {
        StringAssert.Contains(SubModuleSource(), "saveLoad.LogPhase(phase, saveLoad.ProcessContext());");
    }

    [TestMethod]
    public void SubModule_Patch61Block_LogsTheRuntimeLine()
    {
        var source = SubModuleSource();
        StringAssert.Contains(source, "SaveLoadPhase.Runtime");
        StringAssert.Contains(source, "SaveLoadTokens.RuntimeHost()");
    }

    [TestMethod]
    public void SubModule_AppliesAndInitializesTheCountHooks()
    {
        var source = SubModuleSource();
        StringAssert.Contains(source, "\"Patch61_SaveLoadDiagnostics_SaveGraph\",");
        StringAssert.Contains(source, "\"Patch61_SaveLoadDiagnostics_LoadCounts\",");
        StringAssert.Contains(source, "SaveContext_CollectObjects_Patch.Initialize(");
        StringAssert.Contains(source, "LoadContext_Load_Patch.Initialize(");
    }

    // A typo on either side is a dead patch with no error.
    [TestMethod]
    public void CountHookCategoryAttributes_MatchTheStringsSubModuleApplies()
    {
        Assert.AreEqual("Patch61_SaveLoadDiagnostics_SaveGraph", CategoryOf(typeof(SaveContext_CollectObjects_Patch)));
        Assert.AreEqual("Patch61_SaveLoadDiagnostics_LoadCounts", CategoryOf(typeof(LoadContext_Load_Patch)));
    }

    private static string? CategoryOf(Type hook) =>
        hook.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .SingleOrDefault()
            ?.info.category;
}
