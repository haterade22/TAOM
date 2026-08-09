using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Features.AutoResolveDiagnostics;

namespace TAOM.Tests.Features.AutoResolveDiagnostics;

/// <summary>
/// The handlers are reachable because they are internal + InternalsVisibleTo. MapEvent and
/// PartyBase cannot be constructed in a unit test, so these drive the null path, the toggle gates
/// and the session boundary — which is where the real hazards are. The write side (census, ids,
/// emit) is covered by <see cref="AutoResolveLogWriterTests"/>.
/// </summary>
[TestClass]
public class AutoResolveDiagnosticsBehaviorTests
{
    private IMapEventBattleLogAdapter _adapter = null!;
    private IAutoResolveLogWriter _writer = null!;
    private IAutoResolveDiagnosticsSettingsProvider _settings = null!;
    private AutoResolveDiagnosticsBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IMapEventBattleLogAdapter>();
        _writer = Substitute.For<IAutoResolveLogWriter>();
        _settings = Substitute.For<IAutoResolveDiagnosticsSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.IsCensusEnabled.Returns(true);
        _sut = new AutoResolveDiagnosticsBehavior(_adapter, _writer, _settings);
    }

    // ---- gating -------------------------------------------------------------------------------

    [TestMethod]
    public void OnMapEventEnded_WhenDisabled_CapturesNothingAndEmitsNothing()
    {
        _settings.IsEnabled.Returns(false);

        _sut.OnMapEventEnded(null!);

        _adapter.DidNotReceiveWithAnyArgs().Capture(default!, default!, default!);
        _writer.DidNotReceiveWithAnyArgs().Emit(default!);
    }

    [TestMethod]
    public void OnMapEventEnded_WithNullMapEvent_DoesNotThrowAndEmitsNothing()
    {
        _sut.OnMapEventEnded(null!);

        _writer.DidNotReceiveWithAnyArgs().Emit(default!);
    }

    [TestMethod]
    public void OnMapEventEnded_AlwaysClosesTheSnapshot_EvenWhenDisabled()
    {
        // The toggle gates I/O only. If it gated the closer too, flipping it mid-session would
        // strand a pending entry per battle for the rest of the run — the latch failure that
        // shipped three times (rca-tournament-exit-hang-2026-07-06.md).
        _sut.OnMapEventStarted(null!, null!, null!);
        _settings.IsEnabled.Returns(false);

        _sut.OnMapEventEnded(null!);

        Assert.AreEqual(0, _sut.TrackedBattles);
    }

    [TestMethod]
    public void OnMapEventStarted_WithNullEvent_TracksNothing()
    {
        _sut.OnMapEventStarted(null!, null!, null!);

        Assert.AreEqual(0, _sut.TrackedBattles);
    }

    [TestMethod]
    public void OnPartyAddedToMapEvent_WhenDisabled_TouchesTheAdapterNotAtAll()
    {
        // Regression: this handler used to gate only on _pending occupancy, so a toggle flipped
        // OFF mid-battle left every subsequent reinforcement still doing real adapter work on an
        // already-tracked battle. Four independent reviews flagged it.
        _settings.IsEnabled.Returns(false);

        _sut.OnPartyAddedToMapEvent(null!);

        _adapter.DidNotReceiveWithAnyArgs().SnapshotParty(default!, default!);
        _adapter.DidNotReceiveWithAnyArgs().SnapshotStart(default!);
    }

    [TestMethod]
    public void OnPartyAddedToMapEvent_NeverRerunsAFullStartSnapshot()
    {
        // The late-joiner path must fold in ONE party. Re-running SnapshotStart re-derives every
        // party on both sides plus both leaders, morale and advantage and discards all of it — and
        // PartyBase.MapEventSide's setter recurses into AttachedParties, so a reinforcing army
        // raises this event once per attached party, making that shape quadratic.
        _sut.OnPartyAddedToMapEvent(null!);

        _adapter.DidNotReceiveWithAnyArgs().SnapshotStart(default!);
    }

    // ---- the start-capture gate ---------------------------------------------------------------
    // These drive a pure seam rather than the handler because MapEvent cannot be constructed in a
    // unit test and the null path returns before the settings check ever runs — asserting
    // DidNotReceive().SnapshotStart(...) with a null MapEvent would pass whether or not the gate
    // exists, which is the vacuous-negative trap testing-qa.md warns about.

    [TestMethod]
    public void ShouldCaptureStart_WhenDisabled_IsFalse()
    {
        // THE guard. Reddened by deleting `&& isEnabled` from the seam.
        Assert.IsFalse(AutoResolveDiagnosticsBehavior.ShouldCaptureStart(
            isEnabled: false, hasMapEvent: true, pendingCount: 0));
    }

    [TestMethod]
    public void ShouldCaptureStart_WhenEnabledWithRoom_IsTrue()
    {
        // Paired positive — without it the negative above passes on a hard-coded false.
        Assert.IsTrue(AutoResolveDiagnosticsBehavior.ShouldCaptureStart(
            isEnabled: true, hasMapEvent: true, pendingCount: 0));
    }

    [TestMethod]
    public void ShouldCaptureStart_WithNoMapEvent_IsFalse()
    {
        Assert.IsFalse(AutoResolveDiagnosticsBehavior.ShouldCaptureStart(
            isEnabled: true, hasMapEvent: false, pendingCount: 0));
    }

    [TestMethod]
    public void ShouldCaptureStart_AtTrackingCap_IsFalse()
    {
        Assert.IsFalse(AutoResolveDiagnosticsBehavior.ShouldCaptureStart(
            isEnabled: true, hasMapEvent: true,
            pendingCount: AutoResolveDiagnosticsBehavior.MaxTrackedBattles));
    }

    [TestMethod]
    public void ShouldCaptureStart_OneBelowCap_IsTrue()
    {
        // Pins the boundary as `<` and not `<=`, so the cap cannot silently drift by one.
        Assert.IsTrue(AutoResolveDiagnosticsBehavior.ShouldCaptureStart(
            isEnabled: true, hasMapEvent: true,
            pendingCount: AutoResolveDiagnosticsBehavior.MaxTrackedBattles - 1));
    }

    // ---- session boundary ---------------------------------------------------------------------

    [TestMethod]
    public void OnSessionLaunched_ResetsTheWriterAndWritesTheCensus()
    {
        _sut.OnSessionLaunched();

        Received.InOrder(() =>
        {
            _writer.BeginSession();
            _writer.WriteCensus();
        });
    }

    [TestMethod]
    public void OnSessionLaunched_ClearsTrackingFromThePreviousCampaign()
    {
        _sut.OnMapEventStarted(null!, null!, null!);

        _sut.OnSessionLaunched();

        Assert.AreEqual(0, _sut.TrackedBattles);
    }

    [TestMethod]
    public void OnSessionLaunched_WhenTheWriterThrows_DoesNotPropagate()
    {
        // The one containment path that is genuinely reachable in a unit test. The previous
        // "adapter throws" test called OnMapEventEnded(null!), which returns at the null guard
        // before the adapter is ever touched — it passed whether or not the try/catch existed, and
        // was a recurrence of RCA finding #8 in this feature's own review log.
        _writer.When(w => w.BeginSession()).Throw(new System.Exception("engine not ready"));

        _sut.OnSessionLaunched();   // must not throw — this runs during session launch
    }
}
