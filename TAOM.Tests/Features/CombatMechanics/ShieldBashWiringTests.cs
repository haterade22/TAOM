using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// Hammerhand (#895) source pins that run without the game: the facade's one cost gate precedes every skill read, the facade and
/// builder never read the ShrugOff blow flag, the builder reads the three deliberate immunities, both damage models ask Hammerhand
/// after the charge rules and before the engine, and the service stays free of TaleWorlds types.
/// </summary>
[TestClass]
public class ShieldBashWiringTests
{
    private const string HooksSource = "Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs";
    private const string ServiceSource = "Main/Features/CombatMechanics/ShieldBashKnockdownService.cs";
    private const string ContextSource = "Main/Features/CombatMechanics/Domain/ShieldBashContext.cs";

    [DataTestMethod]
    [DataRow("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs")]
    [DataRow("Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs")]
    public void Model_AsksHammerhandAfterTheChargeRulesAndBeforeBase(string modelSource)
    {
        var text = OverrideText(modelSource, "DecideAgentKnockedDownByBlow");

        var signature = text.IndexOf("SignatureStrikeVerdicts.Decide(", StringComparison.Ordinal);
        var charge = text.IndexOf("_combat.ChargeKnockdown(", StringComparison.Ordinal);
        var bash = text.IndexOf("_combat.ShieldBashKnockdown(", StringComparison.Ordinal);
        var baseCall = text.IndexOf("base.DecideAgentKnockedDownByBlow(", StringComparison.Ordinal);

        Assert.IsTrue(signature >= 0 && signature < charge, "signature strikes ask first");
        Assert.IsTrue(charge >= 0 && charge < bash, "the charge rules ask before Hammerhand");
        Assert.IsTrue(bash >= 0 && bash < baseCall, "Hammerhand asks before the engine's own answer");
        StringAssert.Contains(text, "?? _combat.ShieldBashKnockdown(", "Hammerhand is a `??` link, so a vanilla true is never taken away");
    }

    // The facade pays for one cheap clause, then builds the context; the toggle, the charge, the shield class, the humanoid test and
    // the immunity verdict are the service's. The two skill reads and the roll live in the builder, behind that clause.
    [TestMethod]
    public void HooksFacade_DeclinesOnOneCheapGateBeforeAnySkillRead()
    {
        var method = HooksMethod("ShieldBashKnockdown");

        var gate = method.IndexOf("!collisionData.IsAlternativeAttack || attackerAgent?.Character == null || victimAgent?.Character == null", StringComparison.Ordinal);
        var build = method.IndexOf("BuildShieldBashContext(", StringComparison.Ordinal);

        Assert.IsTrue(gate >= 0, "the gate is the alternative attack plus both characters");
        Assert.IsTrue(build > gate, "the context is built after the gate");
        Assert.IsFalse(method.Contains("GetEffectiveSkill") || method.Contains("RandomFloat"), "the skill reads and the roll live in the builder, behind the gate");
        StringAssert.Contains(method, "_shieldBashKnockdownService.Decide(in context)");
    }

    // Vanilla sets ShrugOff on nearly every bash and kick (1 to 3 damage against a blunt threshold of 5), so a Hammerhand that read the
    // flag would decline almost all of them. It checks the three deliberate immunities instead.
    [DataTestMethod]
    [DataRow(HooksSource)]
    [DataRow(ServiceSource)]
    [DataRow(ContextSource)]
    public void Hammerhand_NeverReadsTheShrugOffBlowFlag(string path)
    {
        var scope = path == HooksSource
            ? HooksMethod("ShieldBashKnockdown") + Builder()
            : RepoPaths.ReadSource(path, stripComments: true);

        Assert.IsFalse(scope.Contains("BlowFlags.ShrugOff") || scope.Contains("HasShrugOff"), $"{path}: Hammerhand must not read the ShrugOff blow flag");
    }

    [TestMethod]
    public void HooksFacade_BuilderReadsTheThreeImmunitiesBothSkillsTheRaceTheAbilityTheHumanTestThePlayerFlagAndTheRoll()
    {
        var builder = Builder();

        StringAssert.Contains(builder, "_creatureCombatService.IsUnstoppable(victimAgent.Monster?.StringId, collisionData.InflictedDamage)");
        // The counting ShrugsOff is the shrug-off seam's; Hammerhand only asks, so it must not add to the shrug-offs telemetry.
        StringAssert.Contains(builder, "RaceAbilityHooks.IsShruggingOff(victimAgent)");
        Assert.IsFalse(builder.Contains("RaceAbilityHooks.ShrugsOff("), "Hammerhand must use the non-counting query");
        StringAssert.Contains(builder, "victimAgent.IsHero && _careerAgentStatService.ShouldShrugOffBlow((victimAgent.Character as CharacterObject)?.HeroObject?.StringId)");
        StringAssert.Contains(builder, "GetEffectiveSkill(attackerAgent, DefaultSkills.Athletics)");
        StringAssert.Contains(builder, "GetEffectiveSkill(victimAgent, DefaultSkills.Athletics)");
        StringAssert.Contains(builder, "victimRaceId: GetRaceId(victimAgent)");
        StringAssert.Contains(builder, "RaceAbilityHooks.KnockDownResistance(victimAgent, 1f)");
        StringAssert.Contains(builder, "attackerIsHuman: attackerAgent.IsHuman");
        StringAssert.Contains(builder, "victimIsPlayer: victimAgent.IsMainAgent");
        StringAssert.Contains(builder, "randomRoll: MBRandom.RandomFloat");
    }

    // A Hammerhand bash or kick never asks GetKnockDownResistance, so the career TroopKnockdownResistance counter (#769 E10)
    // reaches it only through the ability factor; without this, a "+50% troop knockdown resistance" pick did nothing here.
    [TestMethod]
    public void HooksFacade_VictimResistanceFactor_PassesThroughTheCareerKnockdownCounter()
    {
        StringAssert.Contains(Builder(),
            "victimCareerResistanceScale: factor => CareerTroopHooks.KnockDownResistance(_careerAgentStatService, victimAgent, factor)");
    }

    [TestMethod]
    public void HooksFacade_ReadsAthleticsThroughTheRegisteredStatModel()
    {
        StringAssert.Contains(Builder(), "MissionGameModels.Current.AgentStatCalculateModel");
    }

    [TestMethod]
    public void Service_AndItsContext_NameNoTaleWorldsType()
    {
        foreach (var path in new[] { ServiceSource, ContextSource })
        {
            var source = RepoPaths.ReadSource(path, stripComments: true);
            Assert.IsFalse(source.Contains("TaleWorlds"), $"{path} must stay free of TaleWorlds types (ADR-007)");
        }
    }

    private static string Builder()
    {
        var source = RepoPaths.ReadSource(HooksSource, stripComments: true);
        var start = source.IndexOf("ShieldBashContext BuildShieldBashContext(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "BuildShieldBashContext is gone");
        var end = source.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        return source.Substring(start, end - start);
    }

    private static string HooksMethod(string name)
    {
        var source = RepoPaths.ReadSource(HooksSource, stripComments: true);
        var start = source.IndexOf($" {name}(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"CombatMechanicsHooks no longer declares {name}");
        var end = source.IndexOf("\n    public ", start, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }

    private static string OverrideText(string path, string name)
    {
        var source = RepoPaths.ReadSource(path, stripComments: true);
        var start = source.IndexOf($" {name}(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"{path} no longer declares {name}");
        var end = source.IndexOf("public override", start, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }
}
