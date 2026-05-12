using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild;
using TAOM.Features.EditorCacheRebuild.Validation;

namespace TAOM.Tests.Features.EditorCacheRebuild.Validation;

[TestClass]
public class SmokeTestGateTests
{
    private IModLogger _logger = null!;
    private ICacheRebuildConfigProvider _configProvider = null!;
    private CacheRebuildConfig _config = null!;
    private INavigationCacheAdapter _adapter = null!;
    private SmokeTestGate _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _configProvider = Substitute.For<ICacheRebuildConfigProvider>();
        _config = new CacheRebuildConfig
        {
            Parallelism = 4,
            SmokeTestPairs = 10,
            SmokeTestDistanceTolerance = 1e-4f,
        };
        _configProvider.GetConfig().Returns(_config);
        _adapter = Substitute.For<INavigationCacheAdapter>();
        _sut = new SmokeTestGate(_logger, _configProvider);
    }

    private static ISettlementDataHolder MakeFortification(string id)
    {
        var s = Substitute.For<ISettlementDataHolder>();
        s.StringId.Returns(id);
        s.IsFortification.Returns(true);
        return s;
    }

    private static ISettlementDataHolder MakeVillage(string id)
    {
        var s = Substitute.For<ISettlementDataHolder>();
        s.StringId.Returns(id);
        s.IsFortification.Returns(false);
        return s;
    }

    [TestMethod]
    public void Run_Parallelism1_SkippedWithReason()
    {
        _config.Parallelism = 1;
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder>());

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(SmokeTestOutcome.Skipped, result.Outcome);
        Assert.IsTrue(result.IsSafeForParallel);
        Assert.IsTrue(result.Reason!.Contains("parallelism=1"));
    }

    [TestMethod]
    public void Run_NoFortifications_Skipped()
    {
        var v1 = MakeVillage("v1");
        var v2 = MakeVillage("v2");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { v1, v2 });

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(SmokeTestOutcome.Skipped, result.Outcome);
        Assert.IsTrue(result.IsSafeForParallel);
    }

    [TestMethod]
    public void Run_DeterministicResults_Passes()
    {
        var settlements = new List<ISettlementDataHolder>();
        for (int i = 0; i < 20; i++) settlements.Add(MakeFortification($"f{i:00}"));
        _adapter.GetAllRegisteredSettlements().Returns(settlements);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>())
            .Returns(new PairComputeResult(new object(), new object(), 50f, 1f));

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(SmokeTestOutcome.Passed, result.Outcome);
        Assert.IsTrue(result.IsSafeForParallel);
        Assert.IsTrue(result.PairsTested > 0);
        Assert.IsTrue(result.MaxDistanceDelta <= _config.SmokeTestDistanceTolerance);
    }

    [TestMethod]
    public void Run_DivergentParallelResults_Fails()
    {
        var settlements = new List<ISettlementDataHolder>();
        for (int i = 0; i < 20; i++) settlements.Add(MakeFortification($"f{i:00}"));
        _adapter.GetAllRegisteredSettlements().Returns(settlements);

        var callCount = 0;
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>())
            .Returns(_ =>
            {
                var n = System.Threading.Interlocked.Increment(ref callCount);
                // First batch (serial baseline) returns 50f.
                // Second batch (parallel) returns 100f for some pairs → big delta.
                var distance = n <= _config.SmokeTestPairs ? 50f : (n % 2 == 0 ? 100f : 50f);
                return new PairComputeResult(new object(), new object(), distance, 1f);
            });

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(SmokeTestOutcome.Failed, result.Outcome);
        Assert.IsFalse(result.IsSafeForParallel);
        Assert.IsTrue(result.MaxDistanceDelta > _config.SmokeTestDistanceTolerance);
    }

    [TestMethod]
    public void Run_OnlyOneFortification_Skipped()
    {
        var f1 = MakeFortification("f1");
        _adapter.GetAllRegisteredSettlements().Returns(new List<ISettlementDataHolder> { f1 });

        var result = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(SmokeTestOutcome.Skipped, result.Outcome);
    }

    [TestMethod]
    public void Run_DeterministicSeed_ProducesSamePairsAcrossRuns()
    {
        var settlements = new List<ISettlementDataHolder>();
        for (int i = 0; i < 20; i++) settlements.Add(MakeFortification($"f{i:00}"));
        _adapter.GetAllRegisteredSettlements().Returns(settlements);
        _adapter.ComputeClosestEntrancePair(Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>(),
            Arg.Any<ISettlementDataHolder>(), Arg.Any<bool>())
            .Returns(new PairComputeResult(new object(), new object(), 75f, 1f));

        var result1 = _sut.Run(_adapter, CancellationToken.None);
        var result2 = _sut.Run(_adapter, CancellationToken.None);

        Assert.AreEqual(result1.PairsTested, result2.PairsTested);
        Assert.AreEqual(result1.Outcome, result2.Outcome);
    }
}
