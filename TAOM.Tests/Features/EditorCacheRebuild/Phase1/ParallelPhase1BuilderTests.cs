using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild;
using TAOM.Features.EditorCacheRebuild.Phase1;

namespace TAOM.Tests.Features.EditorCacheRebuild.Phase1;

[TestClass]
public class ParallelPhase1BuilderTests
{
    private IModLogger _logger = null!;
    private INavigationCacheAdapter _adapter = null!;
    private ICacheRebuildConfigProvider _configProvider = null!;
    private CacheRebuildConfig _config = null!;
    private ParallelPhase1Builder _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _adapter = Substitute.For<INavigationCacheAdapter>();
        _configProvider = Substitute.For<ICacheRebuildConfigProvider>();
        _config = new CacheRebuildConfig { Parallelism = 4 };
        _configProvider.GetConfig().Returns(_config);
        _sut = new ParallelPhase1Builder(_logger, _configProvider);
    }

    private static ISettlementDataHolder MakeSettlement(string id, bool hasPort = false)
    {
        var s = Substitute.For<ISettlementDataHolder>();
        s.StringId.Returns(id);
        s.HasPort.Returns(hasPort);
        return s;
    }

    private static PairComputeResult ValidResult() =>
        new(new object(), new object(), 100f, 1f);

    [TestMethod]
    public void Run_Default_ComputesAllPairsAndBuffersValid()
    {
        var s1 = MakeSettlement("a");
        var s2 = MakeSettlement("b");
        var s3 = MakeSettlement("c");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2, s3 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>()).Returns(ValidResult());

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(3, result.PairsComputed);
        _adapter.Received(3).WriteComputedPair(Arg.Any<PairComputeResult>());
    }

    [TestMethod]
    public void Run_InvalidComputeResult_NotWritten()
    {
        var s1 = MakeSettlement("a");
        var s2 = MakeSettlement("b");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>()).Returns(PairComputeResult.Invalid);

        _sut.Run(_adapter, CancellationToken.None);

        _adapter.DidNotReceive().WriteComputedPair(Arg.Any<PairComputeResult>());
    }

    [TestMethod]
    public void Run_AllNavType_BothPorts_ComputesFourVariants()
    {
        var s1 = MakeSettlement("a", hasPort: true);
        var s2 = MakeSettlement("b", hasPort: true);
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.All);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>()).Returns(ValidResult());

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(4, result.PairsComputed);
        _adapter.Received(1).ComputeClosestEntrancePair(s1, false, s2, false);
        _adapter.Received(1).ComputeClosestEntrancePair(s1, true, s2, true);
        _adapter.Received(1).ComputeClosestEntrancePair(s1, false, s2, true);
        _adapter.Received(1).ComputeClosestEntrancePair(s1, true, s2, false);
    }

    [TestMethod]
    public void Run_Naval_OnlyComputesPairsWithBothPorts()
    {
        var s1 = MakeSettlement("a", hasPort: true);
        var s2 = MakeSettlement("b", hasPort: false);
        var s3 = MakeSettlement("c", hasPort: true);
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2, s3 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Naval);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>()).Returns(ValidResult());

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(1, result.PairsComputed);
        _adapter.Received(1).ComputeClosestEntrancePair(s1, true, s3, true);
    }

    [TestMethod]
    public void Run_LargeSettlementCount_AllPairsHandled()
    {
        var settlements = new List<ISettlementDataHolder>();
        for (int i = 0; i < 30; i++) settlements.Add(MakeSettlement($"s{i:00}"));
        _adapter.GetAllRegisteredSettlements().Returns(settlements);
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>()).Returns(ValidResult());

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(30 * 29 / 2, result.PairsComputed);
    }

    [TestMethod]
    [ExpectedException(typeof(System.OperationCanceledException))]
    public void Run_CancelledBeforeStart_Throws()
    {
        var s1 = MakeSettlement("a");
        var s2 = MakeSettlement("b");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>()).Returns(ValidResult());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _sut.Run(_adapter, cts.Token);
    }

    [TestMethod]
    public void Run_Parallelism1_StillProducesCorrectOutput()
    {
        _config.Parallelism = 1;
        var s1 = MakeSettlement("a");
        var s2 = MakeSettlement("b");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { s1, s2 });
        _adapter.NavigationType.Returns(MobileParty.NavigationType.Default);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>()).Returns(ValidResult());

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(1, result.PairsComputed);
        _adapter.Received(1).WriteComputedPair(Arg.Any<PairComputeResult>());
    }
}
