using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The slot's two run-report records (#1054): what the HUD showed, and the campaign settlement of the last staged battle.</summary>
[TestClass]
public class StagedSiegeSlotRecordTests
{
    private object _token = null!;
    private StagedSiegeService _service = null!;
    private StagedHallService _hall = null!;

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearCampaignSettlement();
        _token = new object();
        var walls = Substitute.For<IStagedSiegeMissionAdapter>();
        walls.MissionToken.Returns(_token);
        _service = new StagedSiegeService(walls, StagedSiegeSceneCatalog.FromText(null, Substitute.For<IModLogger>()), Substitute.For<IModLogger>());
        var hall = Substitute.For<IStagedHallAdapter>();
        hall.MissionToken.Returns(_token);
        _hall = new StagedHallService(hall, StagedSiegeSceneCatalog.FromText(null, Substitute.For<IModLogger>()), Substitute.For<IModLogger>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearCampaignSettlement();
    }

    [TestMethod]
    public void HudFor_NothingRecorded_IsNull()
    {
        StagedSiegeSlot.Arm(_service);

        Assert.IsNull(StagedSiegeSlot.HudFor(_token));
    }

    [TestMethod]
    public void RecordHud_KeepsEachPairOnceAndTheMarkerLetters()
    {
        StagedSiegeSlot.Arm(_service);

        StagedSiegeSlot.RecordHud(_token, "A:Take");
        StagedSiegeSlot.RecordHud(_token, "A:Take");
        StagedSiegeSlot.RecordHud(_token, "B:Take");
        StagedSiegeSlot.RecordHudMarker(_token, "A");
        StagedSiegeSlot.RecordHudMarker(_token, "A");

        var record = StagedSiegeSlot.HudFor(_token)!;
        CollectionAssert.AreEquivalent(new[] { "A:Take", "B:Take" }, record.Pairs.ToList());
        CollectionAssert.AreEqual(new[] { "A" }, record.Markers.ToList());
    }

    [TestMethod]
    public void HudFor_AnotherMission_IsNull()
    {
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.RecordHud(_token, "A:Take");

        Assert.IsNull(StagedSiegeSlot.HudFor(new object()));
    }

    [TestMethod]
    public void RecordHud_ForANewMissionToken_StartsAFreshRecord()
    {
        StagedSiegeSlot.RecordHud(_token, "A:Take");
        var next = new object();

        StagedSiegeSlot.RecordHud(next, "B:Hold");

        Assert.IsNull(StagedSiegeSlot.HudFor(_token));
        CollectionAssert.AreEqual(new[] { "B:Hold" }, StagedSiegeSlot.HudFor(next)!.Pairs.ToList());
    }

    [TestMethod]
    public void Arm_ClearsTheHudRecord()
    {
        StagedSiegeSlot.RecordHud(_token, "A:Take");

        StagedSiegeSlot.Arm(_service);

        Assert.IsNull(StagedSiegeSlot.HudFor(_token));
    }

    [TestMethod]
    public void ArmHall_ClearsTheHudRecord()
    {
        StagedSiegeSlot.RecordHud(_token, "E:TakeHall");

        StagedSiegeSlot.ArmHall(_hall);

        Assert.IsNull(StagedSiegeSlot.HudFor(_token));
    }

    [TestMethod]
    public void HudFor_SurvivesTheServiceClearingItself()
    {
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.RecordHud(_token, "A:Take");

        StagedSiegeSlot.ClearIf(_token);

        Assert.IsNotNull(StagedSiegeSlot.HudFor(_token), "the report is written after the slot is cleared at shutdown");
    }

    [TestMethod]
    public void ClearArmed_ClearsTheHudRecord_SoNoFinishedMissionStaysReferenced()
    {
        StagedSiegeSlot.RecordHud(_token, "A:Take");

        StagedSiegeSlot.ClearArmed();

        Assert.IsNull(StagedSiegeSlot.HudFor(_token));
    }

    [TestMethod]
    public void CampaignSettlement_RecordedThenCleared()
    {
        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);

        StagedSiegeSlot.RecordCampaignSettlement("castle_sturgia_a");
        Assert.AreEqual("castle_sturgia_a", StagedSiegeSlot.CampaignSettlementId);

        StagedSiegeSlot.ClearCampaignSettlement();
        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void CampaignSettlement_ClearedWhenAWallsServiceArms()
    {
        StagedSiegeSlot.RecordCampaignSettlement("castle_old");

        StagedSiegeSlot.Arm(_service);

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void CampaignSettlement_ClearedWhenAHallServiceArms()
    {
        StagedSiegeSlot.RecordCampaignSettlement("castle_old");

        StagedSiegeSlot.ArmHall(_hall);

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void CampaignSettlement_SurvivesClearArmed()
    {
        // OnCreated of the hall mission calls ClearArmed after the walls fight recorded the settlement.
        StagedSiegeSlot.RecordCampaignSettlement("castle_sturgia_a");

        StagedSiegeSlot.ClearArmed();

        Assert.AreEqual("castle_sturgia_a", StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void RecordCampaignSettlement_EmptyId_RecordsNothing()
    {
        StagedSiegeSlot.RecordCampaignSettlement(string.Empty);

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }
}
