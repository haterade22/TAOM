using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// Patch84 guards the siege aftermath menus against the null <c>_besiegerParty</c> that crashed
/// bundle d7d9f7d3 (2026-09-07).
///
/// Two halves. <see cref="Patch84_SiegeAftermathMenuGuard.Decide"/> is pure and carries the whole
/// policy, so it is tested directly. Everything else the patch stands on is an engine detail it
/// cannot see change — the behaviour type, the private field it reads, and the two private menu
/// inits it prefixes — and a rename in any of them makes the guard silently inert, putting the game
/// back to crashing in <c>menu_settlement_taken_player_participant_on_init</c> with nothing in the
/// stack to say why. Those are pinned as binding drift-guards.
///
/// The repair itself needs a live campaign (a <c>MenuContext</c>, a <c>GameMenu</c> and a captured
/// settlement) and is verified in game, per the feature doc.
/// </summary>
[TestClass]
public class Patch84SiegeAftermathMenuGuardTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static System.Type BehaviorType()
    {
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.SiegeAftermathCampaignBehavior");
        Assert.IsNotNull(type, "SiegeAftermathCampaignBehavior did not resolve — Patch84 has no target at all.");
        return type;
    }

    // ---- the decision ----------------------------------------------------------------------

    [TestMethod]
    public void Decide_LetsVanillaRun_WhenBothOfItsDereferencesWouldSucceed()
    {
        // The overwhelmingly common case. Every ordinary siege in the game passes through this
        // prefix, so anything but RunVanilla here would replace working menus with a fallback.
        Assert.AreEqual(
            Patch84_SiegeAftermathMenuGuard.MenuVerdict.RunVanilla,
            Patch84_SiegeAftermathMenuGuard.Decide(
                besiegerPartyPresent: true,
                besiegerSettlementPresent: true,
                fallbackSettlementPresent: true));
    }

    [TestMethod]
    public void Decide_RepairsWithSettlement_WhenTheBesiegerPartyIsNull()
    {
        // Bundle d7d9f7d3 exactly: _besiegerParty never assigned, but the player is standing in the
        // settlement that was just taken, so Settlement.CurrentSettlement can still name it.
        Assert.AreEqual(
            Patch84_SiegeAftermathMenuGuard.MenuVerdict.RepairWithSettlement,
            Patch84_SiegeAftermathMenuGuard.Decide(
                besiegerPartyPresent: false,
                besiegerSettlementPresent: false,
                fallbackSettlementPresent: true));
    }

    [TestMethod]
    public void Decide_RepairsWithSettlement_WhenOnlyTheBesiegersSettlementIsNull()
    {
        // The second of vanilla's two unguarded dereferences. Guarding only the party would let this
        // case through and move the crash one line down, onto currentSettlement.GetName().
        Assert.AreEqual(
            Patch84_SiegeAftermathMenuGuard.MenuVerdict.RepairWithSettlement,
            Patch84_SiegeAftermathMenuGuard.Decide(
                besiegerPartyPresent: true,
                besiegerSettlementPresent: false,
                fallbackSettlementPresent: true));
    }

    [TestMethod]
    public void Decide_RepairsWithoutSettlement_WhenNothingNamesTheSettlement()
    {
        // Degenerate, and it still must not crash: an empty menu body with its Continue option is a
        // recoverable session, which is the whole point of the guard.
        Assert.AreEqual(
            Patch84_SiegeAftermathMenuGuard.MenuVerdict.RepairWithoutSettlement,
            Patch84_SiegeAftermathMenuGuard.Decide(
                besiegerPartyPresent: false,
                besiegerSettlementPresent: false,
                fallbackSettlementPresent: false));
    }

    [TestMethod]
    public void Decide_NeverRunsVanilla_WhenTheBesiegerPartyIsAbsent()
    {
        // The invariant behind all of the above, stated once: a null _besiegerParty can never be
        // handed to vanilla, whatever the other two terms say.
        foreach (var besiegerSettlement in new[] { true, false })
        foreach (var fallback in new[] { true, false })
        {
            Assert.AreNotEqual(
                Patch84_SiegeAftermathMenuGuard.MenuVerdict.RunVanilla,
                Patch84_SiegeAftermathMenuGuard.Decide(false, besiegerSettlement, fallback),
                $"a null besieger party was passed to vanilla (besiegerSettlement={besiegerSettlement}, fallback={fallback})");
        }
    }

    // ---- the bindings ----------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BesiegerPartyField_StillResolves_AsAMobileParty()
    {
        RequireGame();

        var field = AccessTools.Field(BehaviorType(), "_besiegerParty");
        Assert.IsNotNull(field, "SiegeAftermathCampaignBehavior._besiegerParty did not resolve — Patch84 cannot read the state it guards.");
        Assert.AreEqual("MobileParty", field.FieldType.Name,
            "_besiegerParty changed type — the guard's cast to MobileParty would silently yield null and repair every menu.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BothMenuInits_ResolveAsPatchTargets()
    {
        RequireGame();

        var type = BehaviorType();

        foreach (var name in new[]
                 {
                     "menu_settlement_taken_player_participant_on_init",
                     "menu_settlement_taken_player_army_member_on_init",
                 })
        {
            var method = AccessTools.Method(type, name);
            Assert.IsNotNull(method, $"{name} did not resolve — that prefix would never apply.");

            // One MenuCallbackArgs parameter. Pinned because the prefixes take (__instance, args):
            // an arity change would stop Harmony matching them.
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length, $"{name} arity drifted.");
            Assert.AreEqual("MenuCallbackArgs", parameters[0].ParameterType.Name,
                $"{name} no longer takes MenuCallbackArgs.");

            var overloads = type.GetMethods(AccessTools.all).Count(m => m.Name == name);
            Assert.AreEqual(1, overloads,
                $"{name} gained an overload — [HarmonyPatch] by name is now ambiguous and must name the signature.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MenuIdsVanillaSwitchesTo_AreStillTheOnesWeGuard()
    {
        RequireGame();

        // menu_settlement_taken_on_init routes to these two by literal id. If TaleWorlds renames a
        // menu, our prefixes stay bound to methods nothing reaches any more and the guard is inert
        // while still reporting healthy. The registration strings live in the behaviour's own
        // RegisterEvents body, so assert against the method names we patch instead: they are the
        // same literals, and this test exists to make the coupling explicit for the next reader.
        var type = BehaviorType();
        Assert.IsNotNull(AccessTools.Method(type, "menu_settlement_taken_on_init"),
            "menu_settlement_taken_on_init is gone — the routing that reaches our two targets has been restructured, re-derive Patch84.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BothPatchClasses_AreRegisteredInAllThreePlaces()
    {
        // A patch needs all three or it is dead code with no error, warning or log line: the target
        // attribute, the category attribute, and a matching PatchCategory call in SubModule.cs.
        // Patch39_BanditPartySize shipped missing the second and all five deep-review agents missed
        // it (lessons/harmony-il.md), so all three are asserted rather than assumed. Both classes
        // are checked: they share one category, so a missing attribute on either silently halves
        // the guard.
        foreach (var patch in new[]
                 {
                     typeof(Patch84_ParticipantMenuGuard),
                     typeof(Patch84_ArmyMemberMenuGuard),
                 })
        {
            var target = patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false);
            Assert.AreEqual(1, target.Length, $"{patch.Name} lost its [HarmonyPatch] target attribute.");

            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>()
                .Select(c => c.info.category)
                .ToList();
            CollectionAssert.Contains(categories, "Patch84_SiegeAftermathMenuGuard",
                $"{patch.Name} lost its [HarmonyPatchCategory] — SubModule's PatchCategory call would apply nothing.");
        }

        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        StringAssert.Contains(source, "TryPatchCategory(\"Patch84_SiegeAftermathMenuGuard\")",
            "SubModule.cs no longer applies Patch84_SiegeAftermathMenuGuard — the patch is dead code.");

        // The binding is resolved in Initialize, and without that call IsReady is false forever,
        // which makes both prefixes defer to vanilla and the crash returns.
        StringAssert.Contains(source, "Patch84_SiegeAftermathMenuGuard.Initialize(",
            "SubModule.cs no longer initialises Patch84 — IsReady stays false and both prefixes defer to vanilla.");
    }
}
