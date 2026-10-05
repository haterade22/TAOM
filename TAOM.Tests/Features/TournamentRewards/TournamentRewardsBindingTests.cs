using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.TournamentRewards;
using TAOM.Features.TournamentRewards.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// Patch96's targets and the private members it reaches, against the installed engine. Harmony binds prefix
/// parameters by NAME, so the names are pinned as well as the types (lessons/harmony-il.md). Every check but one is
/// metadata-only reflection, so the class is part of the engine-bump binding gate (BindingVerification), and the
/// SandBox types it resolves by name need the game assemblies pre-loaded, as the sibling binding classes do. The
/// exception, the MbEvent order check, runs engine code and so carries RequiresGameIL as well.
/// </summary>
[TestClass]
public class TournamentRewardsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetMaximumBet_IsAParameterlessIntInstanceMethod()
    {
        RequireGame();

        var behavior = AccessTools.TypeByName("SandBox.Tournaments.MissionLogics.TournamentBehavior");
        Assert.IsNotNull(behavior, "SandBox.Tournaments.MissionLogics.TournamentBehavior");
        var method = AccessTools.Method(behavior, "GetMaximumBet");

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(int), method.ReturnType);
        Assert.AreEqual(0, method.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void JoinConsequence_ResolvesWithItsArgsParameter()
    {
        RequireGame();

        Assert.IsNotNull(Patch96_TournamentJoinChoices.Original);
        CollectionAssert.AreEqual(new[] { "args" }, Patch96_TournamentJoinChoices.Original!.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(MenuCallbackArgs), Patch96_TournamentJoinChoices.Original.GetParameters()[0].ParameterType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TournamentGamePrize_HasTheSetterTheAdapterInvokes()
    {
        RequireGame();

        Assert.IsNotNull(TournamentJoinAdapter.PrizeSetter);
        Assert.AreEqual(typeof(ItemObject), TournamentJoinAdapter.PrizeSetter!.GetParameters().Single().ParameterType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch96Classes_CarryTheModulesCategory()
    {
        RequireGame();

        foreach (var patch in new[] { typeof(Patch96_TournamentMaxBet), typeof(Patch96_TournamentJoinChoices) })
        {
            var categories = patch.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
            CollectionAssert.AreEqual(new[] { TournamentRewardsModule.PatchCategory }, categories,
                patch.Name + " must carry the module's category, or it is never applied");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MbEvent_RunsTheLastRegisteredListenerFirst()
    {
        RequireGame();

        // One link of the order TournamentRewardsBehavior relies on (its doc comment traces the rest through the
        // engine sources): its TournamentFinished listener must run before vanilla's, which registers first.
        var tournamentFinished = new MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject>();
        var order = new List<string>();
        tournamentFinished.AddNonSerializedListener("vanilla", (winner, participants, town, prize) => order.Add("vanilla"));
        tournamentFinished.AddNonSerializedListener("taom", (winner, participants, town, prize) => order.Add("taom"));

        tournamentFinished.Invoke(null!, null!, null!, null!);

        CollectionAssert.AreEqual(new[] { "taom", "vanilla" }, order);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BehaviorAndAdapterEngineMembers_Resolve()
    {
        RequireGame();

        // Members the behavior's handlers and the join adapter reference: a missing one fails at JIT, before the
        // body's own try can run.
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(Town), "Settlement"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(Settlement), "StringId") ?? AccessTools.PropertyGetter(typeof(TaleWorlds.ObjectSystem.MBObjectBase), "StringId"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(BasicCharacterObject), "IsHero"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(TournamentGame), "CreationTime"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(CampaignEvents), "PlayerEliminatedFromTournament"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(CampaignEvents), "TournamentFinished"));
    }
}
