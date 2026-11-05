using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
