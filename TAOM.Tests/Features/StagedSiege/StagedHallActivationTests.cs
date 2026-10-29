using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The hall arming decision: one test per guard, in order.</summary>
[TestClass]
public class StagedHallActivationTests
{
    private static HallVerdict Decide(bool isHall = true, bool campaign = true, bool client = false, bool inHall = true,
        bool enabled = true, bool wallsScene = true, bool markers = true) =>
        StagedHallActivation.Decide(isHall, campaign, client, inHall, enabled, wallsScene, markers);

    [TestMethod]
    public void AllGuardsPass_Armed() => Assert.AreEqual(HallVerdict.Armed, Decide());

    [TestMethod]
    public void NotAHallMission() => Assert.AreEqual(HallVerdict.NotAHallMission, Decide(isHall: false));

    [TestMethod]
    public void NoCampaign() => Assert.AreEqual(HallVerdict.NoCampaign, Decide(campaign: false));

    [TestMethod]
    public void Client() => Assert.AreEqual(HallVerdict.Client, Decide(client: true));

    [TestMethod]
    public void NotInTheLordsHall() => Assert.AreEqual(HallVerdict.NotInTheLordsHall, Decide(inHall: false));

    [TestMethod]
    public void Disabled() => Assert.AreEqual(HallVerdict.Disabled, Decide(enabled: false));

    [TestMethod]
    public void WallsSceneNotRecorded() => Assert.AreEqual(HallVerdict.WallsSceneNotRecorded, Decide(wallsScene: false));

    [TestMethod]
    public void NoMarkers() => Assert.AreEqual(HallVerdict.NoMarkers, Decide(markers: false));

    [TestMethod]
    public void GuardsRunInOrder()
    {
        Assert.AreEqual(HallVerdict.NotAHallMission, Decide(false, false, true, false, false, false, false));
        Assert.AreEqual(HallVerdict.NoCampaign, Decide(true, false, true, false, false, false, false));
        Assert.AreEqual(HallVerdict.Client, Decide(true, true, true, false, false, false, false));
        Assert.AreEqual(HallVerdict.NotInTheLordsHall, Decide(true, true, false, false, false, false, false));
        Assert.AreEqual(HallVerdict.Disabled, Decide(true, true, false, true, false, false, false));
        Assert.AreEqual(HallVerdict.WallsSceneNotRecorded, Decide(true, true, false, true, true, false, false));
    }
}
