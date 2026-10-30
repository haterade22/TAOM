using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #769 E10: the three counters a player's career gives the leader's non-hero troops against the enemy's
/// fear, knockdown and crush-through. The math is on <see cref="ICareerAgentStatService"/> so it is pure and
/// testable; each reads the class-filtered magnitude with no hit mask.
/// </summary>
[TestClass]
public class CareerTroopCounterTests
{
    private const string Lord = "lord1";

    private ICareerPassiveService _passives = null!;
    private CareerAgentStatService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _passives = Substitute.For<ICareerPassiveService>();
        _sut = new CareerAgentStatService(_passives, Substitute.For<IModLogger>());
    }

    private void Given(PassiveEffectType type, TroopClassMask troopClass, float magnitude)
        => _passives.GetTroopMagnitude(Lord, type, troopClass, Arg.Any<AttackTypeMask?>()).Returns(magnitude);

    // ── ScaleTroopFear ──

    [TestMethod]
    public void ScaleTroopFear_Resistance_ShrinksTheDrainByThatFraction()
    {
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0.25f);

        Assert.AreEqual(7.5f, _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f), 0.0001f);
    }

    [TestMethod]
    public void ScaleTroopFear_FullResistance_DrainsNothing()
    {
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 1f);

        Assert.AreEqual(0f, _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f));
    }

    [TestMethod]
    public void ScaleTroopFear_MoreThanFullResistance_NeverTurnsTheDrainIntoAGain()
    {
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 1.7f);

        Assert.AreEqual(0f, _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f));
    }

    [TestMethod]
    public void ScaleTroopFear_NoLeader_ReturnsTheDrainAndNeverAsks()
    {
        Assert.AreEqual(10f, _sut.ScaleTroopFear(null, TroopClassMask.Infantry, 10f));
        Assert.AreEqual(10f, _sut.ScaleTroopFear("", TroopClassMask.Infantry, 10f));
        _passives.DidNotReceiveWithAnyArgs().GetTroopMagnitude(default!, default, default, default);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-0.3f)]
    [DataRow(float.NaN)]
    public void ScaleTroopFear_MagnitudeNotPositive_ReturnsTheDrain(float magnitude)
    {
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, magnitude);

        Assert.AreEqual(10f, _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void ScaleTroopFear_DrainNotFinite_ReturnsItUntouched(float drain)
    {
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0.5f);

        var result = _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, drain);

        Assert.IsTrue(float.IsNaN(drain) ? float.IsNaN(result) : result == drain);
    }

    [TestMethod]
    public void ScaleTroopFear_AskedPerTroopClass_AClassTheLeaderDoesNotShieldTakesTheFullDrain()
    {
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0.5f);

        Assert.AreEqual(5f, _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f), 0.0001f);
        Assert.AreEqual(10f, _sut.ScaleTroopFear(Lord, TroopClassMask.Cavalry, 10f));
    }

    [TestMethod]
    public void ScaleTroopFear_AsksTheFearTypeOnly()
    {
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Infantry, 0.9f);
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 0.9f);

        Assert.AreEqual(10f, _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f));
    }

    // ── ScaleTroopKnockdownResistance ──

    [TestMethod]
    public void ScaleTroopKnockdownResistance_Magnitude_RaisesTheResistance()
    {
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Cavalry, 0.5f);

        Assert.AreEqual(1.5f, _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Cavalry, 1f), 0.0001f);
    }

    [TestMethod]
    public void ScaleTroopKnockdownResistance_NonHumanMaxValueInput_StaysFinite()
    {
        // Vanilla returns float.MaxValue for a non-human; times (1 + m) overflows to infinity, which the
        // engine would multiply into HealthLimit. The input comes back instead.
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Infantry, 0.5f);

        var result = _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Infantry, float.MaxValue);

        Assert.AreEqual(float.MaxValue, result);
    }

    [DataTestMethod]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NaN)]
    public void ScaleTroopKnockdownResistance_NonFiniteInput_ComesBackAsIs(float resistance)
    {
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Infantry, 0.5f);

        var result = _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Infantry, resistance);

        Assert.IsTrue(float.IsNaN(resistance) ? float.IsNaN(result) : result == resistance);
    }

    [TestMethod]
    public void ScaleTroopKnockdownResistance_NonFiniteMagnitude_ReturnsTheInput()
    {
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Infantry, float.PositiveInfinity);

        Assert.AreEqual(2f, _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Infantry, 2f));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-0.4f)]
    [DataRow(float.NaN)]
    public void ScaleTroopKnockdownResistance_MagnitudeNotPositive_ReturnsTheInput(float magnitude)
    {
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Infantry, magnitude);

        Assert.AreEqual(2f, _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Infantry, 2f));
    }

    [TestMethod]
    public void ScaleTroopKnockdownResistance_NoLeaderOrOtherClass_ReturnsTheInput()
    {
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Infantry, 0.5f);

        Assert.AreEqual(2f, _sut.ScaleTroopKnockdownResistance(null, TroopClassMask.Infantry, 2f));
        Assert.AreEqual(2f, _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Ranged, 2f));
    }

    // ── HoldsAgainstCrush ──

    [TestMethod]
    public void HoldsAgainstCrush_NoPerk_FalseAndTheRollIsNeverDrawn()
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 0f);
        var draws = 0;

        var holds = _sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => { draws++; return 0f; });

        Assert.IsFalse(holds);
        Assert.AreEqual(0, draws, "a soldier without the perk must not advance the random stream");
    }

    [DataTestMethod]
    [DataRow(-0.2f)]
    [DataRow(float.NaN)]
    public void HoldsAgainstCrush_MagnitudeNotPositive_FalseWithoutARoll(float magnitude)
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, magnitude);
        var draws = 0;

        Assert.IsFalse(_sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => { draws++; return 0f; }));
        Assert.AreEqual(0, draws);
    }

    [TestMethod]
    public void HoldsAgainstCrush_NoLeader_FalseWithoutARollOrALookup()
    {
        var draws = 0;

        Assert.IsFalse(_sut.HoldsAgainstCrush(null, TroopClassMask.Infantry, () => { draws++; return 0f; }));
        Assert.AreEqual(0, draws);
        _passives.DidNotReceiveWithAnyArgs().GetTroopMagnitude(default!, default, default, default);
    }

    [TestMethod]
    public void HoldsAgainstCrush_RollBelowTheChance_Holds()
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 0.3f);

        Assert.IsTrue(_sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => 0.29f));
    }

    [TestMethod]
    public void HoldsAgainstCrush_RollAtTheChance_DoesNotHold()
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 0.3f);

        Assert.IsFalse(_sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => 0.3f));
    }

    [TestMethod]
    public void HoldsAgainstCrush_RollAboveTheChance_DoesNotHold()
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 0.3f);

        Assert.IsFalse(_sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => 0.9f));
    }

    [TestMethod]
    public void HoldsAgainstCrush_MagnitudeOverOne_ClampsToACertainHold()
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 3f);

        Assert.IsTrue(_sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => 0.999f));
    }

    [TestMethod]
    public void HoldsAgainstCrush_DrawsExactlyOnceWhenThePerkIsHeld()
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 0.5f);
        var draws = 0;

        _sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => { draws++; return 0.1f; });

        Assert.AreEqual(1, draws);
    }

    [TestMethod]
    public void HoldsAgainstCrush_OtherClass_FalseWithoutARoll()
    {
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Infantry, 1f);
        var draws = 0;

        Assert.IsFalse(_sut.HoldsAgainstCrush(Lord, TroopClassMask.Cavalry, () => { draws++; return 0f; }));
        Assert.AreEqual(0, draws);
    }

    // ── evidence: what the counters did, in the mission-end summary (#769 C4-01) ──

    private IModLogger _log = null!;

    private void WithLogger()
    {
        _log = Substitute.For<IModLogger>();
        _sut = new CareerAgentStatService(_passives, _log);
    }

    [TestMethod]
    public void ResetDiagnostics_AfterCounterFirings_WritesOneSummaryLinePerLeaderAndClass()
    {
        WithLogger();
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0.25f);
        Given(PassiveEffectType.TroopKnockdownResistance, TroopClassMask.Infantry, 0.5f);
        Given(PassiveEffectType.TroopCrushHold, TroopClassMask.Cavalry, 0.5f);

        _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f);
        _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f);
        _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Infantry, 1f);
        _sut.HoldsAgainstCrush(Lord, TroopClassMask.Cavalry, () => 0.1f);   // held
        _sut.HoldsAgainstCrush(Lord, TroopClassMask.Cavalry, () => 0.9f);   // rolled, not held: not counted
        _sut.ScaleTroopFear(Lord, TroopClassMask.Cavalry, 10f);             // no pip for cavalry: not counted

        _sut.ResetDiagnostics();

        Received.InOrder(() =>
        {
            _log.LogInfo("[CareerPerks] troop counter summary for this mission: 2 combination(s)");
            _log.LogInfo("[CareerPerks] troop counter summary for 'lord1' [Infantry]: fear scaled=2 knockdown scaled=1 crush held=0");
            _log.LogInfo("[CareerPerks] troop counter summary for 'lord1' [Cavalry]: fear scaled=0 knockdown scaled=0 crush held=1");
        });
        _log.Received(3).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void ResetDiagnostics_TwoLeadersOneClass_AreSummarisedApart()
    {
        WithLogger();
        _passives.GetTroopMagnitude("lord2", PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, Arg.Any<AttackTypeMask?>()).Returns(0.5f);
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0.5f);

        _sut.ScaleTroopFear("lord2", TroopClassMask.Infantry, 10f);
        _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f);
        _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f);
        _sut.ResetDiagnostics();

        _log.Received(1).LogInfo("[CareerPerks] troop counter summary for 'lord1' [Infantry]: fear scaled=2 knockdown scaled=0 crush held=0");
        _log.Received(1).LogInfo("[CareerPerks] troop counter summary for 'lord2' [Infantry]: fear scaled=1 knockdown scaled=0 crush held=0");
    }

    [TestMethod]
    public void ResetDiagnostics_NoCounterFired_WritesNoCounterSummary()
    {
        WithLogger();
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0f);

        _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f);
        _sut.ScaleTroopKnockdownResistance(Lord, TroopClassMask.Infantry, 1f);
        _sut.HoldsAgainstCrush(Lord, TroopClassMask.Infantry, () => 0f);
        _sut.ResetDiagnostics();

        _log.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("troop counter")));
    }

    [TestMethod]
    public void ResetDiagnostics_ClearsTheCounterTally_SoTheNextBattleStartsFromZero()
    {
        WithLogger();
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0.5f);
        _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f);
        _sut.ResetDiagnostics();
        _log.ClearReceivedCalls();

        _sut.ResetDiagnostics();

        _log.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void TheCounters_WithoutALogger_StillScaleAndNeverThrowOnReset()
    {
        _sut = new CareerAgentStatService(_passives);
        Given(PassiveEffectType.TroopFearResistance, TroopClassMask.Infantry, 0.5f);

        Assert.AreEqual(5f, _sut.ScaleTroopFear(Lord, TroopClassMask.Infantry, 10f), 0.0001f);
        _sut.ResetDiagnostics();
    }

    [TestMethod]
    public void ReportHookFault_LogsTheFirstFaultOfAHookOnceAndNamesTheHook()
    {
        WithLogger();
        var first = new InvalidOperationException("first");

        _sut.ReportHookFault("ScaleFearDrain", first);
        _sut.ReportHookFault("ScaleFearDrain", new InvalidOperationException("second"));

        _log.Received(1).LogError(Arg.Is<string>(s => s.Contains("ScaleFearDrain") && s.Contains("first") && s.Contains("[CareerPerks]")));
        _log.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ReportHookFault_AnotherHook_LogsItsOwnFirstFault()
    {
        WithLogger();

        _sut.ReportHookFault("ScaleFearDrain", new InvalidOperationException("a"));
        _sut.ReportHookFault("ApplyCrushHold", new InvalidOperationException("b"));

        _log.Received(2).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ReportHookFault_WithoutALoggerOrAnException_NeverThrows()
    {
        _sut = new CareerAgentStatService(_passives);

        _sut.ReportHookFault("ScaleFearDrain", new InvalidOperationException("x"));
        WithLogger();
        _sut.ReportHookFault("KnockDownResistance", null!);
    }

    // ── the types ──

    [TestMethod]
    public void TheThreeCounters_AreConsumedAndClassFiltered()
    {
        // The set itself is pinned once, without the game, in CareerConfigProviderTests.ClassFiltered_IsTheSixTroopTypesAndNoHeroType.
        foreach (var type in new[] { PassiveEffectType.TroopFearResistance, PassiveEffectType.TroopKnockdownResistance, PassiveEffectType.TroopCrushHold })
        {
            Assert.IsTrue(PassiveEffectConsumers.IsConsumed(type), $"{type} consumed");
            Assert.IsTrue(PassiveEffectConsumers.IsClassFiltered(type), $"{type} class filtered");
            Assert.IsFalse(PassiveEffectConsumers.IsMaskGated(type), $"{type} reads no hit");
        }
    }

    [TestMethod]
    public void EveryClassFilteredType_IsAlsoConsumed()
    {
        // A class-filtered type nothing reads would let troop_class pass validation on a phantom.
        foreach (var type in PassiveEffectConsumers.ClassFiltered)
            Assert.IsTrue(PassiveEffectConsumers.IsConsumed(type), type.ToString());
    }

    [TestMethod]
    public void TheThreeCounters_AreAppendedAfterTheExistingValues()
    {
        // The enum is parsed by name from XML, but an ordinal shift would still move any numeric reader.
        Assert.IsTrue((int)PassiveEffectType.TroopFearResistance > (int)PassiveEffectType.SpecialResourceUpgradeCostModifier);
        Assert.IsTrue((int)PassiveEffectType.TroopKnockdownResistance > (int)PassiveEffectType.TroopFearResistance);
        Assert.IsTrue((int)PassiveEffectType.TroopCrushHold > (int)PassiveEffectType.TroopKnockdownResistance);
    }
}
