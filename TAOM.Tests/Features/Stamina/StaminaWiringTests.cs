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
        var visit = ticker.Substring(At(ticker, "private void Visit(Agent agent, StaminaCell cell)"));
        var owned = At(visit, "var owned = CoopMissionOwnership.IsOwnedHere(agent);");
        // A soldier another peer owns reads Fresh, so every write after this line gives him back what stamina took.
        StringAssert.Contains(visit, "var band = owned ? cell.Forced ?? StaminaBand.Fresh : StaminaBand.Fresh;");
        Assert.IsTrue(owned < At(visit, "cell.Publish("), "ownership before the band is published");
        Assert.IsTrue(owned < At(visit, "HoldAiStrip(agent);"), "ownership before the AI input component and its callback claim");
        Assert.IsTrue(At(visit, "if (cell.Removed || !IsLive(agent)) return;") < owned, "a dead agent's handle is refused before any other read");

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
        var visit = ticker.Substring(At(ticker, "private void Visit(Agent agent, StaminaCell cell)"));
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
