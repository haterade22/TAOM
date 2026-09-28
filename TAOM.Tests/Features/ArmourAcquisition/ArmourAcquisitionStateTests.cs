using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmourAcquisition;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The feature's campaign state rides one flat Dictionary&lt;string, string&gt; in SyncData (the
/// SiegeDefense shape, no SaveableTypeDefiner). It must round-trip, shrug off a hand-damaged or
/// future-version entry, and reset completely for a new campaign, because the state object is a
/// process singleton and a new campaign never calls SyncData to load
/// (csharp-architecture.md, "Singleton Services Holding Per-Campaign State").
/// </summary>
[TestClass]
public class ArmourAcquisitionStateTests
{
    private static ArmourAcquisitionState Populated()
    {
        var state = new ArmourAcquisitionState();
        state.HarnessStage["main_hero"] = ArmourAcquisitionState.HarnessReady;
        state.HarnessDeclinedDay["main_hero"] = 40;
        state.LordEventLastDay["main_hero"] = 77;
        state.VisitUntilDay["town_G1"] = 120;
        return state;
    }

    [TestMethod]
    public void EncodeDecode_RoundTripsEveryField()
    {
        var restored = new ArmourAcquisitionState();
        restored.Decode(Populated().Encode());

        Assert.AreEqual(ArmourAcquisitionState.HarnessReady, restored.HarnessStage["main_hero"]);
        Assert.AreEqual(40, restored.HarnessDeclinedDay["main_hero"]);
        Assert.AreEqual(77, restored.LordEventLastDay["main_hero"]);
        Assert.AreEqual(120, restored.VisitUntilDay["town_G1"]);
    }

    [TestMethod]
    public void Decode_ReplacesWhateverWasThere()
    {
        var state = Populated();

        state.Decode(new Dictionary<string, string> { ["v"] = "1" });

        Assert.AreEqual(0, state.HarnessStage.Count);
        Assert.AreEqual(0, state.VisitUntilDay.Count);
    }

    [TestMethod]
    public void Decode_Null_IsAnEmptyState()
    {
        var state = Populated();

        state.Decode(null);

        Assert.AreEqual(0, state.LordEventLastDay.Count);
    }

    [TestMethod]
    public void Decode_NonNumericValue_IsSkippedAndCounted()
    {
        var state = new ArmourAcquisitionState();

        var skipped = state.Decode(new Dictionary<string, string>
        {
            ["stage|main_hero"] = "two",
            ["visit|town_A"] = "12",
        });

        Assert.AreEqual(1, skipped);
        Assert.IsFalse(state.HarnessStage.ContainsKey("main_hero"));
        Assert.AreEqual(12, state.VisitUntilDay["town_A"]);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("3")]
    [DataRow("9")]
    public void Decode_HarnessStageNeitherReadyNorClaimed_IsSkipped(string raw)
    {
        var state = new ArmourAcquisitionState();

        var skipped = state.Decode(new Dictionary<string, string> { ["stage|h"] = raw });

        Assert.AreEqual(1, skipped);
        Assert.IsFalse(state.HarnessStage.ContainsKey("h"));
    }

    [TestMethod]
    public void Decode_UnknownKeys_AreIgnoredNotCounted()
    {
        var state = new ArmourAcquisitionState();

        var skipped = state.Decode(new Dictionary<string, string> { ["future|x"] = "1", ["v"] = "2", ["swept"] = "1" });

        Assert.AreEqual(0, skipped);
    }

    [TestMethod]
    public void Reset_ClearsEverything()
    {
        var state = Populated();

        state.Reset();

        Assert.AreEqual(0, state.HarnessStage.Count + state.HarnessDeclinedDay.Count + state.LordEventLastDay.Count + state.VisitUntilDay.Count);
    }

    [TestMethod]
    public void Encode_EmptyState_CarriesOnlyTheVersion()
    {
        var encoded = new ArmourAcquisitionState().Encode();

        Assert.AreEqual(1, encoded.Count);
        Assert.AreEqual("1", encoded["v"]);
    }
}
