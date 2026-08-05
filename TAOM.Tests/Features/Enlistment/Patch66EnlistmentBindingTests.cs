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
/// ally-thanks conditions are PUBLIC while the join-army pair is private.
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
