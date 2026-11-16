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

    // #1361: the zero-aim pass is the last statement of each stat model's UpdateAgentStats, so no later multiply (the
    // Long Defeat affixes scale AiShooterError) or overwrite can undo it. Both families: a rule pinned in one only is
    // the two-family trap.
    private static void AssertRangedPassIsLast(string path)
    {
        var update = Body(RepoPaths.ReadSource(path, stripComments: true), "public override void UpdateAgentStats(");
        const string call = "HeroRangedHooks.ApplyStats(agent, agentDrivenProperties);";
        var at = At(update, call);

        Assert.AreEqual("}", update.Substring(at + call.Length).Trim(), path + ": the ranged pass must be the last statement");
        Assert.AreEqual(at, update.LastIndexOf("HeroRangedHooks.", StringComparison.Ordinal), "one call");
    }

    [TestMethod]
    public void CustomBattleStatModel_RangedPassIsLast()
    {
        const string path = "Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs";
        AssertRangedPassIsLast(path);
        var update = Body(RepoPaths.ReadSource(path, stripComments: true), "public override void UpdateAgentStats(");
        Assert.IsTrue(At(update, "LongDefeatStatHooks.UpdateStats(agent, agentDrivenProperties);") < At(update, "HeroRangedHooks.ApplyStats("));
    }

    [TestMethod]
    public void CampaignStatModel_RangedPassIsLast()
    {
        AssertRangedPassIsLast("Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs");
    }

    // The entry the models call looks the agent up before it reads any engine getter, and hands the mount test to the
    // tested ApplyFor.
    [TestMethod]
    public void RangedHooks_ApplyStats_LooksUpBeforeTheEngineAndDelegates()
    {
        var apply = Body(Read("Hooks/HeroRangedHooks.cs"), "public static void ApplyStats(");

        Assert.IsTrue(At(apply, ".KitOf(agent) == null") < At(apply, "ApplyFor(roster, agent, agent.IsMount, properties);"));
    }

    [TestMethod]
    public void Module_SetsRosterAndPierceDelegate()
    {
        var statics = Body(Read("HeroAbilitiesModule.cs"), "public override void InitializeStatics(");

        StringAssert.Contains(statics, "HeroRangedHooks.Roster = resolver.Resolve<HeroAbilityRuntime>().Ranged;");
        StringAssert.Contains(statics, "CombatMechanicsHooks.ShooterPiercesShields = HeroRangedHooks.PiercesShields;");
        StringAssert.Contains(Read("HeroAbilityRuntime.cs"), "public HeroRangedRoster<Agent> Ranged { get; } = new();");
    }

    // The grant is ORed after the shield-penetration service, so its MCM switch (ShieldPenetrationService.cs:51) cannot
    // turn it off, and it never adds MultiplePenetration. The floor reads the engine's two parameters only when granted.
    [TestMethod]
    public void CombatHooks_GrantAfterTheService_FloorReadsParametersOnlyWhenGranted()
    {
        var hooks = RepoPaths.ReadSource("Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs", stripComments: true);
        var flags = Body(hooks, "public WeaponFlags PenetrationFlags(");

        Assert.IsTrue(At(flags, "_shieldPenetrationService.ApplyPenetrationFlags(") < At(flags, "ShooterPiercesShields"));
        StringAssert.Contains(flags, "| WeaponFlags.CanPenetrateShield");
        Assert.IsFalse(flags.Contains("MultiplePenetration"));

        var floor = Body(hooks, "public float ShieldPierceFloor(");
        Assert.IsTrue(At(floor, "ShooterPiercesShields?.Invoke(attacker) != true") < At(floor, "ManagedParameters.Instance"));
        StringAssert.Contains(floor, "ManagedParametersEnum.ShieldPenetrationOffset");
        StringAssert.Contains(floor, "ManagedParametersEnum.ShieldPenetrationFactor");
        StringAssert.Contains(floor, "attackInformation.VictimShield.GetGetModifiedArmorForCurrentUsage()");
        StringAssert.Contains(floor, "ShieldPierceMath.Floor(");
        Assert.IsTrue(At(floor, "ShieldPenetrationOffset") < At(floor, "ShieldPenetrationFactor"), "the arguments are offset, then factor");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CombatMechanics/ShieldPierceMath.cs", stripComments: true),
            "public static float Floor(float damage, float offset, float factor, float armour)");
    }

    // ---- #1361 Slice 3: the engine side of the ranged kit ----

    // Native may raise the three missile callbacks off the main thread (#634): each only reads the roster's published
    // map and enqueues; the tick drains. The kill, the extra arrows and the tally live behind the drain.
    [TestMethod]
    public void RangedLogic_CallbacksOnlyEnqueue()
    {
        var logic = Read("Hooks/HeroRangedMissionLogic.cs");

        foreach (var marker in new[] { "public override void OnAgentShootMissile(", "public override void OnMissileHit(", "public override void OnAgentHit(" })
        {
            var body = Body(logic, marker);
            Assert.IsTrue(At(body, "KitOf(") < At(body, "_queue.Enqueue("), marker + ": roster shooters only");
            foreach (var banned in new[] { "TryKill", "AddCustomMissile", "_runner", "_kills", "TallyOf", "IsEnemyOf", "Outcomes" })
                Assert.IsFalse(body.Contains(banned), marker + " must not touch " + banned);
        }

        var tick = Body(logic, "public override void OnMissionTick(");
        Assert.IsTrue(At(tick, "MissionThreadGuard.MarkMainThread();") < At(tick, "_queue.TryDequeue("));
        Assert.IsTrue(At(tick, "try") < At(tick, "_queue.TryDequeue("));
        StringAssert.Contains(logic, "[HeroAbilities] ranged kit disabled for this mission after ");
        StringAssert.Contains(Body(logic, "public override void OnRemoveBehavior("), "HeroRangedChecks.WriteEnd(");
    }

    // A friendly hit proves nothing and must never roll a kill: the enemy test is the only guard before the roll, since
    // the instant-kill executor checks no team. Only a missile blow counts, and extras fire only inside the window.
    [TestMethod]
    public void RangedLogic_HandleKeepsEnemyMissileAndWindowGuards()
    {
        var logic = Read("Hooks/HeroRangedMissionLogic.cs");
        var handle = Body(logic, "private void Handle(");
        var enemy = At(handle, "heroTeam.IsEnemyOf(e.VictimTeam)");

        Assert.IsTrue(enemy < At(handle, "_kills.OnBodyHit("), "the enemy test comes before the kill roll");
        Assert.IsTrue(enemy < At(handle, "t.MissileHit("), "the enemy test comes before the pierce tally");
        StringAssert.Contains(Body(logic, "public override void OnAgentHit("), "!blow.IsMissile");
        Assert.IsTrue(At(handle, "burst.Until > now") < At(handle, "_runner.Fire("), "extras fire only inside the burst window");
    }

    // Native hands a freed missile index to the next missile at once, so the tallies forget an index when its missile is
    // removed (Mission.OnMissileRemovedEvent, Mission.cs:1573, raised at :5770). The handler only enqueues, behind the hits
    // of that missile, and only while a hero is listed and the logic stands.
    [TestMethod]
    public void RangedLogic_OnMissileRemovedOnlyEnqueues()
    {
        var logic = Read("Hooks/HeroRangedMissionLogic.cs");
        var removed = Body(logic, "private void QueueMissileRemoved(");

        Assert.IsTrue(At(removed, "_disabled") < At(removed, "_queue.Enqueue(HeroMissileEvent.Removed("));
        Assert.IsTrue(At(removed, "_runtime.Ranged.IsEmpty") < At(removed, "_queue.Enqueue(HeroMissileEvent.Removed("));
        foreach (var banned in new[] { "TryKill", "AddCustomMissile", "_runner", "_kills", "_tallies", "Outcomes" })
            Assert.IsFalse(removed.Contains(banned), "the removal handler must not touch " + banned);
        StringAssert.Contains(Body(logic, "public override void AfterStart("), "Mission.OnMissileRemovedEvent += QueueMissileRemoved;");
        Assert.IsTrue(At(Body(logic, "private void Handle("), "HeroMissileKind.Removed") < At(Body(logic, "private void Handle("), "_tallies.Of("),
            "a removal names no hero and goes to every tally");
    }

    [TestMethod]
    public void RangedLogic_UnsubscribesInOnRemoveBehavior()
    {
        var end = Body(Read("Hooks/HeroRangedMissionLogic.cs"), "public override void OnRemoveBehavior(");

        StringAssert.Contains(end, "Mission.OnMissileRemovedEvent -= QueueMissileRemoved;");
        Assert.IsTrue(At(end, "Mission.OnMissileRemovedEvent -= QueueMissileRemoved;") < At(end, "base.OnRemoveBehavior();"));
    }

    // An arrow is an extra by its item alone: the missile index is reused at once, so a remembered index would misread
    // his next primary arrow. OnAgentHit carries the missile's weapon (Mission.cs:5657-5658); OnMissileHit reads the
    // missile from the mission's list by the index the hit carries.
    [TestMethod]
    public void RangedLogic_TellsAnExtraByItsItemOnly()
    {
        var logic = Read("Hooks/HeroRangedMissionLogic.cs");
        var runner = Read("Hooks/HeroMultiShotRunner.cs");

        StringAssert.Contains(Body(logic, "public override void OnMissileHit("), "LongDefeatVolleyHooks.MissileItem(Mission, collisionData.AffectorWeaponSlotOrMissileIndex) ?? \"\"");
        StringAssert.Contains(Body(RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatVolleyHooks.cs", stripComments: true), "internal static string? MissileItem("), "mission.MissilesList");
        StringAssert.Contains(Body(logic, "private void Handle("), "e.IsExtraArrow");
        StringAssert.Contains(Read("Hooks/HeroMissileQueue.cs"), "internal bool IsExtraArrow => Kit.ExtraArrowItem.Length > 0 && ItemId == Kit.ExtraArrowItem;");
        foreach (var gone in new[] { "IsExtra(", "_extras", "IndexMemorySeconds" })
        {
            Assert.IsFalse(logic.Contains(gone), "logic: " + gone);
            Assert.IsFalse(runner.Contains(gone), "runner: " + gone);
        }
        var tally = Read("Hooks/HeroRangedTally.cs");
        Assert.IsFalse(tally.Contains("IndexMemorySeconds") || tally.Contains("PierceWindowSeconds"), "no time window: an index lives until its removal");
    }

    [TestMethod]
    public void RangedLogic_IsAgentOwner()
    {
        Assert.AreEqual(TAOM.Features.CoopInterop.Missions.CoopMissionRole.AgentOwner,
            TAOM.Features.CoopInterop.Missions.CoopMissionRoles.Of(typeof(TAOM.Features.HeroAbilities.Hooks.HeroRangedMissionLogic)));
    }

    // One executor class for the melee strike and the arrow (decision R3): the module builds it from the same services
    // SignatureStrikesMissionLogic does.
    [TestMethod]
    public void Module_AddsTheRangedLogicWithTheSharedExecutor()
    {
        var module = Read("HeroAbilitiesModule.cs");

        StringAssert.Contains(module, "new HeroRangedMissionLogic(runtime, new SignatureInstantKill(r.Resolve<ISignatureStrikeService>(), r.Resolve<ISignatureAgentRoster>(), r.Resolve<ISignatureStrikeRegistry>(), runtime.Outcomes, runtime.Logger))");
    }

    [TestMethod]
    public void Host_AddsRosterBeforeRefresh()
    {
        var run = Body(Read("Hooks/HeroAbilityHost.cs"), "internal void RunAll(");
        var passive = Body(run, "if (!record.PassiveGiven)");

        Assert.IsTrue(At(passive, "_runtime.Ranged.Add(hero, record.Kit.Ranged);") < At(passive, "HeroAbilityCaster.Refresh(hero);"),
            "the refresh must read the roster with him in it");
        Assert.IsTrue(At(run, "!CoopMissionOwnership.IsOwnedHere(hero)") < At(run, "_runtime.Ranged.Add("), "owned heroes only");
    }

    [TestMethod]
    public void Host_StandDownClearsRosterBeforeExpire()
    {
        var standDown = Body(Read("Hooks/HeroAbilityHost.cs"), "internal void StandDown(");
        var first = standDown.Substring(standDown.IndexOf('{') + 1).TrimStart();

        StringAssert.StartsWith(first, "_runtime.Ranged.Clear();", "the first statement");
        Assert.IsTrue(At(standDown, "_runtime.Ranged.Clear();") < At(standDown, "Expire(float.PositiveInfinity);"),
            "the expiry's refresh must drop the zeros");
    }

    [TestMethod]
    public void Host_DropForgetAndClear_LeaveTheRoster()
    {
        var host = Read("Hooks/HeroAbilityHost.cs");

        StringAssert.Contains(Body(host, "private void Drop("), "_runtime.Ranged.Remove(record.Agent);");
        StringAssert.Contains(Body(host, "private void Drop("), "_runtime.Bursts.Remove(record.Agent);");
        var forget = Body(host, "internal void Forget(");
        StringAssert.Contains(forget, "_runtime.Ledger.Remove(agent);", "a non-hero ally carries grants too");
        StringAssert.Contains(forget, "Drop(record);", "Drop takes the hero off the roster");
        Assert.IsFalse(forget.Contains("_runtime.Ranged.Remove("), "Drop already removes him");
        StringAssert.Contains(Body(host, "internal void Clear("), "_runtime.Ranged.Clear();");
        StringAssert.Contains(Body(host, "internal void Clear("), "_runtime.Bursts.Clear();");
    }

    // A 40 m trigger with the query clamped to 15 m would count no contact, and the fired check could never fail.
    [TestMethod]
    public void Host_ContactQueryUnclamped()
    {
        var host = Read("Hooks/HeroAbilityHost.cs");

        StringAssert.Contains(Body(host, "private bool InContact("), "mission.GetNearbyEnemyAgents(hero.Position.AsVec2, record.ContactRange, team, _scratch);");
        Assert.IsFalse(host.Contains("MaxQueryRadius"));
    }

    [TestMethod]
    public void Caster_BurstOnExtraArrows_AndRangedOnlyAllies()
    {
        var caster = Read("Hooks/HeroAbilityCaster.cs");

        StringAssert.Contains(Body(caster, "internal CastResult Cast("), "if (ability.ExtraArrows > 0)");
        StringAssert.Contains(Body(caster, "internal CastResult Cast("), "_runtime.Bursts[hero] = new HeroBurst(until, ability.ExtraArrows);");
        var allies = Body(caster, "private int Allies(");
        Assert.IsTrue(At(allies, "if (ability.AlliesRangedOnly && !ally.IsRangedCached)") < At(allies, "Grant(ally,"));
    }

    // AddCustomMissile writes missionObjectToIgnore.Id on a server or a recorder (Mission.cs:4972-4975), and the extra
    // arrow has no object to ignore.
    [TestMethod]
    public void MultiShot_RefusesServerOrRecorder()
    {
        var fire = Body(Read("Hooks/HeroMultiShotRunner.cs"), "internal void Fire(");

        Assert.IsTrue(At(fire, "if (GameNetwork.IsServerOrRecorder)") < At(fire, "AddCustomMissile("));
        Assert.IsTrue(At(fire, "!AgentSlotIdentity.IsCurrentOccupant(hero) || !hero.IsActive()") < At(fire, "AddCustomMissile("), "the blow owner is the live hero");
        StringAssert.Contains(fire, "AddCustomMissile(hero, weapon, origin, direction.Value, orientation, BaseSpeed(hero, speed), speed, false, null)");
        Assert.IsFalse(fire.Contains("Added") || fire.Contains("MissilesList"), "AddCustomMissile always lists the missile (Mission.cs:4969-4971): no growth count");
    }

    [TestMethod]
    public void MultiShot_AimsWithTheEngineSolver()
    {
        var runner = Read("Hooks/HeroMultiShotRunner.cs");

        StringAssert.Contains(runner, "Mission.GetMissileVerticalAimCorrection(");
        StringAssert.Contains(runner, "ItemObject.GetAirFrictionConstant(usage.WeaponClass, usage.WeaponFlags)");
        StringAssert.Contains(runner, "weapon.GetWeaponStatsDataForUsage(0)");
        StringAssert.Contains(runner, "ExtraShotAim.IsUsableAngle(");
    }

    // A dead attacker makes TakeDamage a no-op, and TryKill would then write died=0.
    [TestMethod]
    public void ArrowKills_ChecksShooterLiveBeforeTryKill()
    {
        var hit = Body(Read("Hooks/HeroArrowKills.cs"), "internal void OnBodyHit(");
        var call = At(hit, "_executor.TryKill(");

        Assert.IsTrue(At(hit, "!AgentSlotIdentity.IsCurrentOccupant(hero) || !hero.IsActive() || hero.IsFadingOut()") < call,
            "TakeDamage refuses a fading attacker (CustomAttacksUtils.cs:185)");
        Assert.IsTrue(At(hit, "t.DeadShooter++;") < call);
        Assert.IsTrue(At(hit, "e.BlockedShield || e.BlockedWeapon") < call, "never a kill on a blocked hit");
        Assert.IsTrue(At(hit, "t.Exempt++;") < call, "an extra arrow never rolls");
        StringAssert.Contains(hit, "HeroRangedService.ArrowKillChance(");
        StringAssert.Contains(hit, "e.Victim.IsActive() && e.Victim.Health > 0f", "a victim the arrow already killed is no call");
        Assert.IsTrue(At(hit, "if (!(chance > 0f))") < At(hit, "t.Calls++;"));
        StringAssert.Contains(hit, "_executor.TryKill(hero, e.Victim, chance, Magnitude, t.HeroId + \"_arrow\")");
    }
}
