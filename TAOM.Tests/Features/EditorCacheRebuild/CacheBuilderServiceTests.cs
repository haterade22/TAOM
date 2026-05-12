using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild;
using TAOM.Features.EditorCacheRebuild.Checkpoint;
using TAOM.Features.EditorCacheRebuild.Diff;
using TAOM.Features.EditorCacheRebuild.Phase1;
using TAOM.Features.EditorCacheRebuild.Phase2;
using TAOM.Features.EditorCacheRebuild.Validation;

namespace TAOM.Tests.Features.EditorCacheRebuild;

[TestClass]
public class CacheBuilderServiceTests
{
    private TestableSerialPhase1Builder _serialPhase1 = null!;
    private TestableParallelPhase1Builder _parallelPhase1 = null!;
    private TestableSerialPhase2Builder _serialPhase2 = null!;
    private TestableParallelPhase2Builder _parallelPhase2 = null!;
    private ISmokeTestGate _smokeTestGate = null!;
    private IValidationReportWriter _reportWriter = null!;
    private ICheckpointSerializer _checkpointSerializer = null!;
    private ISettlementSnapshotStore _snapshotStore = null!;
    private ISettlementDiffer _differ = null!;
    private TAOM.Core.Infrastructure.IPathService _pathService = null!;
    private ICacheRebuildConfigProvider _configProvider = null!;
    private CacheRebuildConfig _config = null!;
    private IModLogger _logger = null!;
    private INavigationCacheAdapter _adapter = null!;
    private CacheBuilderService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _serialPhase1 = new TestableSerialPhase1Builder(_logger);
        _parallelPhase1 = new TestableParallelPhase1Builder(_logger);
        _serialPhase2 = new TestableSerialPhase2Builder(_logger);
        _parallelPhase2 = new TestableParallelPhase2Builder(_logger);
        _smokeTestGate = Substitute.For<ISmokeTestGate>();
        _smokeTestGate.Run(Arg.Any<INavigationCacheAdapter>(), Arg.Any<CancellationToken>())
            .Returns(new SmokeTestResult(SmokeTestOutcome.Passed, 10, 0f));
        _reportWriter = Substitute.For<IValidationReportWriter>();
        _checkpointSerializer = Substitute.For<ICheckpointSerializer>();
        _checkpointSerializer.TryLoad(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<INavigationCacheAdapter>())
            .Returns((CheckpointMetadata?)null);
        _snapshotStore = Substitute.For<ISettlementSnapshotStore>();
        _differ = Substitute.For<ISettlementDiffer>();
        _differ.Compute(Arg.Any<SettlementSnapshotFile?>(), Arg.Any<INavigationCacheAdapter>(), Arg.Any<int>())
            .Returns(new SettlementDiff { ForcedFullRebuild = true, Reason = "test default" });
        _pathService = Substitute.For<TAOM.Core.Infrastructure.IPathService>();
        _pathService.ModuleRootPath.Returns(System.IO.Path.GetTempPath());
        _configProvider = Substitute.For<ICacheRebuildConfigProvider>();
        _config = new CacheRebuildConfig
        {
            Parallelism = 4,
            ValidationReportRelativePath = "",
            EnableCheckpoint = false,
            CheckpointRelativeDirectory = "",
            EnableIncremental = false,
            SettlementSnapshotRelativePath = "",
        };
        _configProvider.GetConfig().Returns(_config);
        _adapter = Substitute.For<INavigationCacheAdapter>();
        _adapter.GetAllRegisteredSettlements().Returns(new System.Collections.Generic.List<TaleWorlds.CampaignSystem.Map.DistanceCache.ISettlementDataHolder>());
        _sut = new CacheBuilderService(_serialPhase1, _parallelPhase1, _serialPhase2, _parallelPhase2, _smokeTestGate, _reportWriter, _checkpointSerializer, _snapshotStore, _differ, _configProvider, _pathService, _logger);
    }

    [TestMethod]
    public void Build_SmokeTestFails_FallsBackToSerial()
    {
        _config.Parallelism = 4;
        _smokeTestGate.Run(Arg.Any<INavigationCacheAdapter>(), Arg.Any<CancellationToken>())
            .Returns(new SmokeTestResult(SmokeTestOutcome.Failed, 10, 0.5f, "diverged"));
        _serialPhase1.ResultToReturn = new Phase1Result(50, 1.0);
        _serialPhase2.ResultToReturn = new Phase2Result(10, 5, 1.0);

        var result = _sut.Build(_adapter, CancellationToken.None);

        Assert.IsTrue(_serialPhase1.WasCalled);
        Assert.IsFalse(_parallelPhase1.WasCalled);
        Assert.IsTrue(_serialPhase2.WasCalled);
        Assert.IsFalse(_parallelPhase2.WasCalled);
        Assert.AreEqual(SmokeTestOutcome.Failed, result.SmokeTest.Outcome);
    }

    [TestMethod]
    public void Build_Parallelism1_DoesNotRunSmokeTest()
    {
        _config.Parallelism = 1;
        _serialPhase1.ResultToReturn = new Phase1Result(50, 0.5);

        _sut.Build(_adapter, CancellationToken.None);

        _smokeTestGate.DidNotReceive().Run(Arg.Any<INavigationCacheAdapter>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public void Build_Parallelism4_UsesParallelBuilders()
    {
        _config.Parallelism = 4;
        _parallelPhase1.ResultToReturn = new Phase1Result(100, 1.5);
        _parallelPhase2.ResultToReturn = new Phase2Result(20, 10, 2.0);

        var result = _sut.Build(_adapter, CancellationToken.None);

        Assert.IsTrue(_parallelPhase1.WasCalled);
        Assert.IsFalse(_serialPhase1.WasCalled);
        Assert.IsTrue(_parallelPhase2.WasCalled);
        Assert.IsFalse(_serialPhase2.WasCalled);
        Assert.AreEqual(100, result.Phase1.PairsComputed);
        Assert.AreEqual(10, result.Phase2.NeighborPairsAdded);
    }

    [TestMethod]
    public void Build_Parallelism1_UsesSerialBuilders()
    {
        _config.Parallelism = 1;
        _serialPhase1.ResultToReturn = new Phase1Result(50, 0.5);
        _serialPhase2.ResultToReturn = new Phase2Result(10, 5, 1.0);

        var result = _sut.Build(_adapter, CancellationToken.None);

        Assert.IsTrue(_serialPhase1.WasCalled);
        Assert.IsFalse(_parallelPhase1.WasCalled);
        Assert.IsTrue(_serialPhase2.WasCalled);
        Assert.IsFalse(_parallelPhase2.WasCalled);
        Assert.AreEqual(50, result.Phase1.PairsComputed);
        Assert.AreEqual(5, result.Phase2.NeighborPairsAdded);
    }

    [TestMethod]
    public void Build_Phase2Throws_ReturnsCancelled()
    {
        _parallelPhase1.ResultToReturn = new Phase1Result(50, 1.0);
        _parallelPhase2.ThrowOnRun = true;

        var result = _sut.Build(_adapter, CancellationToken.None);

        Assert.IsTrue(result.Cancelled);
    }

    private sealed class TestableSerialPhase1Builder : SerialPhase1Builder
    {
        public bool WasCalled;
        public Phase1Result ResultToReturn = new(0, 0);

        public TestableSerialPhase1Builder(IModLogger logger) : base(logger) { }

        public override Phase1Result Run(INavigationCacheAdapter adapter, CancellationToken ct)
        {
            WasCalled = true;
            return ResultToReturn;
        }
    }

    private sealed class TestableParallelPhase1Builder : ParallelPhase1Builder
    {
        public bool WasCalled;
        public Phase1Result ResultToReturn = new(0, 0);

        public TestableParallelPhase1Builder(IModLogger logger)
            : base(logger, MakeConfigProvider()) { }

        private static ICacheRebuildConfigProvider MakeConfigProvider()
        {
            var p = Substitute.For<ICacheRebuildConfigProvider>();
            p.GetConfig().Returns(new CacheRebuildConfig { Parallelism = 4 });
            return p;
        }

        public override Phase1Result Run(INavigationCacheAdapter adapter, CancellationToken ct)
        {
            WasCalled = true;
            return ResultToReturn;
        }
    }

    private sealed class TestableSerialPhase2Builder : SerialPhase2Builder
    {
        public bool WasCalled;
        public Phase2Result ResultToReturn = new(0, 0, 0);

        public TestableSerialPhase2Builder(IModLogger logger) : base(logger) { }

        public override Phase2Result Run(INavigationCacheAdapter adapter, CancellationToken ct)
        {
            WasCalled = true;
            return ResultToReturn;
        }
    }

    private sealed class TestableParallelPhase2Builder : ParallelPhase2Builder
    {
        public bool WasCalled;
        public bool ThrowOnRun;
        public Phase2Result ResultToReturn = new(0, 0, 0);

        public TestableParallelPhase2Builder(IModLogger logger)
            : base(logger, MakeConfigProvider()) { }

        private static ICacheRebuildConfigProvider MakeConfigProvider()
        {
            var p = Substitute.For<ICacheRebuildConfigProvider>();
            p.GetConfig().Returns(new CacheRebuildConfig { Parallelism = 4 });
            return p;
        }

        public override Phase2Result Run(INavigationCacheAdapter adapter, CancellationToken ct)
        {
            WasCalled = true;
            if (ThrowOnRun) throw new OperationCanceledException();
            return ResultToReturn;
        }
    }
}
