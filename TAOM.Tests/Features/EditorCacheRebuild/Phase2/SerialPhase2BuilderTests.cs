using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Phase2;

namespace TAOM.Tests.Features.EditorCacheRebuild.Phase2;

[TestClass]
public class SerialPhase2BuilderTests
{
    private IModLogger _logger = null!;
    private INavigationCacheAdapter _adapter = null!;
    private SerialPhase2Builder _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _adapter = Substitute.For<INavigationCacheAdapter>();
        _sut = new SerialPhase2Builder(_logger);
    }

    private static ISettlementDataHolder MakeFortification(string id) => MakeSettlement(id, isFortification: true);
    private static ISettlementDataHolder MakeVillage(string id) => MakeSettlement(id, isFortification: false);

    private static ISettlementDataHolder MakeSettlement(string id, bool isFortification)
    {
        var s = Substitute.For<ISettlementDataHolder>();
        s.StringId.Returns(id);
        s.IsFortification.Returns(isFortification);
        return s;
    }

    [TestMethod]
    public void Run_AllNeighbors_AddsEveryPair()
    {
        var s1 = MakeFortification("a");
        var s2 = MakeFortification("b");
        var s3 = MakeFortification("c");
        var collection = new SettlementCollection(new List<ISettlementDataHolder> { s1, s2, s3 });
        _adapter.GetFortificationsForNeighborDetection().Returns(collection);
        _adapter.CheckBeingNeighbor(Arg.Any<SettlementCollection>(), Arg.Any<ISettlementDataHolder>(), Arg.Any<ISettlementDataHolder>())
            .Returns(true);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(3, result.NeighborPairsAdded);
        _adapter.Received(1).AddNeighbor(s1, s2);
        _adapter.Received(1).AddNeighbor(s1, s3);
        _adapter.Received(1).AddNeighbor(s2, s3);
    }

    [TestMethod]
    public void Run_NoNeighbors_AddsNone()
    {
        var s1 = MakeFortification("a");
        var s2 = MakeFortification("b");
        var collection = new SettlementCollection(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.GetFortificationsForNeighborDetection().Returns(collection);
        _adapter.CheckBeingNeighbor(Arg.Any<SettlementCollection>(), Arg.Any<ISettlementDataHolder>(), Arg.Any<ISettlementDataHolder>())
            .Returns(false);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(0, result.NeighborPairsAdded);
        _adapter.DidNotReceive().AddNeighbor(Arg.Any<ISettlementDataHolder>(), Arg.Any<ISettlementDataHolder>());
    }

    [TestMethod]
    public void Run_NonFortifications_Skipped()
    {
        var s1 = MakeFortification("a");
        var s2 = MakeVillage("b");
        var s3 = MakeFortification("c");
        var collection = new SettlementCollection(new List<ISettlementDataHolder> { s1, s2, s3 });
        _adapter.GetFortificationsForNeighborDetection().Returns(collection);
        _adapter.CheckBeingNeighbor(Arg.Any<SettlementCollection>(), Arg.Any<ISettlementDataHolder>(), Arg.Any<ISettlementDataHolder>())
            .Returns(true);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(1, result.NeighborPairsAdded);
        _adapter.Received(1).AddNeighbor(s1, s3);
        _adapter.DidNotReceive().AddNeighbor(s1, s2);
        _adapter.DidNotReceive().AddNeighbor(s2, s3);
    }

    [TestMethod]
    public void Run_FortificationConsideredCount_ReturnedCorrectly()
    {
        var collection = new SettlementCollection(new List<ISettlementDataHolder>
        {
            MakeFortification("a"),
            MakeFortification("b"),
            MakeFortification("c"),
        });
        _adapter.GetFortificationsForNeighborDetection().Returns(collection);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(3, result.FortificationsConsidered);
    }

    [TestMethod]
    [ExpectedException(typeof(System.OperationCanceledException))]
    public void Run_CancelledMidLoop_Throws()
    {
        var items = new List<ISettlementDataHolder>();
        for (int i = 0; i < 100; i++) items.Add(MakeFortification($"s{i}"));
        _adapter.GetFortificationsForNeighborDetection().Returns(new SettlementCollection(items));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _sut.Run(_adapter, cts.Token);
    }

    [TestMethod]
    public void Run_EmptyFortifications_ReturnsZero()
    {
        _adapter.GetFortificationsForNeighborDetection()
            .Returns(new SettlementCollection(new List<ISettlementDataHolder>()));

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(0, result.NeighborPairsAdded);
        Assert.AreEqual(0, result.FortificationsConsidered);
    }
}
