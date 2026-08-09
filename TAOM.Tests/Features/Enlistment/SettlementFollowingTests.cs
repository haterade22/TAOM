using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Settlement following: the player goes INTO the commander's settlement instead of standing
/// invisibly outside the gate for the whole stop. Two verdicts and one indivisible transaction,
/// where the failure modes are a party in two places at once and a player inside a settlement
/// with no menu (the donor's crash).
/// </summary>
[TestClass]
public class SettlementFollowingTests
{
    private IMobilePartyAttachmentAdapter _attachment;
    private IGameMenuAdapter _gameMenu;
    private ServiceAttachmentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _attachment = Substitute.For<IMobilePartyAttachmentAdapter>();
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        _attachment.RestorePresence().Returns(true);
        _attachment.MoveIntoSettlement(Arg.Any<string>()).Returns(true);
        _attachment.LeaveSettlement().Returns(true);
        _attachment.ParkNear(Arg.Any<string>()).Returns(true);
        _gameMenu.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _sut = new ServiceAttachmentService(_attachment, _gameMenu, Substitute.For<IModLogger>());
    }

    private static CommanderSnapshot Commander(
        string settlementId = null, bool inMapEvent = false) =>
        new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true,
            partyIsInMapEvent: inMapEvent,
            partyIsInSettlement: !string.IsNullOrEmpty(settlementId),
            settlementId: settlementId);

    private static PlayerPresenceSnapshot Player(
        string settlementId = null, bool inMapEvent = false, bool parked = true) =>
        new PlayerPresenceSnapshot(
            mainPartyExists: true,
            isActive: !parked, isVisible: !parked,
            settlementId: settlementId, isInMapEvent: inMapEvent);

    private AttachmentStatus Assess(CommanderSnapshot c, PlayerPresenceSnapshot p) =>
        _sut.Assess(EnlistmentState.EnlistedAttached, c, p).Status;

    // ---- Assess ---------------------------------------------------------------------------

    [TestMethod]
    public void Assess_CommanderInSettlementPlayerOutside_SettlementFollowRequired()
    {
        Assert.AreEqual(AttachmentStatus.SettlementFollowRequired,
            Assess(Commander("town_ES1"), Player()));
    }

    [TestMethod]
    public void Assess_BothInSameSettlement_Attached_NotAttachRequired()
    {
        // Inside the walls the party is deliberately active and visible, so LooksParked is false.
        // Returning AttachRequired here would re-park the player back outside every single hour.
        Assert.AreEqual(AttachmentStatus.Attached,
            Assess(Commander("town_ES1"), Player("town_ES1", parked: false)));
    }

    [TestMethod]
    public void Assess_PlayerInSettlementCommanderLeft_SettlementExitRequired()
    {
        Assert.AreEqual(AttachmentStatus.SettlementExitRequired,
            Assess(Commander(), Player("town_ES1", parked: false)));
    }

    [TestMethod]
    public void Assess_PlayerInDifferentSettlement_SettlementExitRequired()
    {
        Assert.AreEqual(AttachmentStatus.SettlementExitRequired,
            Assess(Commander("town_ES2"), Player("town_ES1", parked: false)));
    }

    [TestMethod]
    public void Assess_PlayerInSettlementCommanderInMapEvent_SettlementExitRequired_NotBattleJoin()
    {
        // THE ordering pin. Exit is checked ABOVE the battle branch: joining a map event while
        // CurrentSettlement still points at another settlement puts the party in two places at
        // once, and for a joining DEFENDER the engine rewrites a siege assault to SiegeOutside
        // off exactly that field — turning an assault on the walls into a field fight for
        // everyone in it.
        Assert.AreEqual(AttachmentStatus.SettlementExitRequired,
            Assess(Commander(inMapEvent: true), Player("town_ES1", parked: false)));
    }

    [TestMethod]
    public void Assess_BesiegedTogether_DoesNotExit_SoTheSiegeTypeSurvives()
    {
        // The player inside the settlement the commander is defending is the CORRECT state, and
        // the reason this batch improves siege joins rather than risking them.
        Assert.AreEqual(AttachmentStatus.BattleJoinRequired,
            Assess(Commander("town_ES1", inMapEvent: true), Player("town_ES1", parked: false)));
    }

    [TestMethod]
    public void Assess_NeitherInSettlement_UnchangedParkedBehaviour()
    {
        Assert.AreEqual(AttachmentStatus.Attached, Assess(Commander(), Player()));
        Assert.AreEqual(AttachmentStatus.AttachRequired, Assess(Commander(), Player(parked: false)));
    }

    [TestMethod]
    public void Assess_CaptivityStillOutranksSettlementFollowing()
    {
        var player = new PlayerPresenceSnapshot(mainPartyExists: true, isCaptive: true, settlementId: "town_ES1");
        var result = _sut.Assess(EnlistmentState.EnlistedAttached, Commander("town_ES2"), player);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.PlayerCaptive, result.BlockReason);
    }

    [TestMethod]
    public void Assess_MissingCommanderStillOutranksSettlementExit()
    {
        var result = _sut.Assess(
            EnlistmentState.EnlistedAttached, CommanderSnapshot.Missing, Player("town_ES1", parked: false));

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.CommanderPartyMissing, result.BlockReason);
    }

    // ---- FollowCommanderIntoSettlement: one transaction ------------------------------------

    [TestMethod]
    public void Follow_HappyPath_RestoresPresenceMovesInAndAssertsTheMenu()
    {
        Assert.IsTrue(_sut.FollowCommanderIntoSettlement("lord_1", "town_ES1"));

        Received.InOrder(() =>
        {
            _attachment.RestorePresence();
            _attachment.MoveIntoSettlement("town_ES1");
            _gameMenu.EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
        });
    }

    [TestMethod]
    public void Follow_PresenceRestoreFails_DoesNotMoveIn()
    {
        // The engine skips inactive parties in placement — moving in from a hidden state is how
        // a party ends up half-placed.
        _attachment.RestorePresence().Returns(false);

        Assert.IsFalse(_sut.FollowCommanderIntoSettlement("lord_1", "town_ES1"));
        _attachment.DidNotReceive().MoveIntoSettlement(Arg.Any<string>());
    }

    [TestMethod]
    public void Follow_MoveFails_ReParksOutside()
    {
        _attachment.MoveIntoSettlement(Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_sut.FollowCommanderIntoSettlement("lord_1", "town_ES1"));
        _attachment.Received(1).ParkNear("lord_1");
        _gameMenu.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
    }

    [TestMethod]
    public void Follow_MenuFails_LeavesTheSettlementRatherThanSittingInItWithNoMenu()
    {
        // MANDATORY rollback. Inside a settlement with no menu of ours is the donor's crash:
        // the next vanilla settlement-menu init dereferences PlayerEncounter.EncounterSettlement
        // with no live encounter.
        _gameMenu.EnsureMenuOpen(Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_sut.FollowCommanderIntoSettlement("lord_1", "town_ES1"));
        _attachment.Received(1).LeaveSettlement();
        _attachment.Received(1).ParkNear("lord_1");
    }

    [TestMethod]
    public void Follow_NoSettlementId_DoesNothing()
    {
        Assert.IsFalse(_sut.FollowCommanderIntoSettlement("lord_1", null));
        _attachment.DidNotReceive().RestorePresence();
        _attachment.DidNotReceive().MoveIntoSettlement(Arg.Any<string>());
    }

    // ---- ExitSettlementForService -----------------------------------------------------------

    [TestMethod]
    public void Exit_LeavesThenReParks()
    {
        Assert.IsTrue(_sut.ExitSettlementForService("lord_1"));

        Received.InOrder(() =>
        {
            _attachment.LeaveSettlement();
            _attachment.ParkNear("lord_1");
        });
    }

    [TestMethod]
    public void Exit_LeaveFails_DoesNotParkOnTopOfAStuckParty()
    {
        // Parking a party that is still inside a settlement would hide it there — invisible AND
        // stuck, which is strictly harder to diagnose than stuck alone.
        _attachment.LeaveSettlement().Returns(false);

        Assert.IsFalse(_sut.ExitSettlementForService("lord_1"));
        _attachment.DidNotReceive().ParkNear(Arg.Any<string>());
    }
}

