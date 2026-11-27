using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Marshal's Word stage M7 in the Seat's state machine: when the screen tick hands the panel's clicks to the relay, and what it writes about it.</summary>
[TestClass]
public class SeatSessionPanelClickTests
{
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private FakeSeatScreen _screen = null!;
    private FakeSeatSettings _settings = null!;
    private RecordingOutcomeLog _log = null!;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _screen = new FakeSeatScreen();
        _settings = new FakeSeatSettings();
        _log = new RecordingOutcomeLog();
        _sut = new SeatSession(_screen, new FakeSeatHero(), _settings, () => Allowed, _log, Substitute.For<IModLogger>(), "7");
        _sut.OnScreenTick(true);
        for (var i = 0; i < 20 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private void Frame() => _sut.OnScreenTick(false);

    [TestMethod]
    public void PanelOpenWithTheCursorShown_RelaysOncePerScreenTick()
    {
        _screen.PanelOpen = true;

        Frame();
        Frame();
        Frame();

        Assert.AreEqual(3, _screen.Relays);
    }

    [TestMethod]
    public void PanelClosed_RelaysNothing()
    {
        Frame();
        Frame();

        Assert.AreEqual(0, _screen.Relays);
    }

    [TestMethod]
    public void TheCursorSettingOff_RelaysNothing()
    {
        _settings.CursorWithPanel = false;
        _screen.PanelOpen = true;

        Frame();

        Assert.AreEqual(0, _screen.Relays);
    }

    [TestMethod]
    public void LookingWithTheMiddleButton_RelaysNothing()
    {
        _screen.PanelOpen = true;
        _screen.Look = true;

        Frame();

        Assert.AreEqual(0, _screen.Relays);
    }

    [TestMethod]
    public void TheFirstRelayedClick_WritesOnePassLine_AndNoSecond()
    {
        _screen.PanelOpen = true;
        _screen.RelayResult = SeatRelayResult.Acted;

        Frame();
        Frame();

        Assert.AreEqual(1, _log.Count("pass", SeatChecks.PanelClickId));
        Assert.AreEqual(1, _log.Calls("pass", SeatChecks.PanelClickId), "the session writes the line once, not once a click");
    }

    [TestMethod]
    public void AnIdleRelay_WritesNoLine()
    {
        _screen.PanelOpen = true;
        _screen.RelayResult = SeatRelayResult.Idle;

        Frame();

        Assert.AreEqual(0, _log.Count("pass", SeatChecks.PanelClickId) + _log.Count("fail", SeatChecks.PanelClickId));
    }

    [TestMethod]
    public void AMissingViewModelSite_WritesOneFailLine_AndTheSeatKeepsRunning()
    {
        _screen.PanelOpen = true;
        _screen.RelayResult = SeatRelayResult.SiteMissing;

        Frame();
        Frame();

        Assert.AreEqual(1, _log.Count("fail", SeatChecks.PanelClickId));
        Assert.AreEqual(1, _log.Calls("fail", SeatChecks.PanelClickId));
        Assert.IsFalse(_sut.Faulted);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    [TestMethod]
    public void ARelayThatThrows_FaultsTheSeatOnce_AsEveryOtherScreenWriteDoes()
    {
        _screen.PanelOpen = true;
        _screen.RelayThrows = new System.InvalidOperationException("relay");

        Frame();

        Assert.IsTrue(_sut.Faulted);
    }
}
