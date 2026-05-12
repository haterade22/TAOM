using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Phase1;

namespace TAOM.Tests.Features.EditorCacheRebuild.Phase1;

[TestClass]
public class SerialPhase1BuilderTests
{
    private IModLogger _logger = null!;
    private INavigationCacheAdapter _adapter = null!;
    private SerialPhase1Builder _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _adapter = Substitute.For<INavigationCacheAdapter>();
        _sut = new SerialPhase1Builder(_logger);
    }

    private static ISettlementDataHolder MakeSettlement(string id, bool hasPort = false, bool isFortification = false)
    {
        var s = Substitute.For<ISettlementDataHolder>();
        s.StringId.Returns(id);
        s.HasPort.Returns(hasPort);
        s.IsFortification.Returns(isFortification);
        return s;
    }

    [TestMethod]
    public void Run_Default_OneCallPerPair()
    {
        var s1 = MakeSettlement("a");
        var s2 = MakeSettlement("b");
        var s3 = MakeSettlement("c");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2, s3 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(3, result.PairsComputed);
        _adapter.Received(1).AddClosestEntrancePair(s1, false, s2, false);
        _adapter.Received(1).AddClosestEntrancePair(s1, false, s3, false);
        _adapter.Received(1).AddClosestEntrancePair(s2, false, s3, false);
    }

    [TestMethod]
    public void Run_Default_NeverCallsPortVariants()
    {
        var s1 = MakeSettlement("a", hasPort: true);
        var s2 = MakeSettlement("b", hasPort: true);
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);

        _sut.Run(_adapter, CancellationToken.None);

        _adapter.DidNotReceive().AddClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), true, Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>());
        _adapter.DidNotReceive().AddClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(), Arg.Any<ISettlementDataHolder>(), true);
    }

    [TestMethod]
    public void Run_Naval_OnlyEmitsPairsWithBothPorts()
    {
        var s1 = MakeSettlement("a", hasPort: true);
        var s2 = MakeSettlement("b", hasPort: false);
        var s3 = MakeSettlement("c", hasPort: true);
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2, s3 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Naval);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(1, result.PairsComputed);
        _adapter.Received(1).AddClosestEntrancePair(s1, true, s3, true);
        _adapter.DidNotReceive().AddClosestEntrancePair(s1, true, s2, true);
        _adapter.DidNotReceive().AddClosestEntrancePair(s2, true, s3, true);
    }

    [TestMethod]
    public void Run_All_EmitsAllFourEntranceCombinationsWhenBothPorts()
    {
        var s1 = MakeSettlement("a", hasPort: true);
        var s2 = MakeSettlement("b", hasPort: true);
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.All);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(4, result.PairsComputed);
        _adapter.Received(1).AddClosestEntrancePair(s1, false, s2, false);
        _adapter.Received(1).AddClosestEntrancePair(s1, true, s2, true);
        _adapter.Received(1).AddClosestEntrancePair(s1, false, s2, true);
        _adapter.Received(1).AddClosestEntrancePair(s1, true, s2, false);
    }

    [TestMethod]
    public void Run_All_OnlyGatePairWhenNoPorts()
    {
        var s1 = MakeSettlement("a", hasPort: false);
        var s2 = MakeSettlement("b", hasPort: false);
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.All);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(1, result.PairsComputed);
        _adapter.Received(1).AddClosestEntrancePair(s1, false, s2, false);
        _adapter.DidNotReceive().AddClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), true, Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>());
    }

    [TestMethod]
    public void Run_EmptySettlements_NoCallsAndReturnsZero()
    {
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder>());
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(0, result.PairsComputed);
        _adapter.DidNotReceive().AddClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(), Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>());
    }

    [TestMethod]
    [ExpectedException(typeof(System.OperationCanceledException))]
    public void Run_CancelledMidLoop_Throws()
    {
        var settlements = new List<ISettlementDataHolder>();
        for (int i = 0; i < 100; i++) settlements.Add(MakeSettlement($"s{i}"));
        _adapter.GetAllRegisteredSettlements().Returns(settlements);
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _sut.Run(_adapter, cts.Token);
    }

    [TestMethod]
    public void Run_RecordsElapsedTime()
    {
        var s1 = MakeSettlement("a");
        var s2 = MakeSettlement("b");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.IsTrue(result.ElapsedSeconds >= 0);
    }
}