/// <summary>
/// The reconciler's half of settlement following: routing the two new verdicts, and the two
/// mandatory exemptions without which a correct settlement stop is logged as an anomaly every
/// hour and the player is dragged back outside the gate.
/// </summary>
[TestClass]
public class SettlementFollowingReconcilerTests
{
    private IModLogger _logger;
    private EnlistmentStore _store;
    private EnlistmentStateMachine _machine;
    private ICommanderLordAdapter _commander;
    private IMobilePartyAttachmentAdapter _partyAdapter;
    private IServiceAttachmentService _attachment;
    private EnlistmentReconciler _sut;

    private const double Now = 200.0;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _partyAdapter = Substitute.For<IMobilePartyAttachmentAdapter>();
        _attachment = Substitute.For<IServiceAttachmentService>();

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.EnlistedAtDay = 100.0;
        _store.Record.ContractEndDay = 465.0;

        var discharge = new DischargeService(
            _store, _machine, _partyAdapter, Substitute.For<IEncounterAdapter>(),
            new EncounterOwnershipPolicy(), Substitute.For<ICommanderLordAdapter>(),
            Substitute.For<IGameMenuAdapter>(), _logger);

        _sut = new EnlistmentReconciler(
            _store, _machine, _attachment, _commander, discharge,
            new EnlistmentConfigProvider(_logger), Substitute.For<IEncounterAdapter>(),
            new EncounterOwnershipPolicy(),
            Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            EnlistmentTestDoubles.FeatureOn(), Substitute.For<IInquiryAdapter>(), _logger);
    }

    private void Commander(string settlementId = null)
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            partyIsInSettlement: !string.IsNullOrEmpty(settlementId),
            settlementId: settlementId, name: "Lord Test"));
    }

    private void Presence(string settlementId = null, bool parked = true)
    {
        _attachment.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isActive: !parked, isVisible: !parked, settlementId: settlementId));
    }

    private void Verdict(AttachmentStatus status)
    {
        _attachment.Assess(Arg.Any<EnlistmentState>(), Arg.Any<CommanderSnapshot>(), Arg.Any<PlayerPresenceSnapshot>())
            .Returns(new AttachmentAssessment(status));
    }

    [TestMethod]
    public void ReconcileHourly_SettlementFollowRequired_FollowsTheCommanderIn()
    {
        Commander("town_ES1");
        Presence();
        Verdict(AttachmentStatus.SettlementFollowRequired);

        _sut.ReconcileHourly(Now);

        _attachment.Received(1).FollowCommanderIntoSettlement("lord_1_1", "town_ES1");
    }

    [TestMethod]
    public void ReconcileHourly_SettlementExitRequired_LeavesTheSettlement()
    {
        Commander();
        Presence("town_ES1", parked: false);
        Verdict(AttachmentStatus.SettlementExitRequired);

        _sut.ReconcileHourly(Now);

        _attachment.Received(1).ExitSettlementForService("lord_1_1");
    }

    [TestMethod]
    public void ReconcileHourly_AttachedWhileInSettlement_DoesNotSyncAndDoesNotWarn()
    {
        // Inside the walls the engine owns placement, so there is no position to sync — and the
        // party is legitimately unparked, so the "NOT parked" anomaly warning must stay quiet.
        // Without this the whole settlement stop logs a fault every hour.
        Commander("town_ES1");
        Presence("town_ES1", parked: false);
        Verdict(AttachmentStatus.Attached);

        _sut.ReconcileHourly(Now);

        _attachment.DidNotReceive().SyncPosition(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("NOT parked")));
    }

    [TestMethod]
    public void ReconcileHourly_AttachedNotParkedOutsideSettlement_StillWarns()
    {
        // The exemption must not swallow the REAL anomaly: unparked with no settlement to
        // explain it is still a genuine fault, and losing that warning would hide exactly the
        // class of bug the diagnostic was added for.
        Commander();
        Presence(parked: false);
        Verdict(AttachmentStatus.Attached);

        _sut.ReconcileHourly(Now);

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("NOT parked")));
    }

    [TestMethod]
    public void ReconcileHourly_AttachedAndParkedOutsideSettlement_StillSyncs()
    {
        Commander();
        Presence(parked: true);
        Verdict(AttachmentStatus.Attached);

        _sut.ReconcileHourly(Now);

        _attachment.Received(1).SyncPosition("lord_1_1");
    }
}

