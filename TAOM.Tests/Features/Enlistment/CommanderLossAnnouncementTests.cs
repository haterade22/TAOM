using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The moment a commander stops having a command, and what the player is told about it.
///
/// Before this, the transition was completely silent — grepping `ShowMessage|_inquiry` across
/// <c>EnlistmentReconciler</c> and <c>DischargeService</c> returned nothing at all. A player went
/// from invisible soldier to lone visible hero in a war zone with no explanation, standing exactly
/// where their company had just been annihilated (<c>RestorePresence</c> does not move the party).
/// A live session on 2026-08-09 measured 73 real seconds from that transition to the same lone
/// hero being inside a 1,544-combatant sally-out.
/// </summary>
[TestClass]
public class CommanderLossAnnouncementTests
{
    private const double Now = 200.0;

    /// <summary>Parameter index of <c>bodyKey</c> in <c>ShowTwoOptionInquiry</c>.</summary>
    private const int BodyKeyArg = 2;

    /// <summary>Parameter index of <c>onOptionB</c> — the "count your service ended" callback.</summary>
    private const int OnOptionBArg = 9;

    private const int BodyVariablesArg = 12;
    private const int PrioritizeArg = 13;

    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private IMobilePartyAttachmentAdapter _partyAdapter = null!;
    private ICommanderLordAdapter _commander = null!;
    private IInquiryAdapter _inquiry = null!;
    private EnlistmentReconciler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        var machine = new EnlistmentStateMachine(_store, _logger);

        _partyAdapter = Substitute.For<IMobilePartyAttachmentAdapter>();
        _partyAdapter.RestorePresence().Returns(true);
        _partyAdapter.ParkNear(Arg.Any<string>()).Returns(true);
        _partyAdapter.SyncPositionTo(Arg.Any<string>()).Returns(true);
        _partyAdapter.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isCaptive: false,
            isActive: false, isVisible: false, isInMapEvent: false, hasPlayerEncounter: false));

        var attachment = new ServiceAttachmentService(
            _partyAdapter, Substitute.For<IGameMenuAdapter>(), _logger);
        var discharge = new DischargeService(
            _store, machine, _partyAdapter, Substitute.For<IEncounterAdapter>(),
            new EncounterOwnershipPolicy(), Substitute.For<ICommanderLordAdapter>(),
            Substitute.For<IGameMenuAdapter>(), _logger);

        _commander = Substitute.For<ICommanderLordAdapter>();
        _inquiry = Substitute.For<IInquiryAdapter>();

        _sut = new EnlistmentReconciler(
            _store, machine, attachment, _commander, discharge,
            new EnlistmentConfigProvider(_logger), Substitute.For<IEncounterAdapter>(),
            new EncounterOwnershipPolicy(), Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            EnlistmentTestDoubles.FeatureOn(), _inquiry, _logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.EnlistedAtDay = 100.0;
    }

    /// <summary>Alive, no party — the shape both loss cases share.</summary>
    private void CommanderLost(bool prisoner, string town = null, string captor = null)
        => _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: prisoner,
            partyId: null, partyIsActive: false,
            captorName: captor, captivitySettlementName: town, name: "Theoden"));

    private object[] SingleInquiryArgs()
    {
        var calls = new List<NSubstitute.Core.ICall>();
        foreach (var call in _inquiry.ReceivedCalls())
        {
            if (call.GetMethodInfo().Name == nameof(IInquiryAdapter.ShowTwoOptionInquiry))
                calls.Add(call);
        }

        Assert.AreEqual(1, calls.Count, $"expected exactly one inquiry, saw {calls.Count}");
        return calls[0].GetArguments();
    }

    [TestMethod]
    public void CommanderCapturedInAKnownTown_NamesTheCaptorAndThePlace()
    {
        // Captivity is the ONLY loss case with a location — a lord whose party was merely destroyed
        // has no position at all until the engine respawns him. That is the whole reason the cases
        // are split in the TEXT while sharing one clock.
        CommanderLost(prisoner: true, town: "Orthanc", captor: "Isengard");

        _sut.ReconcileHourly(Now);

        var args = SingleInquiryArgs();
        Assert.AreEqual("taom_enlist_lost_captured_body", args[BodyKeyArg]);
        var vars = (IReadOnlyDictionary<string, string>)args[BodyVariablesArg];
        Assert.AreEqual("Isengard", vars["CAPTOR"]);
        Assert.AreEqual("Orthanc", vars["TOWN"]);
        Assert.AreEqual("Theoden", vars["COMMANDER"]);
    }

    [TestMethod]
    public void CommanderCapturedSomewhereUnknown_UsesTheLocationlessLine()
    {
        // IsPrisoner is true but PartyBelongedToAsPrisoner gave us nothing. Naming a town we do not
        // have would be worse than admitting we do not know.
        CommanderLost(prisoner: true, town: null);

        _sut.ReconcileHourly(Now);

        Assert.AreEqual("taom_enlist_lost_captured_unknown_body", SingleInquiryArgs()[BodyKeyArg]);
    }

    [TestMethod]
    public void CommanderFreeButPartyDestroyed_UsesTheBrokenCompanyLine()
    {
        CommanderLost(prisoner: false);

        _sut.ReconcileHourly(Now);

        Assert.AreEqual("taom_enlist_lost_broken_body", SingleInquiryArgs()[BodyKeyArg]);
    }

    [TestMethod]
    public void TheModalIsPrioritized()
    {
        // ShowInquiry ENQUEUES a non-prioritized inquiry behind whatever is on screen, and this
        // fires in the tick after a battle — exactly when vanilla raises its own ransom and peace
        // popups. Queued, it reaches the player minutes later with no context.
        CommanderLost(prisoner: false);

        _sut.ReconcileHourly(Now);

        Assert.AreEqual(true, SingleInquiryArgs()[PrioritizeArg]);
    }

    [TestMethod]
    public void TheModalIsRaisedOncePerEpisode_NotEveryHour()
    {
        // The reconciler runs hourly for the whole seven-day grace. Without the latch the player is
        // asked the same question every game hour until they answer it.
        CommanderLost(prisoner: false);

        _sut.ReconcileHourly(Now);
        _sut.ReconcileHourly(Now + 1.0);
        _sut.ReconcileHourly(Now + 2.0);

        SingleInquiryArgs();   // asserts exactly one
    }

    [TestMethod]
    public void ChoosingToEndService_ActuallyEndsIt()
    {
        CommanderLost(prisoner: false);
        _sut.ReconcileHourly(Now);

        var onOptionB = (Action)SingleInquiryArgs()[OnOptionBArg];
        Assert.IsNotNull(onOptionB, "the 'count your service ended' option has no consequence");
        onOptionB();

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State,
            "choosing to end service must end it, not merely close the popup");
    }

    [TestMethod]
    public void CommanderDead_DischargesAndDoesNotAsk()
    {
        // Death is not a fork. There is nothing to wait for, so there is nothing to choose.
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: false, name: "Theoden"));

        _sut.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _inquiry.DidNotReceiveWithAnyArgs().ShowTwoOptionInquiry(
            default, default, default, default, default, default, default, default,
            default, default, default, default, default, default);
    }
}
