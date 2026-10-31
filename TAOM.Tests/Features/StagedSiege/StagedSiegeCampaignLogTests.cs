using System;
using System.Linq;
using Arg = NSubstitute.Arg;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// What became of the settlement after a staged battle (#1054): the behavior logs the map event end and the owner change, only for
/// the settlement the slot recorded. It saves nothing and its constructor is the per-campaign reset. A behavior needs the engine's
/// types, so these run with the game assemblies; the engine-facing handlers only pull plain values out and call the internal methods.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StagedSiegeCampaignLogTests
{
    private const string Castle = "castle_sturgia_a";

    private IModLogger _logger = null!;
    private StagedSiegeCampaignLog _sut = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearCampaignSettlement();
        _logger = Substitute.For<IModLogger>();
        _sut = new StagedSiegeCampaignLog(_logger);
    }

    [TestCleanup]
    public void Cleanup() => StagedSiegeSlot.ClearCampaignSettlement();

    [TestMethod]
    public void Constructor_ClearsAStaleRecord_ForANewCampaign()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _ = new StagedSiegeCampaignLog(_logger);

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void MapEventEnded_RecordedSettlement_LogsTheOutcomeAndClearsTheRecord()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _sut.HandleMapEventEnded(Castle, "Attacker", 12, 40, 100.0);

        _logger.Received(1).LogInfo("[StagedSiege] campaign: map event at castle_sturgia_a ended, winner Attacker, casualties attacker 12 defender 40");
        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void MapEventEnded_NoWinner_SaysNone()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _sut.HandleMapEventEnded(Castle, "None", 0, 0, 100.0);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("winner none,")));
    }

    [TestMethod]
    public void MapEventEnded_AnotherSettlement_LogsNothingAndKeepsTheRecord()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _sut.HandleMapEventEnded("town_other", "Attacker", 1, 2, 100.0);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
        Assert.AreEqual(Castle, StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void MapEventEnded_NothingRecorded_LogsNothing()
    {
        _sut.HandleMapEventEnded(Castle, "Attacker", 1, 2, 100.0);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void MapEventEnded_FieldBattleWithNoSettlement_LogsNothing()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _sut.HandleMapEventEnded(null, "Attacker", 1, 2, 100.0);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
        Assert.AreEqual(Castle, StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void OwnerChanged_RecordedSettlement_LogsBothClansAndTheDetail()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _sut.HandleOwnerChanged(Castle, "clan_sturgia_1", "player_faction", "BySiege", 100.0);

        _logger.Received(1).LogInfo("[StagedSiege] campaign: castle_sturgia_a owner clan_sturgia_1 -> player_faction (BySiege)");
        Assert.AreEqual(Castle, StagedSiegeSlot.CampaignSettlementId, "only the map event end clears the record");
    }

    [TestMethod]
    public void OwnerChanged_AnotherSettlement_LogsNothing()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _sut.HandleOwnerChanged("town_other", "a", "b", "BySiege", 100.0);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void OwnerChanged_NothingRecorded_LogsNothing()
    {
        _sut.HandleOwnerChanged(Castle, "a", "b", "BySiege", 100.0);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void OwnerChanged_AfterTheMapEventEndAtTheSameTime_IsLogged_AsVanillaOrders()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);

        _sut.HandleMapEventEnded(Castle, "Attacker", 12, 40, 500.0);
        _sut.HandleOwnerChanged(Castle, "clan_sturgia_1", "player_faction", "BySiege", 500.0);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("map event at castle_sturgia_a ended")));
        _logger.Received(1).LogInfo("[StagedSiege] campaign: castle_sturgia_a owner clan_sturgia_1 -> player_faction (BySiege)");
        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void OwnerChanged_SecondChangeAtTheSameTime_IsNotLoggedAgain()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);
        _sut.HandleMapEventEnded(Castle, "Attacker", 1, 2, 500.0);
        _sut.HandleOwnerChanged(Castle, "a", "b", "BySiege", 500.0);

        _sut.HandleOwnerChanged(Castle, "b", "c", "ByGift", 500.0);

        _logger.Received(2).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void OwnerChanged_AtALaterTime_IsNotLogged()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);
        _sut.HandleMapEventEnded(Castle, "Attacker", 1, 2, 500.0);
        _logger.ClearReceivedCalls();

        _sut.HandleOwnerChanged(Castle, "a", "b", "ByKingDecision", 501.0);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void OwnerChanged_AnotherSettlementAtTheSameTime_IsNotLogged()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);
        _sut.HandleMapEventEnded(Castle, "Attacker", 1, 2, 500.0);
        _logger.ClearReceivedCalls();

        _sut.HandleOwnerChanged("town_other", "a", "b", "BySiege", 500.0);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void Handlers_LoggerThrows_WarnOnceAndNeverThrow()
    {
        StagedSiegeSlot.RecordCampaignSettlement(Castle);
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log sink gone"));

        _sut.HandleOwnerChanged(Castle, "a", "b", "BySiege", 100.0);
        _sut.HandleMapEventEnded(Castle, "Attacker", 1, 2, 100.0);

        _logger.Received(2).LogWarning(Arg.Is<string>(s => s.StartsWith("[StagedSiege] campaign log failed")));
    }

    [TestMethod]
    public void SyncData_SavesAndLoadsNothing()
    {
        var store = Substitute.For<IDataStore>();

        _sut.SyncData(store);

        Assert.AreEqual(0, store.ReceivedCalls().Count());
    }

    [TestMethod]
    public void RegisterEvents_ListensToBothEventsWithNonSerializedListeners()
    {
        var source = RepoPaths.ReadSource("Main/Features/StagedSiege/StagedSiegeCampaignLog.cs", stripComments: true);

        StringAssert.Contains(source, "CampaignEvents.MapEventEnded.AddNonSerializedListener(this");
        StringAssert.Contains(source, "CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this");
    }

    [TestMethod]
    public void Module_BuildsAFreshBehaviorForEveryCampaignStart()
    {
        // The constructor is the per-campaign reset of the settlement record, so the declaration must never hand out one shared instance.
        using var container = new Container();
        container.RegisterInstance(_logger);
        var decl = new StagedSiegeModule().CampaignBehaviors[0];

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.AreNotSame(first, second);
        Assert.IsInstanceOfType(first, typeof(StagedSiegeCampaignLog));
    }

    [TestMethod]
    public void Module_DeclaresTheBehavior_AndStillSavesNothing()
    {
        var module = new StagedSiegeModule();

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(StagedSiegeCampaignLog), module.CampaignBehaviors[0].BehaviorType);
        Assert.IsFalse(module.OwnsSaveData);
    }
}
