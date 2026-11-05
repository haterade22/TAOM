using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Drift-guards for every Patch66 target. SetNextMenu/EnterMenuMode are public (loud
/// breakage on drift), but the four LordConversationsCampaignBehavior conversation
/// conditions are matched BY NAME — a TaleWorlds rename makes the category silently apply
/// nothing and the enlisted player becomes recruitable into a second army mid-service.
/// Signature facts pinned here were verified against installed 1.4.7 (Phase 0.2):
/// the clickable condition carries an <c>out TextObject hint</c> parameter, and the two
/// ally-thanks conditions are PUBLIC while the join-army pair is private. Also pins the
/// battle-exit postfix target (<c>MapEventHelper.CanMainPartyLeaveBattleCommonCondition</c>,
/// public static bool) and its exact set of callers.
/// </summary>
[TestClass]
public class Patch66EnlistmentBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SetNextMenu_BindingResolves_WithStringParameter()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.GameMenus.GameMenuManager");
        Assert.IsNotNull(type, "GameMenuManager did not resolve — the menu-guard prefix has no target.");

        var method = AccessTools.Method(type, "SetNextMenu");
        Assert.IsNotNull(method, "GameMenuManager.SetNextMenu did not resolve — menu redirection would silently die.");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "SetNextMenu arity drifted — the `ref string name` prefix would not bind.");
        Assert.AreEqual("String", parameters[0].ParameterType.Name, "SetNextMenu no longer takes a string menu id.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CanMainPartyLeaveBattleCommonCondition_BindingResolves_PublicStaticParameterlessBool()
    {
        RequireGame();
        var type = AccessTools.TypeByName("Helpers.MapEventHelper");
        Assert.IsNotNull(type, "Helpers.MapEventHelper did not resolve; the battle-exit postfix has no target.");

        var method = AccessTools.Method(type, "CanMainPartyLeaveBattleCommonCondition");
        Assert.IsNotNull(method, "CanMainPartyLeaveBattleCommonCondition did not resolve; 'Leave' and 'Abandon army' would reappear for an enlisted soldier.");
        Assert.IsTrue(method.IsStatic, "CanMainPartyLeaveBattleCommonCondition is no longer static.");
        Assert.IsTrue(method.IsPublic, "CanMainPartyLeaveBattleCommonCondition is no longer public (the patch uses nameof).");
        Assert.AreEqual("Boolean", method.ReturnType.Name, "CanMainPartyLeaveBattleCommonCondition no longer returns bool.");
        Assert.AreEqual(0, method.GetParameters().Length, "CanMainPartyLeaveBattleCommonCondition arity drifted.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CanMainPartyLeaveBattleCommonCondition_Callers_AreExactlyTheThreeEncounterMenuConditions()
    {
        RequireGame();
        var type = AccessTools.TypeByName("Helpers.MapEventHelper");
        Assert.IsNotNull(type, "Helpers.MapEventHelper did not resolve.");

        // Scans TaleWorlds.CampaignSystem only (where all three known callers live); SandBox is not scanned.
        var callers = IlCallScanner.FindCallers(type.Assembly,
            m => m.Name == "CanMainPartyLeaveBattleCommonCondition" && m.DeclaringType == type,
            out var unreadable, out var scanned);

        Assert.AreEqual(0, unreadable.Count, "Unreadable bodies mean the caller set cannot be vouched for: " + string.Join("; ", unreadable));
        Assert.IsTrue(scanned > 100, "The IL scan walked too few bodies: " + scanned);

        const string menu = "TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior.";
        CollectionAssert.AreEquivalent(
            new[]
            {
                menu + "game_menu_encounter_leave_on_condition",
                menu + "game_menu_encounter_abandon_army_on_condition",
                menu + "game_menu_encounter_surrender_on_condition",
            },
            callers.Distinct().ToArray(),
            "The caller set of CanMainPartyLeaveBattleCommonCondition changed: a new caller is a new exit to audit for enlisted service.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OrderAttackCondition_BindingResolves_PrivateInstanceBoolWithOneMenuCallbackArgs()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior");
        Assert.IsNotNull(type, "EncounterGameMenuBehavior did not resolve; the sit-out postfix has no target.");

        // The patch targets this method by name (it is private), so a rename makes it apply to nothing.
        var method = AccessTools.Method(type, "game_menu_encounter_order_attack_on_condition");
        Assert.IsNotNull(method, "game_menu_encounter_order_attack_on_condition did not resolve; an enlisted soldier could sit a battle out again (#830).");
        Assert.IsFalse(method.IsStatic, "the condition is no longer an instance method.");
        Assert.IsFalse(method.IsPublic, "the condition is no longer private (informational pin, verified on v1.4.8: EncounterGameMenuBehavior.cs:1919).");
        Assert.AreEqual("Boolean", method.ReturnType.Name, "the condition no longer returns bool.");
        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "the condition's arity drifted.");
        Assert.AreEqual("MenuCallbackArgs", parameters[0].ParameterType.Name, "the condition no longer takes MenuCallbackArgs.");
        Assert.AreEqual("args", parameters[0].Name, "the postfix binds its `args` parameter by name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SitOutPostfix_IsRegisteredInTheEnlistmentCategoryAndTargetsTheCondition()
    {
        var patch = typeof(TAOM.Features.Enlistment.Hooks.EncounterGameMenuOrderAttackConditionPatch);

        var targets = patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Cast<HarmonyPatch>().ToList();
        Assert.AreEqual(1, targets.Count, "the sit-out postfix lost its [HarmonyPatch] target attribute.");
        Assert.AreEqual("game_menu_encounter_order_attack_on_condition", targets[0].info.methodName);
        Assert.AreEqual("EncounterGameMenuBehavior", targets[0].info.declaringType.Name);

        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .Select(c => c.info.category)
            .ToList();
        CollectionAssert.AreEqual(new[] { "Patch66_Enlistment" }, categories,
            "the sit-out postfix must carry exactly [HarmonyPatchCategory(\"Patch66_Enlistment\")] or SubModule's Patch66 call would not apply it.");

        var postfix = patch.GetMethod("Postfix");
        Assert.IsNotNull(postfix);
        CollectionAssert.AreEqual(new[] { "args", "__result" }, postfix.GetParameters().Select(p => p.Name).ToArray(),
            "Harmony binds the postfix parameters by name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void IsPlayerSimulation_HasAPublicGetter()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEvent");
        Assert.IsNotNull(type, "MapEvent did not resolve.");

        var property = type.GetProperty("IsPlayerSimulation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(property, "MapEvent.IsPlayerSimulation is no longer a public property; the sat-out payout reads it at MapEventEnded (#830).");
        Assert.AreEqual("Boolean", property.PropertyType.Name);
        Assert.IsNotNull(property.GetGetMethod(), "MapEvent.IsPlayerSimulation lost its public getter.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void InitSimulation_IsCalledOnlyFromEncounterOrderAttack_AndBattleSimulationOnlyFromInitSimulation()
    {
        RequireGame();
        var encounter = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter");
        var simulation = AccessTools.TypeByName("TaleWorlds.CampaignSystem.BattleSimulation");
        Assert.IsNotNull(encounter, "PlayerEncounter did not resolve.");
        Assert.IsNotNull(simulation, "BattleSimulation did not resolve.");

        // Scans TaleWorlds.CampaignSystem only. SandBox and the other official modules are NOT scanned,
        // so a caller added there would not fail this test. The payout reads MapEvent.IsPlayerSimulation
        // as "the player chose Send troops", which holds only while this is the single way in.
        var initCallers = IlCallScanner.FindCallers(encounter.Assembly,
            m => m.Name == "InitSimulation" && m.DeclaringType == encounter,
            out var unreadable1, out var scanned1);
        var ctorCallers = IlCallScanner.FindCallers(simulation.Assembly,
            m => m.IsConstructor && m.DeclaringType == simulation,
            out var unreadable2, out var scanned2);

        Assert.AreEqual(0, unreadable1.Count + unreadable2.Count, "Unreadable bodies mean the caller sets cannot be vouched for.");
        Assert.IsTrue(scanned1 > 100 && scanned2 > 100, "The IL scan walked too few bodies.");

        CollectionAssert.AreEquivalent(
            new[] { "Helpers.MenuHelper.EncounterOrderAttack" },
            initCallers.Distinct().ToArray(),
            "PlayerEncounter.InitSimulation gained a caller: a simulated player battle no longer means 'Send troops' (#830).");
        CollectionAssert.AreEquivalent(
            new[] { "TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.InitSimulation" },
            ctorCallers.Distinct().ToArray(),
            "BattleSimulation gained a constructor caller: IsPlayerSimulation may be set by a new path (#830).");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void IsPlayerSimulationSetter_IsCalledOnlyFromTheBattleSimulationConstructor()
    {
        RequireGame();
        var mapEvent = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEvent");
        Assert.IsNotNull(mapEvent, "MapEvent did not resolve.");

        // TaleWorlds.CampaignSystem only. The co-op module also writes this property from its own
        // code; that is outside this scan and is covered in the registry entry.
        var callers = IlCallScanner.FindCallers(mapEvent.Assembly,
            m => m.Name == "set_IsPlayerSimulation" && m.DeclaringType == mapEvent,
            out var unreadable, out var scanned);

        Assert.AreEqual(0, unreadable.Count, "Unreadable bodies mean the caller set cannot be vouched for.");
        Assert.IsTrue(scanned > 100, "The IL scan walked too few bodies: " + scanned);
        CollectionAssert.AreEquivalent(
            new[] { "TaleWorlds.CampaignSystem.BattleSimulation..ctor" },
            callers.Distinct().ToArray(),
            "MapEvent.IsPlayerSimulation gained a setter caller: a simulated player battle may no longer mean 'Send troops' (#830).");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void AttackCondition_CallsExactlyTheHonourCheckAndTheAttackCondition()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior");
        Assert.IsNotNull(type, "EncounterGameMenuBehavior did not resolve.");
        var method = AccessTools.Method(type, "game_menu_encounter_attack_on_condition");
        Assert.IsNotNull(method, "game_menu_encounter_attack_on_condition did not resolve; the Attack probe has nothing to mirror.");

        // The sit-out probe (GetSitOutSnapshot) rebuilds vanilla's whole Attack condition from these two
        // calls, and the #827 truce probe (IsAttackBarredByTruce) mirrors the first of them. A third
        // call here is a new condition the probes would not see.
        var il = method.GetMethodBody().GetILAsByteArray();
        var called = IlCallScanner.ExtractCalledMethods(method, il)
            .Select(m => m.DeclaringType.FullName + "." + m.Name)
            .Distinct()
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "Helpers.MenuHelper.CheckEnemyAttackableHonorably", "Helpers.MenuHelper.EncounterAttackCondition" },
            called,
            "vanilla's Attack condition changed shape: update IEncounterAdapter.GetSitOutSnapshot and IsAttackBarredByTruce.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BattleExitPostfix_IsRegisteredInTheEnlistmentCategory()
    {
        // A patch without its category attribute is dead code with no error, warning or log line.
        var patch = typeof(TAOM.Features.Enlistment.Hooks.MapEventHelperCanMainPartyLeaveBattlePatch);

        Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length,
            "The battle-exit postfix lost its [HarmonyPatch] target attribute.");

        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .Select(c => c.info.category)
            .ToList();
        CollectionAssert.AreEqual(new[] { "Patch66_Enlistment" }, categories,
            "The battle-exit postfix must carry exactly [HarmonyPatchCategory(\"Patch66_Enlistment\")] or SubModule's Patch66 call would not apply it.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EnterMenuMode_BindingResolves_Parameterless()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.GameState.MapState");
        Assert.IsNotNull(type, "MapState did not resolve.");

        var method = AccessTools.Method(type, "EnterMenuMode");
        Assert.IsNotNull(method, "MapState.EnterMenuMode did not resolve — the recovery postfix has no target.");
        Assert.AreEqual(0, method.GetParameters().Length, "EnterMenuMode arity drifted.");
    }

    [DataTestMethod]
    [TestCategory("BindingVerification")]
    [DataRow("conversation_lord_join_army_on_condition", 0, false)]
    [DataRow("conversation_lord_join_army_on_clickable_condition", 1, false)]
    [DataRow("conversation_ally_thanks_meet_after_helping_in_battle_on_condition", 0, true)]
    [DataRow("conversation_ally_thanks_after_helping_in_battle_on_condition", 0, true)]
    public void ConversationCondition_BindingResolves_BoolReturning(string methodName, int arity, bool isPublic)
    {
        RequireGame();
        var type = AccessTools.TypeByName(
            "TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior");
        Assert.IsNotNull(type, "LordConversationsCampaignBehavior did not resolve.");

        var method = AccessTools.Method(type, methodName);
        Assert.IsNotNull(
            method,
            $"{methodName} did not resolve — its Patch66 prefix would apply to nothing and the vanilla " +
            "line would reappear for an enlisted player.");
        Assert.AreEqual("Boolean", method.ReturnType.Name, $"{methodName} no longer returns bool.");
        Assert.AreEqual(arity, method.GetParameters().Length, $"{methodName} arity drifted.");
        Assert.AreEqual(isPublic, method.IsPublic,
            $"{methodName} visibility drifted (informational pin from the 1.4.7 verification).");

        if (arity == 1)
        {
            var p = method.GetParameters().Single();
            Assert.IsTrue(p.IsOut, $"{methodName}'s parameter is no longer `out` — the prefix signature must change.");
            Assert.AreEqual("TextObject&", p.ParameterType.Name, $"{methodName}'s hint parameter type drifted.");
        }
    }
}
