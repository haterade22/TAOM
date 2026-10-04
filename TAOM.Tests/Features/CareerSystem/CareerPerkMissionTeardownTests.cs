using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// The per-hit <c>[CareerPerks]</c> summary (plan 030, DECISIONS D6) is the only record of every hit after a
/// combination's first, so the mission behavior writes it on every way the engine takes a mission out of play, and
/// only a crash may lose it. <c>OnEndMission</c> runs through <c>Mission.EndMission</c> (the battle's end, a retreat or
/// a surrender, the escape menu's "exit to main menu"). A mission state that <c>GameStateManager.CleanStates</c>
/// finalises (the application shutting down, if a window close reaches <c>CoreManaged.Finalize</c>, unverified; or a
/// mod that loads a save mid-mission) removes its behaviors without it, so
/// <c>OnRemoveBehavior</c> writes the summary too. The stat service here is the real one, so each test reads the
/// lines it writes; the behavior's other collaborators are fakes.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CareerPerkMissionTeardownTests
{
    private const string Subject = "lord1";
    private static readonly AttackTypeMask MeleeCut = AttackTypeMask.Melee | AttackTypeMask.Cut;

    private IModLogger _logger = null!;
    private ICareerPassiveService _passives = null!;
    private CareerAgentStatService _stats = null!;

    [TestInitialize]
    public void Setup()
    {
        CareerAbilityBuffTracker.ClearAll();
        _logger = Substitute.For<IModLogger>();
        _passives = Substitute.For<ICareerPassiveService>();
        _passives.GetPassiveMagnitude(Subject, PassiveEffectType.TroopDamage).Returns(0.10f);
        _stats = new CareerAgentStatService(_passives, _logger);
    }

    [TestCleanup]
    public void Cleanup() => CareerAbilityBuffTracker.ClearAll();

    private CareerPerkMissionBehavior NewBehavior(ICareerAgentStatService? stats = null) => new CareerPerkMissionBehavior(
        Substitute.For<ICareerDataService>(),
        Substitute.For<ICareerAbilityService>(),
        Substitute.For<IAbilityActivationController>(),
        Substitute.For<IAbilityEffectExecutor>(),
        stats ?? _stats,
        Substitute.For<ICareerConfigProvider>(),
        _logger);

    private void DealHits(int count)
    {
        for (int i = 0; i < count; i++)
            _stats.CalculateDamageAmplification(null, Subject, MeleeCut, 50f);
    }

    private void AssertOneSummaryOf(int hits)
    {
        _logger.Received(1).LogInfo($"[CareerPerks] hit summary for this mission: 1 combination(s), {hits} hit(s)");
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[CareerPerks] hit amp summary for 'lord1'") && s.Contains($"hits={hits} ")));
    }

    [TestMethod]
    public void OnEndMission_AfterHits_WritesTheHitSummary()
    {
        var sut = NewBehavior();
        DealHits(3);

        sut.OnEndMissionInternal();

        AssertOneSummaryOf(3);
    }

    // The abort paths: the state is finalised with no EndMission, so OnEndMission never runs.
    [TestMethod]
    public void OnRemoveBehavior_WithoutAnEndMission_WritesTheHitSummary()
    {
        var sut = NewBehavior();
        DealHits(3);

        sut.OnRemoveBehavior();

        AssertOneSummaryOf(3);
    }

    // The ordinary teardown reaches both: the second call finds the tally already written and cleared.
    [TestMethod]
    public void OnRemoveBehavior_AfterAnEndMission_WritesNoSecondSummary()
    {
        var sut = NewBehavior();
        DealHits(3);

        sut.OnEndMissionInternal();
        sut.OnRemoveBehavior();

        AssertOneSummaryOf(3);
    }

    [TestMethod]
    public void OnRemoveBehavior_AMissionWithNoHits_WritesNoSummary()
    {
        var sut = NewBehavior();

        sut.OnRemoveBehavior();

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.StartsWith("[CareerPerks]")));
    }

    // The stat service is a singleton: a tally an aborted mission never wrote would otherwise ride into the next
    // mission, which would then log no first hit for the combination and sum both missions' hits into one line.
    [TestMethod]
    public void OnRemoveBehavior_AnAbortedMission_DoesNotLeakItsTallyIntoTheNextMission()
    {
        DealHits(2);
        NewBehavior().OnRemoveBehavior();
        _logger.ClearReceivedCalls();

        var next = NewBehavior();
        DealHits(1);
        next.OnEndMissionInternal();

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.StartsWith("[CareerPerks] hit amp for 'lord1'")));
        AssertOneSummaryOf(1);
    }

    [TestMethod]
    public void OnRemoveBehavior_WhenTheSummaryThrows_IsContainedAndReported()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        stats.When(s => s.ResetDiagnostics()).Do(_ => throw new InvalidOperationException("boom"));
        var sut = NewBehavior(stats);

        sut.OnRemoveBehavior();   // a throw here would unwind the engine's loop over every behavior's removal

        _logger.Received(1).LogWarning("CareerSystem: OnRemoveBehavior ResetDiagnostics() threw: boom");
    }

    [TestMethod]
    public void OnEndMission_WhenTheSummaryThrows_IsContainedAndReported()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        stats.When(s => s.ResetDiagnostics()).Do(_ => throw new InvalidOperationException("boom"));
        var sut = NewBehavior(stats);

        sut.OnEndMissionInternal();

        _logger.Received(1).LogWarning("CareerSystem: OnEndMission ResetDiagnostics() threw: boom");
    }
}