/// <summary>
/// The 2026-08-08 in-game thrash: the player was teleported EW1 -> out -> EW2 -> out -> EW3,
/// seconds apart, because CommanderSnapshot mixed two sources of settlement truth.
/// </summary>
[TestClass]
public class CommanderSettlementIdentityTests
{
    /// <summary>
    /// `Hero.CurrentSettlement` resolves through `PartyBelongedTo`, so once a commander joins an
    /// ARMY it reports the ARMY's settlement while his own party is elsewhere. Reading the flag
    /// from the party and the id from the hero made Assess follow into one town and then
    /// immediately demand an exit from it. Both must come from the party.
    /// </summary>
    [TestMethod]
    public void GetSnapshot_ReadsEverySettlementFieldFromTheParty_NeverTheHero()
    {
        var src = System.IO.File.ReadAllText("../../../../Main/Adapters/CommanderLordAdapter.cs");
        var snapshotCall = src.Substring(src.IndexOf("return new CommanderSnapshot("));
        snapshotCall = snapshotCall.Substring(0, snapshotCall.IndexOf("        }"));

        foreach (var field in new[] { "partyIsInSettlement:", "settlementId:", "settlementName:", "settlementMenuId:" })
        {
            var line = snapshotCall.Split('\n').FirstOrDefault(l => l.Contains(field));
            Assert.IsNotNull(line, $"{field} vanished from GetSnapshot");
            StringAssert.Contains(line, "party", $"{field} must be derived from the commander's PARTY");
            Assert.IsFalse(line.Contains("hero.CurrentSettlement"),
                $"{field} reads hero.CurrentSettlement — that resolves through PartyBelongedTo and " +
                "reports the ARMY's settlement, which is what caused the follow/exit teleport thrash");
        }
    }

