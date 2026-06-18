using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation;

namespace TAOM.Tests.Features.ShaderPrecompilation;

[TestClass]
public class ShaderPrecompileCrashGuardTests
{
    private string _dir;
    private string _inflight;
    private string _crashed;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom_crashguard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _inflight = Path.Combine(_dir, "inflight.marker");
        _crashed = Path.Combine(_dir, "crashed.txt");
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private ShaderPrecompileCrashGuard New() => new(_logger, _inflight, _crashed);

    // ---- pure helpers ---- //

    [TestMethod]
    public void ParseInflightScene_ValidScene_ReturnsScene()
        => Assert.AreEqual("taom_rohan_battle_fords_of_isen_forceatmo",
            ShaderPrecompileCrashGuard.ParseInflightScene("scene=taom_rohan_battle_fords_of_isen_forceatmo\nutc=2026-06-18T20:00:00Z\n"));

    [TestMethod]
    public void ParseInflightScene_EmptyOrMissing_ReturnsNull()
    {
        Assert.IsNull(ShaderPrecompileCrashGuard.ParseInflightScene("scene=\nutc=x\n"));
        Assert.IsNull(ShaderPrecompileCrashGuard.ParseInflightScene("utc=x\n"));
        Assert.IsNull(ShaderPrecompileCrashGuard.ParseInflightScene(""));
        Assert.IsNull(ShaderPrecompileCrashGuard.ParseInflightScene(null));
    }

    [TestMethod]
    public void FormatInflight_RoundTripsThroughParse()
    {
        var text = ShaderPrecompileCrashGuard.FormatInflight("scene_a", DateTime.UtcNow);
        Assert.AreEqual("scene_a", ShaderPrecompileCrashGuard.ParseInflightScene(text));
    }

    [TestMethod]
    public void ParseCrashedScenes_IgnoresCommentsAndBlanks_AndDedupes()
    {
        var set = ShaderPrecompileCrashGuard.ParseCrashedScenes("# header\n\nscene_a\nscene_b\nSCENE_A\n  scene_b  \n");
        Assert.AreEqual(2, set.Count);
        Assert.IsTrue(set.Contains("scene_a"));
        Assert.IsTrue(set.Contains("scene_b"));
    }

    [TestMethod]
    public void ParseCrashedScenes_EmptyOrNull_ReturnsEmpty()
    {
        Assert.AreEqual(0, ShaderPrecompileCrashGuard.ParseCrashedScenes("").Count);
        Assert.AreEqual(0, ShaderPrecompileCrashGuard.ParseCrashedScenes(null).Count);
    }

    // ---- file lifecycle ---- //

    [TestMethod]
    public void Consume_NoInflight_NoCrashList_ReturnsEmpty()
        => Assert.AreEqual(0, New().ConsumeAndGetSkipSet().Count);

    [TestMethod]
    public void MarkLoading_ThenConsumeWithoutClear_RecordsCrashAndSkips()
    {
        // Simulate: a walk marked the scene it was loading, then the process hard-crashed (no ClearLoading).
        New().MarkLoading("scene_x");
        Assert.IsTrue(File.Exists(_inflight), "inflight marker should be written before load");

        var skip = New().ConsumeAndGetSkipSet();   // next walk start

        CollectionAssert.Contains(skip.ToList(), "scene_x");
        Assert.IsFalse(File.Exists(_inflight), "inflight marker should be consumed (deleted)");
        Assert.IsTrue(File.Exists(_crashed), "the crashed scene should be persisted");
    }

    [TestMethod]
    public void MarkLoading_ThenClear_ThenConsume_NotRecorded()
    {
        // Clean path: the item loaded + resolved, so the runner cleared the marker.
        var g = New();
        g.MarkLoading("scene_y");
        g.ClearLoading();
        Assert.IsFalse(File.Exists(_inflight));

        Assert.AreEqual(0, New().ConsumeAndGetSkipSet().Count, "a cleanly-cleared scene must not be skipped");
    }

    [TestMethod]
    public void Consume_PersistsSkipAcrossSubsequentWalks()
    {
        New().MarkLoading("scene_x");
        New().ConsumeAndGetSkipSet();               // walk 2: records the crash
        var third = New().ConsumeAndGetSkipSet();   // walk 3: no inflight, but still skips from the persisted list
        CollectionAssert.Contains(third.ToList(), "scene_x");
    }

    [TestMethod]
    public void RecordingSameSceneTwice_DedupesInPersistedList()
    {
        New().MarkLoading("scene_x");
        New().ConsumeAndGetSkipSet();   // record once
        New().MarkLoading("scene_x");
        New().ConsumeAndGetSkipSet();   // crash again — must not duplicate

        Assert.AreEqual(1, ShaderPrecompileCrashGuard.ParseCrashedScenes(File.ReadAllText(_crashed)).Count);
    }
}
