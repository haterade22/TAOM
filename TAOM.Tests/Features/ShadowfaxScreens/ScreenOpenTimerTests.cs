using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens;

[TestClass]
public class ScreenOpenTimerTests
{
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private ScreenOpenTimer _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.LogScreenOpenTimes.Returns(true);
        _sut = new ScreenOpenTimer(_clock, _logger, _settings);
    }

    [TestCleanup]
    public void Cleanup() => ScreenOpenTiming.Initialize(null);

    [TestMethod]
    public void Begin_WhenOff_ReturnsNull_AndNeverReadsTheClock()
    {
        _settings.LogScreenOpenTimes.Returns(false);
        _clock.ThrowOnRead = 1;

        Assert.IsNull(_sut.Begin("Clan", "open"));
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_WhenTheProviderThrows_ReturnsNull()
    {
        _settings.LogScreenOpenTimes.Throws(new InvalidOperationException("mcm gone"));

        Assert.IsNull(_sut.Begin("Clan", "open"));
    }

    [TestMethod]
    public void End_WritesOneLineWithScreenPhaseMsAndDetail()
    {
        _clock.Now = 100;
        var window = _sut.Begin("Clan", "open");
        _clock.Advance(42);

        _sut.End(window, null, "lists 1");

        Assert.AreEqual(1, _logger.Lines.Count);
        Assert.AreEqual("INFO [ScreenOpen] Clan open: 42.0 ms (lists 1)", _logger.Lines[0]);
    }

    [TestMethod]
    public void End_WithoutDetail_HasNoParentheses()
    {
        var window = _sut.Begin("Party", "ctor");
        _clock.Advance(5);

        _sut.End(window, null);

        Assert.AreEqual("INFO [ScreenOpen] Party ctor: 5.0 ms", _logger.Lines.Single());
    }

    [TestMethod]
    public void End_WithAnException_AddsThrewAndTheTypeName()
    {
        var window = _sut.Begin("Inventory", "open");
        _clock.Advance(3);

        _sut.End(window, new InvalidOperationException("x"), "d");

        Assert.AreEqual("INFO [ScreenOpen] Inventory open: 3.0 ms, threw InvalidOperationException (d)", _logger.Lines.Single());
    }

    [TestMethod]
    public void End_Twice_WritesOneLine()
    {
        var window = _sut.Begin("Clan", "open");

        _sut.End(window, null);
        _sut.End(window, null);

        Assert.AreEqual(1, _logger.Lines.Count);
    }

    [TestMethod]
    public void End_WithANullWindow_WritesNothing()
    {
        _sut.End(null, null, "d");

        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_WhenTheClockThrows_ReturnsNull_AndDoesNotThrow()
    {
        _clock.ThrowOnRead = 1;

        Assert.IsNull(_sut.Begin("Clan", "open"));
    }

    [TestMethod]
    public void End_WhenTheClockThrows_DoesNotThrow()
    {
        var window = _sut.Begin("Clan", "open");
        _clock.ThrowOnRead = 2;

        _sut.End(window, null);

        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void End_WhenTheLoggerThrows_DoesNotThrow()
    {
        var window = _sut.Begin("Clan", "open");
        _logger.OnLog = _ => throw new InvalidOperationException("disk full");

        _sut.End(window, null);
    }

    [TestMethod]
    public void End_WithAZeroFrequency_WritesZeroMs()
    {
        _clock.Frequency = 0;
        var window = _sut.Begin("Clan", "open");
        _clock.Advance(500);

        _sut.End(window, null);

        Assert.AreEqual("INFO [ScreenOpen] Clan open: 0.0 ms", _logger.Lines.Single());
    }

    [TestMethod]
    public void End_FormatsWithTheInvariantCulture()
    {
        var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var window = _sut.Begin("Clan", "open");
            _clock.Advance(1500);

            _sut.End(window, null);

            Assert.AreEqual("INFO [ScreenOpen] Clan open: 1500.0 ms", _logger.Lines.Single());
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [TestMethod]
    public void Facade_Unwired_IsASafeNoOp()
    {
        ScreenOpenTiming.Initialize(null);

        var window = ScreenOpenTiming.Begin("Clan", "open");
        ScreenOpenTiming.End(window, null, "d");

        Assert.IsNull(window);
    }

    [TestMethod]
    public void Facade_Wired_RoutesToTheTimer()
    {
        ScreenOpenTiming.Initialize(_sut);

        var window = ScreenOpenTiming.Begin("Clan", "open");
        _clock.Advance(7);
        ScreenOpenTiming.End(window, null);

        Assert.AreEqual("INFO [ScreenOpen] Clan open: 7.0 ms", _logger.Lines.Single());
    }
}
