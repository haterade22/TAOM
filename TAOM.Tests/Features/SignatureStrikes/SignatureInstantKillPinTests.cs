using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// Source pins for the instant-kill executor (#1359). The engine side cannot run here, so these pin the
/// rules its comments state: the roll is drawn on the main tick and never in the hit callback, the victim
/// is re-gated for the frame that has passed before the blow is written, and the runner asks for the kill
/// after its ring loop.
/// </summary>
[TestClass]
public class SignatureInstantKillPinTests
{
    private const string Executor = "Main/Features/SignatureStrikes/Hooks/SignatureInstantKill.cs";
    private const string Runner = "Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs";
    private const string Logic = "Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs";

    private static int At(string text, string part)
    {
        var at = text.IndexOf(part, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, part + " not found");
        return at;
    }

    [TestMethod]
    public void TheVictimRegates_ComeBeforeTheBlow()
    {
        var source = RepoPaths.ReadSource(Executor, stripComments: true);
        var blow = At(source, "CustomAttacksUtils.TakeDamage(");

        foreach (var gate in new[] { "victim.IsActive()", "victim.IsFadingOut()", "AgentSlotIdentity.IsCurrentOccupant(victim)", "victim.Health > 0f" })
            Assert.IsTrue(At(source, gate) < blow, gate + " must come before the blow");
    }

    [TestMethod]
    public void TheRoll_IsDrawnBeforeTheDecision_AndOnlyHere()
    {
        var source = RepoPaths.ReadSource(Executor, stripComments: true);

        Assert.IsTrue(At(source, "MBRandom.RandomFloat") < At(source, "_service.DecideInstantKill("));
        Assert.AreEqual(source.IndexOf("MBRandom", StringComparison.Ordinal), source.LastIndexOf("MBRandom", StringComparison.Ordinal),
            "one roll per call");
    }

    [TestMethod]
    public void TheBlow_IsTheServicesDamageOnTheDefaultDamageType()
    {
        var source = RepoPaths.ReadSource(Executor, stripComments: true);

        StringAssert.Contains(source, "CustomAttacksUtils.TakeDamage(victim, attacker, damage, magnitude, knockDown: false);");
        StringAssert.Contains(source, "var damage = _service.InstantKillDamage(health);");
    }

    [TestMethod]
    public void TheVictimFacts_AreTheOnesTheDecisionIsDefinedOn()
    {
        var source = RepoPaths.ReadSource(Executor, stripComments: true);

        StringAssert.Contains(source, "victim.IsMainAgent || victim.IsPlayerControlled || !victim.IsAIControlled");
        StringAssert.Contains(source, "ReferenceEquals(victim, victim.Team?.GeneralAgent)");
        StringAssert.Contains(source, "_roster.TryGet(victim, out _)");
        StringAssert.Contains(source, "_registry.IsKillImmuneRace(race)");
        StringAssert.Contains(source, "victim.IsHuman, victim.IsHero");
    }

    [TestMethod]
    public void TheShrugOffFact_ComesFromTheRaceAbilityHooks_NotTheBlowFlags()
    {
        var source = RepoPaths.ReadSource(Executor, stripComments: true);

        StringAssert.Contains(source, "_registry.IsKillImmuneRace(race), RaceAbilityHooks.IsShruggingOff(victim), health))");
    }

    [TestMethod]
    public void TheRunner_AsksForTheKillAfterTheRingLoop_AndBeforeTheLogLine()
    {
        var source = RepoPaths.ReadSource(Runner, stripComments: true);
        var call = At(source, "_instantKill.TryKill(");

        Assert.IsTrue(call > At(source, "foreach (var victim in _nearbyBuffer)"));
        Assert.IsTrue(call > At(source, "hit++;"));
        Assert.IsTrue(call < At(source, "_logger.LogInfo("));
    }

    [TestMethod]
    public void TheRunner_KeepsAKillOnlyRow_PastItsNothingToApplyReturn()
    {
        var source = RepoPaths.ReadSource(Runner, stripComments: true);

        StringAssert.Contains(source, "&& !(effect.KillChance > 0f)");
    }

    [TestMethod]
    public void TheHitCallback_NeverRolls()
    {
        var logic = RepoPaths.ReadSource(Logic, stripComments: true);
        var start = At(logic, "public override void OnMeleeHit(");
        var end = logic.IndexOf("public override", start + 10, StringComparison.Ordinal);
        var hit = end > start ? logic.Substring(start, end - start) : logic.Substring(start);

        Assert.IsFalse(hit.Contains("MBRandom"), "OnMeleeHit only enqueues");
        Assert.IsFalse(hit.Contains("TryKill"), "OnMeleeHit only enqueues");
        Assert.IsFalse(RepoPaths.ReadSource(Runner, stripComments: true).Contains("MBRandom"), "the runner never rolls itself");
    }
}
