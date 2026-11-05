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
