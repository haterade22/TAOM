using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.Stamina.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Stamina;

/// <summary>
/// Source-text pins for stamina's wiring (#1497): who calls what, and in which order. Each rule itself has behaviour
/// tests in its own pure type (StaminaLockRules, StaminaInputRules, StaminaRefreshQueue, StaminaStatApplier).
/// </summary>
[TestClass]
public class StaminaWiringTests
{
    private static string Code(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    private static int At(string code, string needle)
    {
        var index = code.IndexOf(needle, StringComparison.Ordinal);
        Assert.IsTrue(index >= 0, "not found: " + needle);
        return index;
    }

    [TestMethod]
    public void StoppedBlows_AreChargedOnlyToASoldierTheRulesStep()
    {
        // The hit callbacks add to a cell from any thread and only a step takes the sum. A cell that is never stepped
        // (an exempt people, an MCM opt-out, another peer's soldier, a forced band) would keep every blow he stopped and
        // pay for all of them at the look that steps him again.
        var logic = Code("Main/Features/Stamina/Hooks/StaminaMissionLogic.cs");
        StringAssert.Contains(logic, "private void Charge(StaminaCell cell, StaminaBlock block, float shieldDamage) { if (!cell.Tracked) return;");

        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        var visit = ticker.Substring(At(ticker, "private void Visit(Agent agent, StaminaCell cell, float now, StaminaTuning tuning)"));
        visit = visit.Substring(0, At(visit, "private void Enrol("));
        // A soldier at a siege engine pays nothing, a stopped blow included: dropped before the step.
        Assert.IsTrue(At(visit, "if (agent.IsUsingGameObject) StaminaService.Skip(cell);") < At(visit, "StaminaService.Step("));
        StringAssert.Contains(visit, "else { StaminaService.Skip(cell); }");
    }

    [TestMethod]
    public void TheLockCounters_ComeFromTheTallyRule_AndTheStuckClockNeedsABandTheRulesDecided()
    {
        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        var settle = ticker.Substring(At(ticker, "private void Settle("));
        settle = settle.Substring(0, At(settle, "private static void OnBandChanged("));

        StringAssert.Contains(settle, "var byRules = owned && cell.Forced == null;");
        StringAssert.Contains(settle, "var tally = StaminaLockRules.Tally(before, band, byRules, cell.BandByRules, cell.EntryCounted);");
        StringAssert.Contains(settle, "if (tally.Entered)");
        StringAssert.Contains(settle, "if (tally.Recovered)");
        StringAssert.Contains(settle, "byRules && !tally.StartClock && !cell.StuckReported && now - cell.ExhaustedSince > StuckSeconds");
        // The counters have one writer: the band-change handler only talks to the player.
        var changed = ticker.Substring(At(ticker, "private static void OnBandChanged("));
        changed = changed.Substring(0, At(changed, "private void HoldAiStrip("));
        Assert.IsFalse(changed.Contains("Telemetry"), changed);
    }

    [TestMethod]
    public void TheHudRead_AndTheConsoleForce_LeaveOutASoldierWithNoStamina()
    {
        // An exempt people or an MCM opt-out: no bar that stays full for ever, and no "N soldiers set" for soldiers
        // whose band the console cannot change.
        var hooks = Code("Main/Features/Stamina/Hooks/StaminaHooks.cs");
        var read = hooks.Substring(At(hooks, "public static bool TryRead("));
        StringAssert.Contains(read.Substring(0, At(read, "return true;")), "|| !cell.Tracked) return false;");

        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        var force = ticker.Substring(At(ticker, "public string Force("));
        Assert.IsTrue(At(force, "if (band != null && !entry.Value.Tracked) continue;") < At(force, "entry.Value.Forced = band;"));
    }

    [DataTestMethod]
    [DataRow("Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs")]
    [DataRow("Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs")]
    public void BothStatModels_ApplyStaminaRightAfterTheRaceAbilities(string model)
    {
        // The engine keeps one stat model per game mode, so each needs the call: without the Custom Battle one stamina
        // does nothing there. After the race abilities, so a tired soldier's multipliers scale his final values.
        StringAssert.Contains(Code(model),
            "RaceAbilityHooks.ApplyStats(agent, agentDrivenProperties); StaminaHooks.ApplyStats(agent, agentDrivenProperties);");
    }

    [TestMethod]
    public void TheModule_IsListed_AndDeclaresTheMissionLogicAndThePatchCategory()
    {
        var module = FeatureModules.All.Single(m => m.Id == "Stamina");

        Assert.AreEqual(nameof(StaminaMissionLogic), module.MissionBehaviors.Single().BehaviorType.Name);
        var category = module.PatchCategories.Single();
        Assert.AreEqual("Patch171_Stamina", category.Category);
        // Marshal's Word's reason: preparing a patched method early can run a type initializer that needs Mission.Current.
        Assert.AreEqual(ApplyPhase.FirstMission, category.Phase);
    }

    [TestMethod]
    public void TheMissionLogic_IsAnAgentOwner_AndTheTickAsksWhoOwnsASoldierBeforeItActsOnHim()
    {
        Assert.AreEqual(CoopMissionRole.AgentOwner, CoopMissionRoles.All[nameof(StaminaMissionLogic)]);

        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        var visit = ticker.Substring(At(ticker, "private void Visit(Agent agent, StaminaCell cell, float now, StaminaTuning tuning)"));
        var owned = At(visit, "var owned = CoopMissionOwnership.IsOwnedHere(agent);");
        // A soldier another peer owns reads Fresh, so every write after this line gives him back what stamina took.
        StringAssert.Contains(visit, "if (!owned) { band = StaminaBand.Fresh; fraction = 1f; }");
        Assert.IsTrue(owned < At(visit, "StaminaService.Step("), "ownership before anything drains or comes back");
        Assert.IsTrue(owned < At(visit, "cell.Publish("), "ownership before the band is published");
        Assert.IsTrue(owned < At(visit, "HoldAiStrip(agent);"), "ownership before the AI input component and its callback claim");
        Assert.IsTrue(At(visit, "if (cell.Removed || !IsLive(agent)) { cell.Tracked = false; return; }") < owned, "a dead agent's handle is refused before any other read");
        // The per-frame action poll runs only for a soldier the last look found alive and owned by this peer.
        // "The player's soldier" is the main agent or one a player controls. Not "not AI controlled": an agent with no
        // controller (the Fords tower gunners) is nobody's.
        StringAssert.Contains(visit, "var subject = owned && !cell.Exempt && StaminaService.Applies(agent.IsMainAgent || agent.IsMine, _applyToPlayer, _applyToAi); cell.Tracked = subject;");
        // The two MCM opt-outs are read once per tick, as the cost scale is.
        Assert.AreEqual(1, Regex.Matches(ticker, @"_runtime\.Settings\.ApplyToPlayer").Count);
        Assert.AreEqual(1, Regex.Matches(ticker, @"_runtime\.Settings\.ApplyToAi").Count);
        // The profile (race, culture, armour) is read once, and only for a soldier this peer owns.
        StringAssert.Contains(visit, "if (owned && !cell.Enrolled) Enrol(agent, cell, tuning);");
        StringAssert.Contains(ticker, "if (entry.Value.Tracked && !entry.Value.Removed) Poll(entry.Key, entry.Value, now, tuning);");

        var force = ticker.Substring(At(ticker, "public string Force("));
        StringAssert.Contains(force, "!CoopMissionOwnership.IsOwnedHere(agent)");
    }

    [TestMethod]
    public void OnlyTheClaimsType_WritesTheSharedAiInputCallbackFlag()
    {
        // Two features need the engine's one flag per agent. A direct write by either switches the callback off under the other.
        var writers = RepoPaths.CSharpFiles(RepoPaths.CodeRoots)
            .Where(path => RepoPaths.MentionsOutsideComments(path, "SetHasOnAiInputSetCallback"))
            .Select(Path.GetFileName)
            .ToList();

        CollectionAssert.AreEqual(new[] { "AiInputCallbackClaims.cs" }, writers);
    }

    [TestMethod]
    public void TheTicker_PutsTheInputComponentLast_AndGivesEveryClaimBack()
    {
        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        var hold = ticker.Substring(At(ticker, "private void HoldAiStrip(Agent agent)"));

        // Whenever another component sits after it, it is removed and added again, so it runs after a component that
        // adds an attack input (Agent.cs:1660-1664 calls them in list order). Checked on every look, not only on entry:
        // Marshal's Word can take an already exhausted soldier and add its component then.
        StringAssert.Contains(hold, "if (component == null || components[components.Count - 1] != component)");
        Assert.IsTrue(At(hold, "agent.RemoveComponent(component);") < At(hold, "agent.AddComponent(component);"));
        var visit = ticker.Substring(At(ticker, "private void Visit(Agent agent, StaminaCell cell, float now, StaminaTuning tuning)"));
        StringAssert.Contains(visit, "if (path == StaminaLockPath.AiInput) HoldAiStrip(agent); if (step.StartAiStrip) AiInputCallbackClaims.Claim(agent, AiInputCallbackClaims.Stamina);");
        StringAssert.Contains(ticker, "AiInputCallbackClaims.Release(agent, AiInputCallbackClaims.Stamina);");
        // A soldier who left the mission: his claim goes with no engine call on a handle that may answer for another agent (#592).
        StringAssert.Contains(ticker, "if (cell.AppliedPath == StaminaLockPath.AiInput) AiInputCallbackClaims.Forget(agent, AiInputCallbackClaims.Stamina);");
        StringAssert.Contains(ticker, "AiInputCallbackClaims.DropOwner(AiInputCallbackClaims.Stamina);");

        // Mission end: the engine has cleared every agent's native pointers before a behavior is removed
        // (Mission.EndMissionInternal -> Agent.Clear), so the teardown may not read an agent or call the engine. A first
        // version called Agent.IsActive there, faulted, and left the claims of the dead mission in the static ledger.
        var teardownStart = At(ticker, "public void Teardown()");
        var teardownBody = ticker.Substring(teardownStart, At(ticker, "public void Clear()") - teardownStart);
        StringAssert.Contains(teardownBody, "AiInputCallbackClaims.DropOwner(AiInputCallbackClaims.Stamina)");
        foreach (var forbidden in new[] { "agent", "Agent", "IsActive", "IsLive", "_runtime.Store", "StopAiStrip", "Release(" })
            Assert.IsFalse(teardownBody.Contains(forbidden), $"Teardown must not read an agent or call the engine: found \"{forbidden}\" in: {teardownBody}");
        // In a running mission: the slot's owner first, then his state, as Marshal's Word's TurnOff does.
        StringAssert.Contains(ticker, "private static bool IsLive(Agent agent) => AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive();");

        var logic = Code("Main/Features/Stamina/Hooks/StaminaMissionLogic.cs");
        var teardown = logic.Substring(At(logic, "public override void OnRemoveBehavior()"));
        Assert.IsTrue(At(teardown, "_runtime.Ticker.Teardown();") < At(teardown, "finally { _runtime.Clear();"), "the claims go back before the cells are cleared, and the clear runs even if that throws");
        Assert.IsTrue(At(teardown, "_runtime.Ticker.Teardown();") < At(teardown, "_runtime.Ticker.Describe()"), "the claims go before the log line is built, so a throw there cannot keep them");
        // An AI soldier keeps the jump under the same two conditions as the player's: mounted, or in water.
        StringAssert.Contains(ticker, "component.Set(deny: true, mounted: agent.HasMount, inWater: agent.IsInWater());");
        StringAssert.Contains(Code("Main/Features/Stamina/Hooks/StaminaInputComponent.cs"), "StaminaInputRules.StripEvents(events, _mounted, _inWater);");
        StringAssert.Contains(Code("Main/Features/Stamina/Hooks/StaminaHooks.cs"), "StaminaInputRules.StripEvents(events, agent.HasMount, agent.IsInWater());");
    }

    [TestMethod]
    public void StatsAreRecomputed_OnlyFromTheTickersBudgetedBatch()
    {
        // Agent.UpdateAgentProperties runs the whole stat model. A second call site would escape the per-frame budget.
        var callers = RepoPaths.CSharpFiles("Main/Features/Stamina", "Client/Features/Stamina")
            .Where(path => RepoPaths.MentionsOutsideComments(path, "UpdateAgentProperties"))
            .Select(Path.GetFileName)
            .ToList();
        CollectionAssert.AreEqual(new[] { "StaminaTicker.cs" }, callers);

        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        Assert.AreEqual(1, Regex.Matches(ticker, @"\.UpdateAgentProperties\(\)").Count);
        StringAssert.Contains(ticker, "_runtime.Refreshes.Take(now, tuning.MaxRefreshesPerFrame, tuning.MinRefreshAgeSeconds, _due);");
    }

    [TestMethod]
    public void ThePoll_ReadsTwoPointers_AndAsksTheEngineOnlyWhenAnActionChanged()
    {
        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        var pollStart = At(ticker, "private void Poll(Agent agent, StaminaCell cell, float now, StaminaTuning tuning)");
        var poll = ticker.Substring(pollStart, At(ticker, "private static StaminaAction OnFootOnly(") - pollStart);

        // Agent.GetCurrentAction is a pointer read (Agent.cs:2782-2785); GetCurrentActionType is an engine call.
        var unchanged = At(poll, "if (action0 == cell.LastAction0 && action1 == cell.LastAction1) return;");
        Assert.IsTrue(unchanged < At(poll, "agent.GetCurrentActionType(0)"), "no engine call while the action is the same");
        Assert.IsTrue(At(poll, "if (!IsLive(agent)) return;") < At(poll, "agent.GetCurrentActionType(0)"), "a dead handle is refused before the engine call");
        // A forced soldier and a siege engine's crew pay nothing; the horse's jump and a rider's kick are free.
        Assert.IsTrue(At(poll, "if (cell.Forced != null || agent.IsUsingGameObject) return;") < At(poll, "Charge(cell, kind1"));
        StringAssert.Contains(poll, "if (agent.HasMount) { kind0 = OnFootOnly(kind0); kind1 = OnFootOnly(kind1); }");
        // One action that shows on both channels in a frame is charged once.
        StringAssert.Contains(poll, "if (kind0 != StaminaAction.None && kind0 != kind1) Charge(cell, kind0, weaponKg, now, economy);");
        // The weight of one item: MissionWeapon.GetWeight() is the whole stack plus loaded ammunition.
        StringAssert.Contains(poll, "agent.WieldedWeapon.Item?.Weight ?? 0f");
        Assert.IsFalse(poll.Contains("GetWeight("), poll);
    }

    [TestMethod]
    public void TheTick_ReadsTheCostScaleOncePerTick_AndRunsOnlyInBattleMode()
    {
        var ticker = Code("Main/Features/Stamina/Hooks/StaminaTicker.cs");
        Assert.AreEqual(1, Regex.Matches(ticker, @"_runtime\.Settings\.CostScale").Count, "the MCM value is read once per tick, never per soldier");

        var logic = Code("Main/Features/Stamina/Hooks/StaminaMissionLogic.cs");
        // Nothing drains during deployment: the mode is asked every tick, so the gate is not latched shut by it.
        StringAssert.Contains(logic, "if (!(_armed ??= DecideArmed()) || Mission.Mode != MissionMode.Battle) return;");
    }

    [TestMethod]
    public void TheHitCallbacks_LookTheDefenderUpBeforeTheyReadTheBlow_AndOnlyAddToHisCell()
    {
        var logic = Code("Main/Features/Stamina/Hooks/StaminaMissionLogic.cs");
        var melee = logic.Substring(At(logic, "public override void OnMeleeHit("));

        Assert.IsTrue(At(melee, "!_runtime.Store.TryGet(victim, out var cell)") < At(melee, "collisionData.CollisionResult"));
        StringAssert.Contains(melee, "if (result != CombatCollisionResult.Blocked && result != CombatCollisionResult.Parried) return;");
        StringAssert.Contains(logic, "if (!_runtime.Armed || victim == null || !collisionData.AttackBlockedWithShield || !_runtime.Store.TryGet(victim, out var cell)) return;");
        // The callbacks may run off the main thread: one atomic add, and the tick applies it.
        StringAssert.Contains(logic, "cell.AddPending(StaminaService.BlockCost(_runtime.Tuning.Economy, block, shieldDamage));");
        // A stopped blow is charged in OnMeleeHit or OnMissileHit only, never also in OnAgentHit or OnScoreHit.
        Assert.IsFalse(Regex.IsMatch(logic, @"override void On(AgentHit|ScoreHit)\("), "a second hit callback would charge a blow twice");
    }

    [TestMethod]
    public void BothPlayerControllers_StripAtTheEndOfTheirInputBuild()
    {
        var patch = Code("Client/Features/Stamina/Hooks/Patch171_StaminaPlayerInput.cs");
        StringAssert.Contains(patch, "[HarmonyPatch(typeof(MissionMainAgentController), \"ControlTick\")] [HarmonyPatchCategory(StaminaModule.PatchCategory)]");
        StringAssert.Contains(patch, "[HarmonyPostfix] public static void Postfix(MissionMainAgentController __instance) => StaminaHooks.StripPlayerInput(__instance.Mission?.MainAgent);");

        // TAOM's own controller replaces vanilla's during a creature ride, so the patch does not run then.
        var own = Code("Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs");
        StringAssert.Contains(own, "ProcessCombatControls(mainAgent); ProcessActionControls(mainAgent); StaminaHooks.StripPlayerInput(mainAgent); }");
    }

    [TestMethod]
    public void TheHooks_LookTheSoldierUpBeforeAnyEngineRead_AndStripOnlyThePlayerLockPath()
    {
        var hooks = Code("Main/Features/Stamina/Hooks/StaminaHooks.cs");
        var apply = hooks.Substring(At(hooks, "public static void ApplyStats("), At(hooks, "public static void StripPlayerInput(") - At(hooks, "public static void ApplyStats("));
        Assert.IsTrue(At(apply, "runtime.Store.TryGet(agent, out var cell)") < At(apply, "agent.HasMount"));

        var strip = hooks.Substring(At(hooks, "public static void StripPlayerInput("));
        StringAssert.Contains(strip, "if (!runtime.Store.TryGet(agent, out var cell) || cell.AppliedPath != StaminaLockPath.PlayerInput) return;");
        Assert.IsTrue(At(strip, "cell.AppliedPath != StaminaLockPath.PlayerInput") < At(strip, "agent.MovementFlags"));
    }
}
