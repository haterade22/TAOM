using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.SaveLoadDiagnostics;

// Log contract for the save-growth probes (plan 114): players upload these lines and the investigation
// greps them, and an unknown value must read as unknown, never as a plausible 0 or false.
[TestClass]
public class SaveLoadTokensTests
{
    [TestMethod]
    public void LoadRequested_CarriesInGameAndCountsAfterTheName()
    {
        Assert.AreEqual(
            "name='S' inGame=true loadsThisProcess=2 inGameLoads=1 gc=1/2/3",
            SaveLoadTokens.LoadRequested("S", inGame: true, loads: 2, inGameLoads: 1, memory: "gc=1/2/3"));
    }

    [TestMethod]
    public void ProcessContext_NoEndedCampaign_SaysNoneWithoutGen2Token()
    {
        Assert.AreEqual(
            "loadsThisProcess=3 inGameLoads=2 prevCampaign=none gc=x",
            SaveLoadTokens.ProcessContext(3, 2, PreviousCampaignState.None, 0, "gc=x"));
    }

    [TestMethod]
    public void ProcessContext_EndedCampaignCollected_SaysCollectedAndGen2Delta()
    {
        Assert.AreEqual(
            "loadsThisProcess=3 inGameLoads=2 prevCampaign=collected gen2Since=4 gc=x",
            SaveLoadTokens.ProcessContext(3, 2, PreviousCampaignState.Collected, 4, "gc=x"));
    }

    [TestMethod]
    public void ProcessContext_EndedCampaignAlive_SaysAlive()
    {
        Assert.AreEqual(
            "loadsThisProcess=3 inGameLoads=2 prevCampaign=alive gen2Since=4 gc=x",
            SaveLoadTokens.ProcessContext(3, 2, PreviousCampaignState.Alive, 4, "gc=x"));
    }

    [TestMethod]
    public void RuntimeHost_UnknownValues_PrintQuestionMarksNotZeros()
    {
        Assert.AreEqual(
            "framework='Mono 6.12' clr=? serverGC=? latency=Interactive is64Bit=?",
            SaveLoadTokens.RuntimeHost("Mono 6.12", "?", null, "Interactive", null));
    }

    [TestMethod]
    public void SaveCollected_FormatsCountsAndCollectMs()
    {
        Assert.AreEqual("objects=10 containers=20 strings=30 collectMs=4601", SaveLoadTokens.SaveCollected(10, 20, 30, 4601));
    }

    [TestMethod]
    public void SaveCollected_UnreadCount_IsOmittedNotZero()
    {
        Assert.AreEqual("containers=20 collectMs=5", SaveLoadTokens.SaveCollected(null, 20, null, 5));
    }

    [TestMethod]
    public void LoadCounts_FormatsCounts()
    {
        Assert.AreEqual("objects=1 containers=2 strings=3", SaveLoadTokens.LoadCounts(1, 2, 3));
        Assert.AreEqual("counts=<unavailable>", SaveLoadTokens.LoadCounts(null, null, null));
    }
}
