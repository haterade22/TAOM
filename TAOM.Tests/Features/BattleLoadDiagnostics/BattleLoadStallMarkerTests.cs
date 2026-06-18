using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class BattleLoadStallMarkerTests
{
    private string _markerPath = string.Empty;
    private IModLogger _logger = null!;
    private BattleLoadStallMarker _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _markerPath = Path.Combine(Path.GetTempPath(),
            "taom_bld_marker_test_" + Guid.NewGuid().ToString("N") + ".marker");
        _logger = Substitute.For<IModLogger>();
        _logger.LogFilePath.Returns(@"Logs\taom_debug_2026-06-17_12-00-00.log");
        _sut = new BattleLoadStallMarker(_logger, _markerPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { if (File.Exists(_markerPath)) File.Delete(_markerPath); } catch { }
    }

    // ---- pure Format/Parse ---- //
    [TestMethod]
    public void FormatThenParse_RoundTripsSceneAndLog()
    {
        var utc = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
        var text = BattleLoadStallMarker.Format("battle_terrain_158", utc, @"Logs\taom_debug_x.log");

        var info = BattleLoadStallMarker.Parse(text, _markerPath);

        Assert.AreEqual("battle_terrain_158", info.SceneName);
        Assert.AreEqual(@"Logs\taom_debug_x.log", info.LogFilePath);
        Assert.AreEqual(utc, info.WrittenUtc!.Value.ToUniversalTime());
        Assert.AreEqual(_markerPath, info.MarkerPath);
    }

    [TestMethod]
    public void Parse_MissingFields_DoesNotThrow_AndDefaultsAreSafe()
    {
        var info = BattleLoadStallMarker.Parse("garbage line\n", _markerPath);

        Assert.AreEqual(string.Empty, info.SceneName);
        Assert.AreEqual(string.Empty, info.LogFilePath);
        Assert.IsNull(info.WrittenUtc);
    }

    [TestMethod]
    public void Format_NullSceneAndLog_ProducesParseableText()
    {
        var text = BattleLoadStallMarker.Format(null!, DateTime.UtcNow, null);
        var info = BattleLoadStallMarker.Parse(text, _markerPath);

        Assert.AreEqual(string.Empty, info.SceneName);
        Assert.AreEqual(string.Empty, info.LogFilePath);
        Assert.IsNotNull(info.WrittenUtc);
    }

    // ---- file lifecycle ---- //
    [TestMethod]
    public void MarkInflight_ThenTryConsume_ReturnsInfo_WithLoggerLogPath()
    {
        _sut.MarkInflight("battle_terrain_b");

        Assert.IsTrue(File.Exists(_markerPath), "marker should be written");
        var info = _sut.TryConsumeStaleMarker();

        Assert.IsNotNull(info);
        Assert.AreEqual("battle_terrain_b", info!.SceneName);
        // The log path is stored ABSOLUTE so the next-session "Open log folder" button (which
        // hands it to explorer.exe) can resolve it — FileLogger.LogFilePath is cwd-relative.
        Assert.IsTrue(Path.IsPathRooted(info.LogFilePath), $"log path should be absolute: {info.LogFilePath}");
        Assert.AreEqual(Path.GetFullPath(@"Logs\taom_debug_2026-06-17_12-00-00.log"), info.LogFilePath);
    }

    [TestMethod]
    public void TryConsume_DeletesMarker_SecondConsumeReturnsNull()
    {
        _sut.MarkInflight("town_ES2");

        Assert.IsNotNull(_sut.TryConsumeStaleMarker());
        Assert.IsFalse(File.Exists(_markerPath), "marker should be deleted on consume");
        Assert.IsNull(_sut.TryConsumeStaleMarker(), "a consumed marker must not fire twice");
    }

    [TestMethod]
    public void ClearInflight_RemovesMarker_NoStaleNoticeNextSession()
    {
        _sut.MarkInflight("battle_terrain_b");
        _sut.ClearInflight();

        Assert.IsFalse(File.Exists(_markerPath));
        Assert.IsNull(_sut.TryConsumeStaleMarker());
    }

    [TestMethod]
    public void TryConsume_NoMarkerFile_ReturnsNull()
        => Assert.IsNull(_sut.TryConsumeStaleMarker());

    [TestMethod]
    public void ClearInflight_NoMarker_DoesNotThrow()
        => _sut.ClearInflight();   // no exception = pass

    [TestMethod]
    public void MarkInflight_TargetDirMissing_CreatesDirAndWritesMarker()
    {
        // Default path is "Logs/..."; on a fresh run that subdir may not exist. Exercise the
        // Directory.CreateDirectory path so deleting it (a mutation) fails this test.
        var nested = Path.Combine(Path.GetTempPath(),
            "taom_bld_" + Guid.NewGuid().ToString("N"), "sub", "battle-load-inflight.marker");
        try
        {
            var sut = new BattleLoadStallMarker(_logger, nested);
            sut.MarkInflight("battle_terrain_x");
            Assert.IsTrue(File.Exists(nested), "marker should be written even when target dir did not exist");
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(nested))!, true); } catch { }
        }
    }

    [TestMethod]
    public void TryConsume_DeleteFails_StillReturnsParsedInfo()
    {
        // Parse-before-delete: hold the marker open WITHOUT delete-share so File.Delete throws.
        // The already-read content must still be parsed and returned so the hang report surfaces.
        _sut.MarkInflight("battle_terrain_locked");
        using (new FileStream(_markerPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var info = _sut.TryConsumeStaleMarker();
            Assert.IsNotNull(info, "a locked/undeletable marker must still surface the parsed info");
            Assert.AreEqual("battle_terrain_locked", info!.SceneName);
        }
    }

    // ---- DI-registration guard ---- //
    [TestMethod]
    public void BattleLoadStallMarker_HasSinglePublicConstructor_ForDryIocResolution()
    {
        // DryIoc auto-resolves only with exactly one PUBLIC ctor. The test-seam (logger, path)
        // ctor is internal on purpose; re-publicizing it crashes OnSubModuleLoad. Fail here, not
        // at game load. (regression guard for the 2026-06-17 UnableToSelectSinglePublicConstructor CTD)
        var publicCtors = typeof(BattleLoadStallMarker)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.AreEqual(1, publicCtors.Length,
            "BattleLoadStallMarker must expose exactly one public ctor so DryIoc can select it");
    }
}
