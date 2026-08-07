using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The safety net for the [EnlistDiag] volume toggle. Before this file, grepping "EnlistDiag"
/// across TAOM.Tests returned zero hits — the diagnostics had no test coverage at all, so gating
/// them was a change with nothing to catch a mis-shaped gate.
///
/// The hazard the gate creates is specific: in <see cref="EnlistmentReconciler.ReconcileAttached"/>
/// the logging sits INTERLEAVED with the encounter self-heal, the re-park and the position sync. A
/// naive `if (!enabled) return;` at the top of that method would silently disable the enlistment
/// self-heal for every player who leaves the toggle at its default OFF. Group B1 exists to make
/// that mistake impossible to ship: each of those tests runs with the toggle OFF and asserts a
/// MUTATION still happens.
/// </summary>
[TestClass]
public class EnlistmentDiagnosticsGateTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private ICommanderLordAdapter _commander = null!;
    private IMobilePartyAttachmentAdapter _partyAdapter = null!;
    private ServiceAttachmentService _attachment = null!;
    private DischargeService _discharge = null!;
    private IEncounterAdapter _encounter = null!;
    private IEnlistmentDiagnosticsSettingsProvider _diag = null!;
    private EnlistmentReconciler _reconciler = null!;

    private const double Now = 200.0;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _partyAdapter = Substitute.For<IMobilePartyAttachmentAdapter>();
        _partyAdapter.RestorePresence().Returns(true);
        _partyAdapter.ParkNear(Arg.Any<string>()).Returns(true);
        _partyAdapter.SyncPositionTo(Arg.Any<string>()).Returns(true);
        _attachment = new ServiceAttachmentService(_partyAdapter, _logger);
        _discharge = new DischargeService(_store, _machine, _partyAdapter,
            Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(),
            Substitute.For<ICommanderLordAdapter>(), Substitute.For<IGameMenuAdapter>(), _logger);
        _encounter = Substitute.For<IEncounterAdapter>();

        // An NSubstitute bool defaults to false, which IS the shipping default — every test in
        // this class therefore runs "toggle off" unless it opts in explicitly.
        _diag = Substitute.For<IEnlistmentDiagnosticsSettingsProvider>();

        _reconciler = new EnlistmentReconciler(_store, _machine, _attachment, _commander, _discharge,
            new EnlistmentConfigProvider(_logger), _encounter, new EncounterOwnershipPolicy(), _diag, _logger);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.EnlistedAtDay = 100.0;
        _store.Record.ContractEndDay = 465.0;
    }

    private void CommanderHealthy(bool inMapEvent = false)
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true,
            partyIsInMapEvent: inMapEvent, name: "Lord Test"));
    }

    private void CommanderDead()
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: false, name: "Lord Test"));
    }

    private void PlayerPresence(bool parked = true, bool inMapEvent = false, bool hasEncounter = false)
    {
        _partyAdapter.GetPresence().Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isCaptive: false,
            isActive: !parked, isVisible: !parked, isInMapEvent: inMapEvent,
            hasPlayerEncounter: hasEncounter));
    }

    /// <summary>A live PlayerEncounter that the ownership policy rules IS ours to close.</summary>
    private void StrandedCommanderEncounter()
    {
        _encounter.GetOwnership("lord_party_1").Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true,
            conversationInProgress: false,
            hasEncounteredMobileParty: true,
            encounteredPartyId: "lord_party_1",
            encounteredPartyIsCommanderRelated: true,
            playerInMapEvent: false));
    }

    // ---------------------------------------------------------------------------------------
    // B1 — with the toggle OFF, every state mutation in ReconcileAttached still happens.
    // These are the tests that a `return`-shaped gate must not survive.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void ReconcileHourly_DiagOff_StrandedEncounter_StillFinishesIt()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true, hasEncounter: true);
        StrandedCommanderEncounter();
        _encounter.Finish(true).Returns(true);

        _reconciler.ReconcileHourly(Now);

        _encounter.Received(1).Finish(true);
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_AttachRequired_StillParks()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_Attached_StillSyncsPosition()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true);

        _reconciler.ReconcileHourly(Now);

        _partyAdapter.Received(1).SyncPositionTo("lord_1_1");
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_BattleStateNoMapEvent_StillDemotesToAttached()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy();
        PlayerPresence(parked: true);
        _encounter.HasCurrent.Returns(false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_CommanderDead_StillDischarges()
    {
        MakeEnlisted();
        CommanderDead();
        PlayerPresence(parked: true);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_BattleJoinRequired_StillRaisesEvent()
    {
        MakeEnlisted();
        CommanderHealthy(inMapEvent: true);
        PlayerPresence(parked: true, inMapEvent: false);
        string requested = null;
        _reconciler.BattleJoinRequested += id => requested = id;

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual("lord_1_1", requested);
    }

    // ---------------------------------------------------------------------------------------
    // B2 — the always-on fault lines are NOT gated. Each overrides a Setup() stub that would
    // otherwise report success and leave the assertion pinning nothing.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void ReconcileHourly_DiagOff_StrandedEncounterCloseFails_StillLogsError()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true, hasEncounter: true);
        StrandedCommanderEncounter();
        _encounter.Finish(true).Returns(false); // override: the close FAILS

        _reconciler.ReconcileHourly(Now);

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("failed to close the stranded PlayerEncounter")));
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_SyncFails_StillLogsError()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true);
        _partyAdapter.SyncPositionTo(Arg.Any<string>()).Returns(false); // override Setup()'s true

        _reconciler.ReconcileHourly(Now);

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("hourly SYNC failed")));
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_ParkFails_StillLogsError()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: false);
        _partyAdapter.ParkNear(Arg.Any<string>()).Returns(false); // override Setup()'s true

        _reconciler.ReconcileHourly(Now);

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("hourly PARK failed")));
    }

    [TestMethod]
    public void ReconcileHourly_DiagOff_AttachedButNotParked_StillLogsWarning()
    {
        // Assess returns Attached when BOTH sides are in a map event, but the party is plainly
        // not parked — the silent-drift case the warning exists to surface.
        MakeEnlisted();
        CommanderHealthy(inMapEvent: true);
        PlayerPresence(parked: false, inMapEvent: true);

        _reconciler.ReconcileHourly(Now);

        _logger.Received().LogWarning(Arg.Is<string>(s =>
            s.Contains("verdict=Attached but the party is NOT parked")));
    }

    // ---------------------------------------------------------------------------------------
    // B3 — the gated line. Paired negative/positive over an IDENTICAL arrange: a lone negative
    // would pass even if the line were deleted outright
    // (docs/reviews/lessons/testing-qa.md, "A negative assertion is vacuous...").
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void ReconcileHourly_DiagOff_TickLineNotEmitted()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true);

        _reconciler.ReconcileHourly(Now);

        _logger.DidNotReceive().LogDebug(Arg.Is<string>(s => s.Contains("[EnlistDiag] TICK")));
    }

    [TestMethod]
    public void ReconcileHourly_DiagOn_TickLineEmitted()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true);
        _diag.IsEnabled.Returns(true); // the ONLY difference from the test above

        _reconciler.ReconcileHourly(Now);

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("[EnlistDiag] TICK")));
    }

    // ---------------------------------------------------------------------------------------
    // Source-scan guard. MobilePartyAttachmentAdapter and EnlistmentBattleBehavior are not
    // unit-testable (static MobileParty.MainParty, sealed MapEvent), so the gate SHAPE in those
    // files is pinned by reading them as text. Precedent: SiegePropDiagnosticsWiringTests reads
    // Main/IoC.cs the same way.
    // ---------------------------------------------------------------------------------------

    private static readonly string[] GatedFiles =
    {
        Path.Combine("Main", "Adapters", "MobilePartyAttachmentAdapter.cs"),
        Path.Combine("Main", "Features", "Enlistment", "EnlistmentReconciler.cs"),
        Path.Combine("Main", "Features", "Enlistment", "Hooks", "EnlistmentBattleBehavior.cs"),
    };

    [TestMethod]
    public void GateShape_NoEnlistmentFileUsesANegatedEarlyOut()
    {
        var negatedGate = new Regex(@"if\s*\(\s*!.*IsEnabled");
        var filesScanned = 0;

        foreach (var relative in GatedFiles)
        {
            var lines = ReadProjectSourceLines(relative);
            if (lines == null)
                Assert.Fail($"{relative} not found — the scan must never pass by not finding its inputs.");
            filesScanned++;

            for (var i = 0; i < lines.Length; i++)
            {
                StringAssert.DoesNotMatch(lines[i], new Regex(Regex.Escape("IsEnabled) return")),
                    $"{relative}:{i + 1} gates a `return` on the diagnostics toggle. The toggle must " +
                    "gate LOGGING ONLY — a return-shaped gate disables the enlistment self-heal for " +
                    "every player on the default-OFF setting.");

                StringAssert.DoesNotMatch(lines[i], negatedGate,
                    $"{relative}:{i + 1} uses a negated early-out on the diagnostics toggle. Write " +
                    "`if (_diag?.IsEnabled == true) <one logging statement>;` instead.");
            }
        }

        // Floor assertion: a path typo must fail loudly, not silently scan nothing.
        Assert.AreEqual(3, filesScanned, "all three gated files must be scanned");
    }

    [TestMethod]
    public void BattleBehavior_CountInvolved_AppearsOnlyInsideTheGatedStatement()
    {
        var lines = ReadProjectSourceLines(GatedFiles[2]);
        Assert.IsNotNull(lines, "EnlistmentBattleBehavior.cs not found");

        var gateIndex = System.Array.FindIndex(lines, l => l.Contains("if (_diagSettings?.IsEnabled"));
        var callIndex = System.Array.FindIndex(lines, l => l.Contains("CountInvolved(mapEvent)"));

        Assert.IsTrue(gateIndex >= 0, "the map-event gate is missing entirely");
        Assert.IsTrue(callIndex >= 0, "CountInvolved(mapEvent) call site not found");
        Assert.IsTrue(callIndex > gateIndex,
            $"CountInvolved(mapEvent) is at line {callIndex + 1} but the gate is at line {gateIndex + 1}. " +
            "The full InvolvedParties walk must sit inside the gated statement's argument list, or " +
            "turning the toggle off saves the write but not the enumeration.");
    }

    private static string[] ReadProjectSourceLines(string relativePath)
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllLines(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