    /// <summary>
    /// Assess compares the two ids with ordinal equality, so a snapshot that disagrees with itself
    /// produces SettlementExitRequired for the settlement it just told us to enter.
    /// </summary>
    [TestMethod]
    public void Assess_FollowThenSameSettlement_SettlesInsteadOfOscillating()
    {
        var attachment = new ServiceAttachmentService(
            Substitute.For<IMobilePartyAttachmentAdapter>(),
            Substitute.For<IGameMenuAdapter>(),
            Substitute.For<IModLogger>());

        var commander = new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true,
            partyIsInSettlement: true, settlementId: "town_EW1");

        // 1) outside -> follow in
        var outside = new PlayerPresenceSnapshot(mainPartyExists: true);
        Assert.AreEqual(AttachmentStatus.SettlementFollowRequired,
            attachment.Assess(EnlistmentState.EnlistedAttached, commander, outside).Status);

        // 2) now inside the SAME settlement -> must settle, not demand an exit
        var inside = new PlayerPresenceSnapshot(
            mainPartyExists: true, isActive: true, isVisible: true, settlementId: "town_EW1");
        Assert.AreEqual(AttachmentStatus.Attached,
            attachment.Assess(EnlistmentState.EnlistedAttached, commander, inside).Status,
            "following into the commander's settlement must terminate — an Exit verdict here is the teleport loop");
    }
}
