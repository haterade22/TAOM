using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CastleRecruitment.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.CultureConversion;
using TAOM.Features.CultureConversion.Hooks;
using TAOM.Features.Diplomacy;
using TAOM.Features.Messengers;
using TAOM.Features.Siege;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Proves each gated behaviour actually STANDS DOWN on a co-op client.
///
/// Why these are worth writing rather than trusting the one-line gate: BannerlordCoop does not
/// suppress the client's GLOBAL DailyTickEvent/HourlyTickEvent (verified against the installed
/// v1.4.7 engine — <c>Campaign.Tick()</c> drives the global events through
/// <c>OnTick</c>/<c>SignalPeriodicEvents</c>, a SEPARATE call from the <c>TickPeriodicEvents</c> that
/// Coop's <c>PartyTickPatch</c> prefixes). So these handlers DO run on a client, and the gate is the
/// only thing stopping a second peer re-running world mutation the host already replicated.
///
/// Each test asserts the handler does not merely fail to mutate, but never TOUCHES its service —
/// an early return is the contract. Asserting "no state changed" instead would pass for a handler
/// that ran fully and happened to no-op on empty data.
///
/// The authority-true complement is deliberately NOT asserted for most of these: past the gate they
/// reach <c>Campaign.Current</c>, which does not exist in a test host. <c>RaceAgeBehaviorTests</c>
/// covers both directions because its handler goes through an injected adapter instead.
/// </summary>
[TestClass]
public class CoopAuthorityGateTests
{
    private static ICoopSessionProvider Client()
    {
        var s = Substitute.For<ICoopSessionProvider>();
        s.IsAuthority.Returns(false);
        s.IsCoopClient.Returns(true);
        s.IsSessionActive.Returns(true);
        s.ShouldDeferToHost.Returns(true);
        return s;
    }

    /// <summary>
    /// Host, or plain singleplayer — both are "authority". Safe to assert against only where the
    /// behaviour delegates straight to a substituted service rather than reaching
    /// <c>Campaign.Current</c> (see the class remark above).
    /// </summary>
    private static ICoopSessionProvider Authority()
    {
        var s = Substitute.For<ICoopSessionProvider>();
        s.IsAuthority.Returns(true);
        s.IsCoopClient.Returns(false);
        s.IsSessionActive.Returns(true);
        s.ShouldDeferToHost.Returns(false);
        return s;
    }

    /// <summary>
    /// Plain singleplayer — no co-op module in play at all. Distinct from <see cref="Authority"/>,
    /// which is a co-op HOST. Both keep TAOM's own rules, so both must leave
    /// <c>ShouldDeferToHost</c> false: gating shared-world decisions on mere co-op PRESENCE was a
    /// real bug, disabling TAOM's diplomacy for a solo player who merely had the module enabled.
    /// </summary>
    private static ICoopSessionProvider Solo()
    {
        var s = Substitute.For<ICoopSessionProvider>();
        s.IsAuthority.Returns(true);
        s.IsCoopClient.Returns(false);
        s.IsSessionActive.Returns(false);
        s.ShouldDeferToHost.Returns(false);
        return s;
    }

    // --- CultureConversion — the crash path -----------------------------------------------------

    [TestMethod]
    public void CultureConversion_OnDailyTick_CoopClient_DoesNotRunDailyChecks()
    {
        // Highest-stakes gate in the set. The client's store holds the SAME pending records as the
        // host (it loaded the host's save), so ungated it matures the same conversions and calls
        // HeroCreator.CreateNotable — which on a client hits Coop's suppressed MBObjectBase.StringId
        // setter and throws ArgumentNullException out of MBObjectManager.
        var service = Substitute.For<ICultureConversionService>();
        var sut = new CultureConversionBehavior(
            service, Substitute.For<ICultureConversionStore>(), Substitute.For<IModLogger>(), Client());

        sut.OnDailyTick();

        service.DidNotReceive().RunDailyChecks(Arg.Any<double>());
    }

    [TestMethod]
    public void CultureConversion_OnGameLoaded_CoopClient_DoesNotReapplyCultures()
    {
        // Separate gate from the tick: a joining client loads the HOST'S save, so cultures are
        // already converted. Settlement.Culture is also a Coop-autosynced field, so a client write
        // is either rejected or echoed back as a spurious change.
        var service = Substitute.For<ICultureConversionService>();
        var sut = new CultureConversionBehavior(
            service, Substitute.For<ICultureConversionStore>(), Substitute.For<IModLogger>(), Client());

        sut.OnGameLoaded(null);

        service.DidNotReceive().ReapplyConvertedCultures();
    }

    // --- War of the Ring ------------------------------------------------------------------------

    [TestMethod]
    public void WarOfTheRing_OnDailyTick_CoopClient_DoesNotCheckPhaseTransition()
    {
        // Phase lives in TAOM SyncData that no co-op mod replicates, and the client's clock is
        // slewed rather than identical — so both peers would cross the threshold independently and
        // issue duplicate DeclareWar calls between AI kingdoms.
        var service = Substitute.For<IWarOfTheRingService>();
        var sut = new WarOfTheRingBehavior(service, Substitute.For<IModLogger>(), Client());

        sut.OnDailyTick();

        service.DidNotReceive().CheckPhaseTransition(Arg.Any<float>());
    }

    // --- Siege defence --------------------------------------------------------------------------

