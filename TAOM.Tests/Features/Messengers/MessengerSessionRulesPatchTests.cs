using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// Patch117 (#898): a messenger conversation must not offer the two join-army lines, the board game, the clan member
/// troop and equipment inspections or the companion recall. The rule is a postfix filter that only ever turns true into false.
/// </summary>
[TestClass]
public class MessengerSessionRulesPatchTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static IMessengerConversationState State(bool open)
    {
        var state = Substitute.For<IMessengerConversationState>();
        state.IsOpen.Returns(open);
        return state;
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void Filter_ClosedConversation_LeavesTheResultAlone(bool vanilla, bool expected)
        => Assert.AreEqual(expected, MessengerSessionRulesPatch.Filter(vanilla, State(false)));

    [TestMethod]
    public void Filter_OpenConversation_ClearsATrueResult()
        => Assert.IsFalse(MessengerSessionRulesPatch.Filter(true, State(true)));

    [TestMethod]
    public void Filter_OpenConversation_NeverTurnsFalseIntoTrue()
        => Assert.IsFalse(MessengerSessionRulesPatch.Filter(false, State(true)));

    [TestMethod]
    public void Filter_WithNoState_FailsOpen()
        => Assert.IsTrue(MessengerSessionRulesPatch.Filter(true, null));

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void TargetMethods_ResolveEveryConditionInTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var targets = MessengerSessionRulesPatch.TargetMethods().Cast<MethodBase>().ToList();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "LordConversationsCampaignBehavior.conversation_lord_join_army_on_condition",
                "LordConversationsCampaignBehavior.player_ask_to_join_players_army_on_condition",
                "LordConversationsCampaignBehavior.conversation_clan_member_manage_troops_on_condition",
                "LordConversationsCampaignBehavior.conversation_clan_member_manage_inventory_on_condition",
                "CompanionRolesCampaignBehavior.companion_rejoin_after_emprisonment_role_on_condition",
                "BoardGameCampaignBehavior.conversation_lord_talk_game_on_condition",
            },
            targets.Select(t => t.DeclaringType!.Name + "." + t.Name).ToList());
        foreach (var t in targets)
        {
            Assert.AreEqual(typeof(bool), ((MethodInfo)t).ReturnType, t.Name + " must still return bool");
            Assert.AreEqual(0, t.GetParameters().Length, t.Name + " must still take no parameters");
            Assert.IsFalse(t.IsStatic, t.Name + " is an instance method");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_PostfixesEveryTargetAndTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.messengersessionrules.jit";
        var harmony = new Harmony(id);
        var targets = MessengerSessionRulesPatch.TargetMethods().Cast<MethodBase>().ToList();
        try
        {
            PatchCategoryIndex.Build(typeof(MessengerSessionRulesModule).Assembly).Apply(harmony, MessengerSessionRulesModule.PatchCategory);

            foreach (var target in targets)
            {
                var info = Harmony.GetPatchInfo(target);
                Assert.IsNotNull(info, target.Name + " was not patched");
                Assert.IsTrue(info!.Postfixes.Any(p => p.owner == id), target.Name + " has no TAOM postfix");
                Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id), "the rule must be a postfix only");
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target.MethodHandle);
            }
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    public void PatchClass_IsInTheLiteralCategory()
    {
        var categories = typeof(MessengerSessionRulesPatch).GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        CollectionAssert.AreEqual(new[] { "Patch117_MessengerSessionRules" }, categories);
    }
}
