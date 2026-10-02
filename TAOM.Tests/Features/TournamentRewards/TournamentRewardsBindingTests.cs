using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.TournamentRewards.Hooks;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// Patch96's targets and the private members it reaches, against the installed engine. Harmony binds prefix
/// parameters by NAME, so the names are pinned as well as the types (lessons/harmony-il.md).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TournamentRewardsBindingTests
{
    [TestMethod]
    public void GetMaximumBet_IsAParameterlessIntInstanceMethod()
    {
        var behavior = AccessTools.TypeByName("SandBox.Tournaments.MissionLogics.TournamentBehavior");
        Assert.IsNotNull(behavior, "SandBox.Tournaments.MissionLogics.TournamentBehavior");
        var method = AccessTools.Method(behavior, "GetMaximumBet");

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(int), method.ReturnType);
        Assert.AreEqual(0, method.GetParameters().Length);
    }

    [TestMethod]
    public void OnTournamentFinished_HasTheParametersThePrefixBindsByName()
    {
        var method = AccessTools.Method(typeof(TournamentCampaignBehavior), "OnTournamentFinished");

        Assert.IsNotNull(method);
        CollectionAssert.AreEqual(new[] { "winner", "participants", "town", "prize" },
            method.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(CharacterObject), typeof(MBReadOnlyList<CharacterObject>), typeof(Town), typeof(ItemObject) },
            method.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    public void JoinConsequence_ResolvesWithItsArgsParameter()
    {
        Assert.IsNotNull(Patch96_TournamentJoinChoices.Original);
        CollectionAssert.AreEqual(new[] { "args" }, Patch96_TournamentJoinChoices.Original!.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(MenuCallbackArgs), Patch96_TournamentJoinChoices.Original.GetParameters()[0].ParameterType);
    }

    [TestMethod]
    public void TournamentGamePrize_HasTheSetterTheAdapterInvokes()
    {
        Assert.IsNotNull(TournamentJoinAdapter.PrizeSetter);
        Assert.AreEqual(typeof(ItemObject), TournamentJoinAdapter.PrizeSetter!.GetParameters().Single().ParameterType);
    }

    [TestMethod]
    public void PatchBodyEngineMembers_Resolve()
    {
        // Members the patch bodies reference: a missing one fails at JIT, before the body's own try can run.
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(Town), "Settlement"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(Settlement), "StringId") ?? AccessTools.PropertyGetter(typeof(TaleWorlds.ObjectSystem.MBObjectBase), "StringId"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(BasicCharacterObject), "IsHero"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(TournamentGame), "CreationTime"));
        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(CampaignEvents), "PlayerEliminatedFromTournament"));
    }
}
