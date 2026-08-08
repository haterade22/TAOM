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
/// self-heal for anyone who turns the toggle off. Group B1 exists to make that mistake impossible
/// to ship: each of those tests runs with the toggle OFF and asserts a MUTATION still happens.
///
/// NOTE ON THE DEFAULT: the toggle ships ON (`TaomSettings.EnableEnlistmentDiagnostics = true`, and
/// the provider resolves a missing MCM setting with `?? true`) because the enlistment service loop
/// is under active diagnosis. These tests deliberately run the OFF path anyway — that is the path
/// where a mis-shaped gate does damage, and it is the path every player lands on once the default
/// flips. Do not "correct" the substitute to true.
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
        _attachment = new ServiceAttachmentService(_partyAdapter, Substitute.For<IGameMenuAdapter>(), _logger);
        _discharge = new DischargeService(_store, _machine, _partyAdapter,
            Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(),
            Substitute.For<ICommanderLordAdapter>(), Substitute.For<IGameMenuAdapter>(), _logger);
        _encounter = Substitute.For<IEncounterAdapter>();

        // An NSubstitute bool defaults to false, so every test in this class runs the "toggle off"
        // path unless it opts in explicitly. That is NOT the shipping default (which is ON — see the
        // class doc); it is the path chosen deliberately, because OFF is where a mis-shaped gate
        // silently disables the self-heal.
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

        // Asserted across BOTH levels: the gate must suppress the line outright, not merely
        // demote it. A future "just make it DEBUG again" edit has to fail here.
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("[EnlistDiag] TICK")));
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

        // INFO, not DEBUG: FileLogger flushes INFO/WARN/ERROR synchronously and leaves DEBUG in an
        // async queue that a hard native CTD discards. A trace the player deliberately switched on
        // to catch a crash must survive that crash.
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("[EnlistDiag] TICK")));
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

    /// <summary>
    /// Collects every identifier that carries the toggle's value in a file: the property itself, plus
    /// any local it is aliased into. MobilePartyAttachmentAdapter reads the toggle ONCE per method
    /// (`var diag = _diag?.IsEnabled == true;`) so the value cannot change mid-method, and then gates
    /// on the bare local (`if (diag)`). A regression written in that file's own idiom —
    /// `if (!diag) return;` — contains no `IsEnabled` token at all, so a scan that only looks for
    /// `IsEnabled` is blind to it in the one file the class doc says cannot be unit-tested.
    /// </summary>
    private static string[] CollectToggleTokens(string[] lines)
    {
        var tokens = new System.Collections.Generic.List<string> { "IsEnabled" };
        var alias = new Regex(@"\bvar\s+(\w+)\s*=\s*[^;]*\bIsEnabled\b");
        foreach (var line in lines)
        {
            var m = alias.Match(line);
            if (m.Success)
                tokens.Add(m.Groups[1].Value);
        }
        return tokens.ToArray();
    }

    /// <summary>
    /// True when the line short-circuits control flow on a toggle token — the shape that would
    /// disable the enlistment self-heal rather than only the logging.
    /// </summary>
    private static bool IsControlFlowGate(string line, string[] toggleTokens)
    {
        foreach (var t in toggleTokens)
        {
            var e = Regex.Escape(t);
            // `if (!diag)` / `if ( ! _diag.IsEnabled )` — a negated test on the toggle.
            if (Regex.IsMatch(line, $@"if\s*\(\s*!\s*{e}\b")) return true;
            // `... IsEnabled) return;` / `if (diag) return;` — a toggle guarding a return.
            if (Regex.IsMatch(line, $@"\b{e}\b[^;]*\)\s*return\b")) return true;
        }
        return false;
    }

    [TestMethod]
    public void GateShape_Detector_FlagsKnownBadShapes_AndSparesGoodOnes()
    {
        // POSITIVE CONTROL. Without this, GateShape_NoEnlistmentFileUsesANegatedEarlyOut passes
        // whenever the detector is broken — an audit that reports "zero found" is worthless until
        // something proves it can find one.
        var tokens = CollectToggleTokens(new[] { "        var diag = _diag?.IsEnabled == true;" });
        CollectionAssert.Contains(tokens, "diag", "the alias `var diag = _diag?.IsEnabled == true;` must be tracked");
        CollectionAssert.Contains(tokens, "IsEnabled");

        // Known-bad — every one of these must be caught.
        Assert.IsTrue(IsControlFlowGate("            if (!diag) return;", tokens), "aliased negated early-out");
        Assert.IsTrue(IsControlFlowGate("            if (!_diag.IsEnabled) return;", tokens), "direct negated early-out");
        Assert.IsTrue(IsControlFlowGate("            if ( ! diag ) return false;", tokens), "spacing variant");
        Assert.IsTrue(IsControlFlowGate("            if (_diag?.IsEnabled != true) return;", tokens), "IsEnabled guarding a return");

        // Known-good — the sanctioned shape must NOT be flagged, or the guard is unusable.
        Assert.IsFalse(IsControlFlowGate("            if (diag)", tokens), "the sanctioned aliased gate");
        Assert.IsFalse(IsControlFlowGate("            if (_diag?.IsEnabled == true)", tokens), "the sanctioned direct gate");
        Assert.IsFalse(IsControlFlowGate("            var diag = _diag?.IsEnabled == true;", tokens), "the alias assignment itself");
    }

    [TestMethod]
    public void GateShape_NoEnlistmentFileUsesANegatedEarlyOut()
    {
        var filesScanned = 0;

        foreach (var relative in GatedFiles)
        {
            var lines = ReadProjectSourceLines(relative);
            if (lines == null)
                Assert.Fail($"{relative} not found — the scan must never pass by not finding its inputs.");
            filesScanned++;

            var tokens = CollectToggleTokens(lines);

            for (var i = 0; i < lines.Length; i++)
            {
                Assert.IsFalse(IsControlFlowGate(lines[i], tokens),
                    $"{relative}:{i + 1} short-circuits control flow on the diagnostics toggle:\n" +
                    $"    {lines[i].Trim()}\n" +
                    "The toggle must gate LOGGING ONLY. A return-shaped gate disables the enlistment " +
                    "self-heal (the encounter close, the re-park, the position sync) for anyone who " +
                    "turns diagnostics off. Write `if (_diag?.IsEnabled == true) <one logging statement>;`.");
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

        // Containment, not ordering. `callIndex > gateIndex` is satisfied by ANY line below the gate,
        // including one after the gated statement has closed — which is exactly the regression this
        // test exists to catch (the walk would then run unconditionally). Walk the parenthesis balance
        // from the gate's own statement to find where it actually ends, and require the call inside it.
        var depth = 0;
        var started = false;
        var statementEnd = -1;
        for (var i = gateIndex; i < lines.Length; i++)
        {
            foreach (var c in lines[i])
            {
                if (c == '(') { depth++; started = true; }
                else if (c == ')') depth--;
            }
            // The gate line closes its own condition; the guarded statement follows and closes at
            // the first point where every parenthesis opened since the gate is balanced AND the
            // line terminates a statement.
            if (started && depth <= 0 && i > gateIndex && lines[i].TrimEnd().EndsWith(";"))
            {
                statementEnd = i;
                break;
            }
        }

        Assert.IsTrue(statementEnd > gateIndex,
            $"could not find the end of the gated statement starting at line {gateIndex + 1}");
        Assert.IsTrue(callIndex > gateIndex && callIndex <= statementEnd,
            $"CountInvolved(mapEvent) is at line {callIndex + 1}, outside the gated statement " +
            $"(lines {gateIndex + 1}-{statementEnd + 1}). The full InvolvedParties walk must sit inside " +
            "the gated statement's argument list — C# does not evaluate the arguments of a statement " +
            "that does not execute. Outside it, turning the toggle off saves the disk write but still " +
            "pays the enumeration on every map event in the world.");
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
