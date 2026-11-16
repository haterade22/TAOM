using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// Source pins for the hero abilities' engine side (#1359). The mission logic, the host and the caster need a running
/// mission, so these pin the wiring the rules rest on: the models read the ledger through RaceAbilityHooks; an engine
/// removal callback (any thread, #634) never touches the ledger itself; a grant is published before the stats refresh
/// that reads it; the enemy shock is the shared MoraleShock; only the AI's own hero on this peer decides; the gate reads
/// both mission markers.
/// </summary>
[TestClass]
public class HeroAbilitiesWiringTests
{
    private const string Folder = "Main/Features/HeroAbilities/";

    private static string Read(string file) => RepoPaths.ReadSource(Folder + file, stripComments: true);

    private static string Body(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
                return source.Substring(at, i - at + 1);
        }

        Assert.Fail(marker + " has no end");
        return string.Empty;
    }

    private static int At(string text, string part)
    {
        var at = text.IndexOf(part, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, part + " not found in:\n" + text);
        return at;
    }

    [TestMethod]
    public void Module_HandsTheLedgerToTheModels()
    {
        var module = Read("HeroAbilitiesModule.cs");

        StringAssert.Contains(Body(module, "public override void InitializeStatics("),
            "RaceAbilityHooks.BuffEffects = resolver.Resolve<HeroAbilityRuntime>().Ledger.EffectsOf;");
        StringAssert.Contains(module, "new HeroAbilitiesMissionLogic(r.Resolve<HeroAbilityRuntime>())");
    }

    [TestMethod]
    public void FeatureModules_AddsTheModule()
    {
        StringAssert.Contains(RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true),
            "new Features.HeroAbilities.HeroAbilitiesModule(),");
    }

    [TestMethod]
    public void AgentRemoved_IsDeferredToTheMainTick()
    {
        var logic = Read("Hooks/HeroAbilitiesMissionLogic.cs");
        var removed = Body(logic, "public override void OnAgentRemoved(");

        StringAssert.Contains(removed, "_deferred.RunOrDefer(");
        StringAssert.Contains(removed, "() => _host.Forget(affectedAgent)");
        Assert.IsFalse(removed.Contains("Ledger"), "the removal callback must not write the ledger off the main thread");
        Assert.IsTrue(At(Body(logic, "public override void OnMissionTick("), "_deferred.Drain();")
                      < At(Body(logic, "public override void OnMissionTick("), "Tick(Mission.CurrentTime)"));
    }

    [TestMethod]
    public void MissionLogic_ChecksTheBattlePhaseEachTick_AndStandsDownOnAThrow()
    {
        var logic = Read("Hooks/HeroAbilitiesMissionLogic.cs");
        var tick = Body(logic, "public override void OnMissionTick(");

        StringAssert.Contains(tick, "Mission.Mode == MissionMode.Battle");
        StringAssert.Contains(logic, "[HeroAbilities] disabled for this mission after ");
        StringAssert.Contains(Body(logic, "private void StandDown("), "_host.StandDown();");
    }

    [TestMethod]
    public void Caster_PublishesTheGrantBeforeTheRefresh()
    {
        var caster = Read("Hooks/HeroAbilityCaster.cs");
        var grant = Body(caster, "private void Grant(");

        Assert.IsTrue(Regex.IsMatch(grant, @"_runtime\.Ledger\.Grant\(agent, source, effects, until\);\s*Refresh\(agent\);"), grant);
        var refresh = Body(caster, "internal static void Refresh(");
        Assert.IsTrue(At(refresh, "AgentSlotIdentity.IsCurrentOccupant(agent)") < At(refresh, "agent.UpdateAgentProperties();"));
        StringAssert.Contains(refresh, "agent.MountAgent?.UpdateAgentProperties();");
    }

    [TestMethod]
    public void Caster_Allies_AreTheHerosOwnTeamOnThisPeer_NeverHimself()
    {
        var allies = Body(Read("Hooks/HeroAbilityCaster.cs"), "private int Allies(");

        foreach (var guard in new[]
                 {
                     "ReferenceEquals(ally, hero)", "!ally.IsHuman", "!ally.IsActive()", "!AgentSlotIdentity.IsCurrentOccupant(ally)",
                     "ally.Team != team", "!CoopMissionOwnership.IsOwnedHere(ally)",
                 })
            Assert.IsTrue(At(allies, guard) < At(allies, "Grant(ally,"), guard + " must come before the grant");
        Assert.IsTrue(At(allies, "ally.IsAIControlled") < At(allies, "ally.ChangeMorale("));
    }

    [TestMethod]
    public void Caster_ShocksEnemiesThroughMoraleShock()
    {
        var shock = Body(Read("Hooks/HeroAbilityCaster.cs"), "private int Shock(");

        StringAssert.Contains(shock, "MoraleShock.Apply(enemy, ability.EnemyShockMorale, 1f, model, _runtime.CareerStats, _runtime.DreadRegistry)");
        Assert.IsFalse(shock.Contains("ChangeMorale("), "the drain is MoraleShock's, with its gate");
    }

    [TestMethod]
    public void Host_OnlyTheAIsOwnHereHeroDecides()
    {
        var run = Body(Read("Hooks/HeroAbilityHost.cs"), "internal void RunAll(");
        var pick = At(run, "PickAbility(");

        Assert.IsTrue(At(run, "!AgentSlotIdentity.IsCurrentOccupant(hero)") < pick);
        Assert.IsTrue(At(run, "!CoopMissionOwnership.IsOwnedHere(hero)") < pick);
        Assert.IsTrue(At(run, "ShouldDecide(hero.IsAIControlled, hero.IsPlayerControlled, hero.IsMainAgent, hero.IsRunningAway)") < pick);
    }

    [TestMethod]
    public void Host_ExpiryRefreshesThroughTheGuardedRefresh()
    {
        var expire = Body(Read("Hooks/HeroAbilityHost.cs"), "internal void Expire(");

        Assert.IsTrue(At(expire, "_runtime.Ledger.Expire(now, _changed);") < At(expire, "HeroAbilityCaster.Refresh("));
    }

    [TestMethod]
    public void Host_ContactCountsEnemiesByThe3DDistanceAgainstTheRulesCount()
    {
        var contact = Body(Read("Hooks/HeroAbilityHost.cs"), "private bool InContact(");

        StringAssert.Contains(contact, "!record.HasContactRule");
        StringAssert.Contains(contact, "Math.Min(record.ContactRange, MaxQueryRadius)");
        StringAssert.Contains(contact, "enemy.IsActive() && enemy.IsHuman && enemy.Position.Distance(hero.Position) <= record.ContactRange");
        StringAssert.Contains(contact, "return count >= record.ContactCount;");
        StringAssert.Contains(Read("Hooks/HeroAbilityHost.cs"), "_runtime.Service.TryContactRule(kit,");
    }

    [TestMethod]
    public void Caster_StatsWantIsComputedFromTheEffectsBeforeTheGrant()
    {
        var sample = Body(Read("Hooks/HeroAbilityCaster.cs"), "private void Sample(");

        StringAssert.Contains(sample, "HeroAbilityChecks.SwingRatio(before, RaceAbilityEffectsMerge.Combine(before, effects))");
        Assert.IsTrue(At(sample, "var before = RaceAbilityHooks.MergedEffectsOf(agent);") < At(sample, "_runtime.Ledger.Grant("));
        Assert.IsFalse(sample.Contains("SwingRatio(before, RaceAbilityHooks"), "want must not read the hooks after the grant");
    }

    [TestMethod]
    public void Caster_WarCryObeysTheRaceAbilitySwitch()
    {
        var cast = Body(Read("Hooks/HeroAbilityCaster.cs"), "internal CastResult Cast(");

        Assert.IsTrue(At(cast, "if (_runtime.RaceAbilities.Settings.WarCries)") < At(cast, "RaceAbilityActivator.WarCry("));
    }

    [TestMethod]
    public void Gate_ReadsTheCombatGateAndTheLongDefeatMarker()
    {
        var read = Body(Read("Hooks/HeroMissionGate.cs"), "internal static (bool Combat, bool LongDefeat) Read(");

        StringAssert.Contains(read, "SignatureMissionGate.IsEligible(mission)");
        StringAssert.Contains(read, "GetMissionBehavior<LongDefeatMissionLogic>() != null");
        Assert.IsFalse(Read("Hooks/HeroMissionGate.cs").Contains("StoryMissionLogic"), "the story arm is dead: the story launcher adds LongDefeatMissionLogic too");
    }

    [TestMethod]
    public void Outline_GoesThroughTheRaceVisualsOneWriteSite()
    {
        var visuals = RepoPaths.ReadSource("Main/Features/RaceAbilities/Hooks/RaceAbilityVisuals.cs", stripComments: true);

        StringAssert.Contains(Body(visuals, "public void Outline("), "Paint(agent,");
        foreach (var file in new[] { "Hooks/HeroAbilityHost.cs", "Hooks/HeroAbilityCaster.cs" })
            Assert.IsFalse(Read(file).Contains("SetContourColor("), file + " must paint through RaceAbilityVisuals.Outline");
    }
}
