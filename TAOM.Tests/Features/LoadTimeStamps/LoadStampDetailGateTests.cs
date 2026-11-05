using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LoadTimeStamps;

[TestClass]
public class LoadStampDetailGateTests
{
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private RecordingLogger _logger = null!;
    private LoadStampDetailGate _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _logger = new RecordingLogger();
        _sut = new LoadStampDetailGate(_settings, _logger);
    }

    [TestMethod]
    public void Enabled_ReflectsTheProvider()
    {
        _settings.LoadTimeStampsEnabled.Returns(true);
        Assert.IsTrue(_sut.Enabled);

        _settings.LoadTimeStampsEnabled.Returns(false);
        Assert.IsFalse(_sut.Enabled);
    }

    [TestMethod]
    public void Enabled_FirstRead_LogsTheDetailLineOnce()
    {
        _settings.LoadTimeStampsEnabled.Returns(true);

        _ = _sut.Enabled;
        _ = _sut.Enabled;

        CollectionAssert.AreEqual(new[] { "INFO " + LoadTimeStampLines.Detail(true) }, _logger.Lines);
        StringAssert.StartsWith(_logger.Lines[0], "INFO [LoadStamps] detail on: ");
    }

    [TestMethod]
    public void Enabled_ValueChanges_LogsTheNewDetailLine()
    {
        _settings.LoadTimeStampsEnabled.Returns(true, true, false);

        _ = _sut.Enabled;
        _ = _sut.Enabled;
        _ = _sut.Enabled;

        Assert.AreEqual(2, _logger.Lines.Count);
        StringAssert.StartsWith(_logger.Lines[0], "INFO [LoadStamps] detail on: ");
        StringAssert.StartsWith(_logger.Lines[1], "INFO [LoadStamps] detail off: ");
    }

    [TestMethod]
    public void Enabled_ProviderThrows_ReturnsFalse()
    {
        _settings.LoadTimeStampsEnabled.Returns(_ => throw new InvalidOperationException("mcm"));

        Assert.IsFalse(_sut.Enabled);
    }

    [TestMethod]
    public void Enabled_ProviderThrows_LogsWhyOnce_AndARecoveredReadLogsTheNewValue()
    {
        var thrown = new InvalidOperationException("mcm");
        _settings.LoadTimeStampsEnabled.Returns(_ => throw thrown, _ => throw thrown, _ => false, _ => true);

        _ = _sut.Enabled;
        _ = _sut.Enabled;
        _ = _sut.Enabled;
        _ = _sut.Enabled;

        CollectionAssert.AreEqual(new[]
        {
            "WARN " + LoadTimeStampLines.DetailUnreadable(thrown),
            "INFO " + LoadTimeStampLines.Detail(false),
            "INFO " + LoadTimeStampLines.Detail(true),
        }, _logger.Lines);
    }

    [TestMethod]
    public void Enabled_ProviderThrowsAfterAnOffRead_LogsWhyOnce()
    {
        var thrown = new InvalidOperationException("mcm");
        _settings.LoadTimeStampsEnabled.Returns(_ => false, _ => throw thrown, _ => throw thrown);

        _ = _sut.Enabled;
        _ = _sut.Enabled;
        _ = _sut.Enabled;

        CollectionAssert.AreEqual(new[]
        {
            "INFO " + LoadTimeStampLines.Detail(false),
            "WARN " + LoadTimeStampLines.DetailUnreadable(thrown),
        }, _logger.Lines);
    }

    [TestMethod]
    public void Enabled_ProviderThrowsAfterAnOnRead_LogsWhyTheDetailWentOff()
    {
        var thrown = new InvalidOperationException("mcm");
        _settings.LoadTimeStampsEnabled.Returns(_ => true, _ => throw thrown);

        Assert.IsTrue(_sut.Enabled);
        Assert.IsFalse(_sut.Enabled);

        CollectionAssert.AreEqual(new[]
        {
            "INFO " + LoadTimeStampLines.Detail(true),
            "WARN " + LoadTimeStampLines.DetailUnreadable(thrown),
        }, _logger.Lines);
    }
}