    // Host-only as a whole, and the reasoning matters because a 2026-08-01 change split this and
    // had to be reverted. The reward looks per-player — it pays Hero.MainHero — but its
    // PRECONDITIONS (PlayerAccepted, RewardClaimed) live on the shared _activeEvents entries
    // serialised into _taom_siege_active_events, and a joining client's baseline for that key is
    // the HOST's save. A client running the reward path would inherit the host's acceptance and
    // claim something it never earned. "Keyed on MainHero" was true of the payout, false of the
    // decision to pay out.

    [TestMethod]
    public void SiegeDefense_OnHourlyTick_CoopClient_DoesNotTick()
    {
        var service = Substitute.For<ISiegeDefenseService>();
        var sut = new SiegeDefenseBehavior(service, Substitute.For<IModLogger>(), Client());

        sut.OnHourlyTick();

        service.DidNotReceive().OnHourlyTickShared();
    }

    [TestMethod]
    public void SiegeDefense_OnHourlyTick_Authority_Ticks()
    {
        var service = Substitute.For<ISiegeDefenseService>();
        var sut = new SiegeDefenseBehavior(service, Substitute.For<IModLogger>(), Authority());

        sut.OnHourlyTick();

        service.Received(1).OnHourlyTickShared();
    }

    // --- Diplomacy load-time enforcement (Codex P1) ----------------------------------------------
    // The fifth diplomacy path. It consults NO predicate — it calls MakePeace/StartAlliance straight
    // from TAOM config — which is exactly why the veto scan, which looks for predicate consumers,
    // could not see it. Gated on ShouldDeferToHost rather than raw IsAuthority: the latter is
    // Coop-specific and fails open, so under BannerlordTogether it reports true on both peers and
    // would gate nothing.

    [TestMethod]
    public void Diplomacy_OnSessionLaunched_CoopActive_DoesNotEnforcePermanentAlliances()
    {
        var service = Substitute.For<IDiplomacyService>();
        var sut = new DiplomacyBehavior(service, Substitute.For<IModLogger>(), Client());

        sut.OnSessionLaunched();

        service.DidNotReceive().EnforcePermanentAlliances();
    }

    [TestMethod]
    public void Diplomacy_OnSessionLaunched_Solo_EnforcesPermanentAlliances()
    {
        var service = Substitute.For<IDiplomacyService>();
        var sut = new DiplomacyBehavior(service, Substitute.For<IModLogger>(), Solo());

        sut.OnSessionLaunched();

        service.Received(1).EnforcePermanentAlliances();
    }

    // --- Messengers -----------------------------------------------------------------------------

    [TestMethod]
    public void Messengers_OnHourlyTick_CoopClient_DoesNotReadSettingsOrProcess()
    {
        // The store is TAOM SyncData, so a client loads the HOST'S in-flight messengers — and
        // arrival writes MobileParty.MainParty.Position, which on a client is that player's OWN
        // party. Asserting the settings read never happens proves the gate precedes everything.
        var settings = Substitute.For<IMessengerSettingsProvider>();
        var store = Substitute.For<IMessengerStateStore>();
        var sut = new MessengerCampaignBehavior(
            Substitute.For<IMessengerService>(), store, settings, Substitute.For<IModLogger>(), Client());

        sut.OnHourlyTick();

        _ = settings.DidNotReceive().EnableMessengers;
        _ = store.DidNotReceive().Count;
    }

    // --- Castle recruitment — the load-path object creation --------------------------------------

    [TestMethod]
    public void CastleRecruitment_OnGameLoaded_CoopClient_DoesNotEnsureCastles()
    {
        // The one castle path a client actually reaches: the daily work is on
        // DailyTickSettlementEvent (client-blocked by Coop), but OnGameLoadedEvent fires on every
        // peer because a joining client loads the host's save through the normal pipeline.
        var service = Substitute.For<ICastleRecruitmentService>();
        var sut = new CastleRecruitmentBehavior(service, Substitute.For<IModLogger>(), Client());

        sut.OnGameLoaded(null);

        _ = service.DidNotReceive().IsEnabled;
    }

    [TestMethod]
    public void CastleRecruitment_OnNewGameCreated_CoopClient_DoesNotEnsureCastles()
    {
        var service = Substitute.For<ICastleRecruitmentService>();
        var sut = new CastleRecruitmentBehavior(service, Substitute.For<IModLogger>(), Client());

        sut.OnNewGameCreated(null);

        _ = service.DidNotReceive().IsEnabled;
    }

    // --- The complement, on the one handler that can run past the gate in a test host ------------

    [TestMethod]
    public void CastleRecruitment_OnGameLoaded_Authority_ProceedsPastTheGate()
    {
        // Guards against the gates being "fixed" by disabling the feature outright — proves an
        // authoritative peer gets PAST the gate, not just that a client stops.
        //
        // CastleRecruitment is the right handler for this: past the gate it reads _service.IsEnabled,
        // which a substitute returns false for, so it stops there and never reaches the engine.
        // CultureConversion cannot be used — its handler evaluates CampaignTime.Now immediately after
        // the gate, and that NREs without a live Campaign (verified: it threw when first attempted).
        var service = Substitute.For<ICastleRecruitmentService>();
        var host = Substitute.For<ICoopSessionProvider>();
        host.IsAuthority.Returns(true);
        var sut = new CastleRecruitmentBehavior(service, Substitute.For<IModLogger>(), host);

        sut.OnGameLoaded(null);

        _ = service.Received(1).IsEnabled;
    }
}
