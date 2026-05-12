using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    // ──────────────────────────────────────────────────────────────────────────────
    // Atomic write & round-trip verification tests
    // (Codex review P3-2, 2026-05-12: gap between Trigger and the production path.)
    // ──────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void WriteOutputAtomically_NoExistingFinal_PromotesTempViaMove()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "taom-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var finalPath = Path.Combine(tempDir, "test_cache.bin");
            var adapter = Substitute.For<INavigationCacheAdapter>();
            adapter.WhenForAnyArgs(a => a.SerializeCache(default!))
                .Do(call => File.WriteAllBytes((string)call[0]!, new byte[] { 0x01, 0x02, 0x03 }));

            _sut.WriteOutputAtomically(adapter, finalPath, "[test]");

            Assert.IsTrue(File.Exists(finalPath), "final cache file must exist after first-time write");
            Assert.IsFalse(File.Exists(finalPath + ".tmp"), ".tmp must be consumed by the rename");
            Assert.IsFalse(File.Exists(finalPath + ".prev"), "no .prev should be created on first write");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void WriteOutputAtomically_ExistingFinal_AtomicReplacePreservesPrev()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "taom-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var finalPath = Path.Combine(tempDir, "test_cache.bin");
            var prevPath = finalPath + ".prev";

            // Seed an existing "old" final cache.
            var oldContent = new byte[] { 0xAA, 0xBB, 0xCC };
            File.WriteAllBytes(finalPath, oldContent);

            var newContent = new byte[] { 0x11, 0x22, 0x33, 0x44 };
            var adapter = Substitute.For<INavigationCacheAdapter>();
            adapter.WhenForAnyArgs(a => a.SerializeCache(default!))
                .Do(call => File.WriteAllBytes((string)call[0]!, newContent));

            _sut.WriteOutputAtomically(adapter, finalPath, "[test]");

            Assert.IsTrue(File.Exists(finalPath), "final cache file must still exist");
            Assert.IsTrue(File.Exists(prevPath), ".prev backup must hold the previous final contents");
            CollectionAssert.AreEqual(newContent, File.ReadAllBytes(finalPath), "final must hold the newly serialized bytes");
            CollectionAssert.AreEqual(oldContent, File.ReadAllBytes(prevPath), ".prev must hold the prior bytes (atomic replace)");
            Assert.IsFalse(File.Exists(finalPath + ".tmp"), ".tmp must be consumed by File.Replace");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void WriteOutputAtomically_StaleTempExists_DeletesBeforeWriting()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "taom-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var finalPath = Path.Combine(tempDir, "test_cache.bin");
            var tempPath = finalPath + ".tmp";

            // Stale .tmp left over from prior aborted build — must be cleaned up.
            File.WriteAllBytes(tempPath, new byte[] { 0xDE, 0xAD });

            var newContent = new byte[] { 0x42, 0x43 };
            var adapter = Substitute.For<INavigationCacheAdapter>();
            adapter.WhenForAnyArgs(a => a.SerializeCache(default!))
                .Do(call => File.WriteAllBytes((string)call[0]!, newContent));

            _sut.WriteOutputAtomically(adapter, finalPath, "[test]");

            Assert.IsTrue(File.Exists(finalPath), "final cache file must exist");
            Assert.IsFalse(File.Exists(tempPath), "stale .tmp must be gone");
            CollectionAssert.AreEqual(newContent, File.ReadAllBytes(finalPath), "stale .tmp content must NOT survive");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void VerifyOutputRoundTrip_DeserializeThrows_ReturnsFailureResult()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "taom-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var path = Path.Combine(tempDir, "test_cache.bin");
            File.WriteAllBytes(path, new byte[] { 0x00 }); // junk content

            var verifyAdapter = Substitute.For<INavigationCacheAdapter>();
            verifyAdapter.When(a => a.DeserializeCache(Arg.Any<string>()))
                .Do(_ => throw new InvalidDataException("simulated corrupt cache"));
            _sessionAdapter.CreateDefaultRuntimeCacheAdapter(Arg.Any<IModLogger?>())
                .Returns(verifyAdapter);

            var result = _sut.VerifyOutputRoundTrip(path, expectedDistanceEntries: 100, expectedNeighborPairs: 10, tag: "[test]");

            Assert.IsFalse(result.Ok, "deserialize exception must produce Failure result");
            StringAssert.Contains(result.Reason, "deserialize threw");
            StringAssert.Contains(result.Reason, "simulated corrupt cache");
            _logger.Received().LogError(Arg.Is<string>(s => s.Contains("POST-WRITE VERIFICATION FAILED")));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void VerifyOutputRoundTrip_DistanceShortfall_ReturnsFailureResult()
    {
        var verifyAdapter = Substitute.For<INavigationCacheAdapter>();
        // Deserialize succeeds but produces only 5 distance entries when we expected 100 (> 10% shortfall).
        var distances = Enumerable.Range(0, 5).Select(_ => default(DistancePair)).ToList();
        verifyAdapter.EnumerateExistingDistances().Returns(distances);
        verifyAdapter.EnumerateExistingNeighbors().Returns(new List<NeighborPair>());
        _sessionAdapter.CreateDefaultRuntimeCacheAdapter(Arg.Any<IModLogger?>())
            .Returns(verifyAdapter);

        var result = _sut.VerifyOutputRoundTrip("dummy-path", expectedDistanceEntries: 100, expectedNeighborPairs: 0, tag: "[test]");

        Assert.IsFalse(result.Ok, "5/100 distances must trip the 90%-tolerance shortfall check");
        StringAssert.Contains(result.Reason, "shortfall");
        Assert.AreEqual(5, result.ActualDistanceCount);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("POST-WRITE VERIFICATION SHORTFALL")));
    }

    [TestMethod]
    public void VerifyOutputRoundTrip_CountsMatch_ReturnsSuccessResult()
    {
        var verifyAdapter = Substitute.For<INavigationCacheAdapter>();
        var distances = Enumerable.Range(0, 100).Select(_ => default(DistancePair)).ToList();
        var neighbors = Enumerable.Range(0, 20).Select(_ => default(NeighborPair)).ToList(); // 10 pairs × 2 directions
        verifyAdapter.EnumerateExistingDistances().Returns(distances);
        verifyAdapter.EnumerateExistingNeighbors().Returns(neighbors);
        _sessionAdapter.CreateDefaultRuntimeCacheAdapter(Arg.Any<IModLogger?>())
            .Returns(verifyAdapter);

        var result = _sut.VerifyOutputRoundTrip("dummy-path", expectedDistanceEntries: 100, expectedNeighborPairs: 10, tag: "[test]");

        Assert.IsTrue(result.Ok, "100/100 distances and 20/20 neighbor entries should pass");
        Assert.AreEqual(100, result.ActualDistanceCount);
        Assert.AreEqual(20, result.ActualNeighborCount);
        Assert.AreEqual(string.Empty, result.Reason);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("round-trip OK")));
    }

    [TestMethod]
    public void VerifyOutputRoundTrip_NeighborSymmetricStorage_ComparesAgainstDoubledExpectation()
    {
        // 5 unique pairs × 2 directions = 10 entries in vanilla's _fortificationNeighbors dict.
        // The verifier must expect 10, not 5.
        var verifyAdapter = Substitute.For<INavigationCacheAdapter>();
        verifyAdapter.EnumerateExistingDistances().Returns(new List<DistancePair>());
        verifyAdapter.EnumerateExistingNeighbors().Returns(Enumerable.Range(0, 10).Select(_ => default(NeighborPair)).ToList());
        _sessionAdapter.CreateDefaultRuntimeCacheAdapter(Arg.Any<IModLogger?>())
            .Returns(verifyAdapter);

        var result = _sut.VerifyOutputRoundTrip("dummy-path", expectedDistanceEntries: 0, expectedNeighborPairs: 5, tag: "[test]");

        Assert.IsTrue(result.Ok, "10 neighbor entries must satisfy `5 pairs × 2 directions` expectation");
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
