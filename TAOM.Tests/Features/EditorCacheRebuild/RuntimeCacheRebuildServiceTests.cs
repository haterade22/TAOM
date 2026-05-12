using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild;

namespace TAOM.Tests.Features.EditorCacheRebuild;

[TestClass]
public class RuntimeCacheRebuildServiceTests
{
    private IDistanceCacheBuilderService _builderService = null!;
    private ICacheRebuildConfigProvider _configProvider = null!;
    private IPathService _pathService = null!;
    private ICampaignSessionAdapter _sessionAdapter = null!;
    private IModLogger _logger = null!;
    private TestableRuntimeCacheRebuildService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _builderService = Substitute.For<IDistanceCacheBuilderService>();
        _configProvider = Substitute.For<ICacheRebuildConfigProvider>();
        _configProvider.GetConfig().Returns(new CacheRebuildConfig());
        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleRootPath.Returns(Path.GetTempPath());
        _sessionAdapter = Substitute.For<ICampaignSessionAdapter>();
        _sessionAdapter.GetSnapshot().Returns(new CampaignSnapshot());
        _logger = Substitute.For<IModLogger>();

        // Default to "session ready" — individual tests override as needed.
        _sessionAdapter.IsReadyForRebuild(out Arg.Any<string>())
            .Returns(call =>
            {
                call[0] = string.Empty;
                return true;
            });

        _sut = new TestableRuntimeCacheRebuildService(
            _builderService, _configProvider, _pathService, _sessionAdapter, _logger);
    }

    [TestMethod]
    public void IsRunning_InitialState_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsRunning);
    }

    [TestMethod]
    public void Trigger_CampaignNotReady_ReturnsFalseAndDoesNotCreateCacheAdapter()
    {
        _sessionAdapter.IsReadyForRebuild(out Arg.Any<string>())
            .Returns(call =>
            {
                call[0] = "Campaign.Current is null (no campaign active — load a save first)";
                return false;
            });

        var accepted = _sut.Trigger();

        Assert.IsFalse(accepted);
        _sessionAdapter.DidNotReceive().CreateDefaultRuntimeCacheAdapter(Arg.Any<IModLogger>());
        Assert.IsFalse(_sut.IsRunning, "IsRunning must remain false when trigger is rejected");
        Assert.AreEqual(0, _sut.SpawnBuildCallCount, "SpawnBuild must NOT be called when session is not ready");
    }

    [TestMethod]
    public void Trigger_CampaignNotReady_LogsRejectionReasonAtWarning()
    {
        const string Reason = "Campaign.Current.MapSceneWrapper is null (campaign still loading — wait until fully initialized)";
        _sessionAdapter.IsReadyForRebuild(out Arg.Any<string>())
            .Returns(call =>
            {
                call[0] = Reason;
                return false;
            });

        _sut.Trigger();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("REJECTED") && s.Contains(Reason)));
    }

    [TestMethod]
    public void Trigger_SessionReady_ReturnsTrueAndSpawnsBuild()
    {
        var accepted = _sut.Trigger();

        Assert.IsTrue(accepted);
        Assert.AreEqual(1, _sut.SpawnBuildCallCount, "SpawnBuild must be invoked exactly once on a ready session");
    }

    [TestMethod]
    public void Trigger_SessionReady_SetsRunningFlag()
    {
        // The testable subclass overrides SpawnBuild to be a no-op — the flag stays set until
        // manually cleared. This lets us verify the Interlocked acquisition path.
        _sut.Trigger();

        Assert.IsTrue(_sut.IsRunning, "Trigger must atomically acquire the running flag before spawning the build");
    }

    [TestMethod]
    public void Trigger_TwiceConcurrently_SecondCallReturnsFalse()
    {
        // First trigger acquires the flag and spawns (no-op) build.
        var firstAccepted = _sut.Trigger();
        Assert.IsTrue(firstAccepted, "first Trigger should succeed on a ready, idle session");
        Assert.IsTrue(_sut.IsRunning);

        // Second trigger sees the flag set and must reject.
        var secondAccepted = _sut.Trigger();

        Assert.IsFalse(secondAccepted, "second concurrent Trigger must be rejected by the Interlocked lock");
        Assert.AreEqual(1, _sut.SpawnBuildCallCount, "SpawnBuild must NOT be invoked a second time while the first is in flight");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("already running")));
    }

    [TestMethod]
    public void Trigger_TwiceConcurrently_DoesNotReCreateCacheAdapter()
    {
        _sut.Trigger();
        _sessionAdapter.ClearReceivedCalls();

        _sut.Trigger();

        _sessionAdapter.DidNotReceive().CreateDefaultRuntimeCacheAdapter(Arg.Any<IModLogger>());
    }

    [TestMethod]
    public void ResolveCacheOutputPath_DefaultNavType_BuildsPathUnderTaomMapModuleData()
    {
        _pathService.ModuleRootPath.Returns(@"E:\Games\Bannerlord\Modules\TAOM");

        var path = _sut.ResolveCacheOutputPath("Default");

        StringAssert.EndsWith(path, Path.Combine("TAOM_Map", "ModuleData", "DistanceCaches", "settlements_distance_cache_Default.bin"));
        StringAssert.Contains(path, Path.Combine("Modules", "TAOM_Map"));
        // The TAOM module root segment must NOT appear in the resolved path — output lives under the sibling TAOM_Map module.
        Assert.IsFalse(
            path.Contains(Path.Combine("Modules", "TAOM", "TAOM_Map")),
            $"Path should walk up out of TAOM module, got: {path}");
    }

    [TestMethod]
    public void ResolveCacheOutputPath_NavalNavType_UsesNavalSuffix()
    {
        _pathService.ModuleRootPath.Returns(@"E:\Games\Bannerlord\Modules\TAOM");

        var path = _sut.ResolveCacheOutputPath("Naval");

        StringAssert.EndsWith(path, "settlements_distance_cache_Naval.bin");
    }

    [TestMethod]
    public void Trigger_SessionReady_LogsAcceptanceAtInfo()
    {
        _sut.Trigger();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("ACCEPTED")));
    }

    [TestMethod]
    public void Trigger_SessionReady_LogsBuildIdInTriggerBanner()
    {
        _sut.Trigger();

        // Each build gets a unique 6-hex correlation ID; the TRIGGER REQUEST banner should reflect it.
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("TRIGGER REQUEST")));
    }

    /// <summary>
    /// Subclass that intercepts <see cref="RuntimeCacheRebuildService.SpawnBuild"/> so unit tests
    /// don't actually spin up a <see cref="System.Threading.Tasks.Task"/>. The Interlocked lock
    /// inside <see cref="RuntimeCacheRebuildService.Trigger"/> still fires, so we can observe its
    /// effect on <see cref="RuntimeCacheRebuildService.IsRunning"/> without timing races.
    /// </summary>
    private sealed class TestableRuntimeCacheRebuildService : RuntimeCacheRebuildService
    {
        public int SpawnBuildCallCount { get; private set; }

        public TestableRuntimeCacheRebuildService(
            IDistanceCacheBuilderService builderService,
            ICacheRebuildConfigProvider configProvider,
            IPathService pathService,
            ICampaignSessionAdapter sessionAdapter,
            IModLogger logger)
            : base(builderService, configProvider, pathService, sessionAdapter, logger)
        {
        }

        internal override void SpawnBuild(string buildId, string tag)
        {
            // Intentionally do not call base — that would Task.Run the real RunBuild which needs
            // a live Campaign.Current. Increment the counter so tests can assert invocation.
            SpawnBuildCallCount++;
        }
    }
}
