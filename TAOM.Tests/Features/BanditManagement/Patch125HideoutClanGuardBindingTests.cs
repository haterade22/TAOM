using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// Drift-guards for Patch125 (#1063). Both prefixes bind engine parameters by NAME and one injects the private
/// <c>_hideouts</c> field, so a rename on an engine bump makes the guard a silent no-op or fails the whole category.
/// The decisions are covered by <c>HideoutClanGuardTests</c>.
/// </summary>
[TestClass]
public class Patch125HideoutClanGuardBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Behavior()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.BanditSpawnCampaignBehavior");
        Assert.IsNotNull(type, "BanditSpawnCampaignBehavior did not resolve.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetInfestedHideoutCount_ResolvesPrivateInstance_WithNamedClanParameter()
    {
        var method = Behavior().GetMethod("GetInfestedHideoutCount", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "GetInfestedHideoutCount is gone: Patch125 cannot guard the new-game hideout count.");
        Assert.AreEqual(typeof(int), method.ReturnType);
        CollectionAssert.AreEqual(new[] { "banditFaction" }, method.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual("TaleWorlds.CampaignSystem.Clan", method.GetParameters()[0].ParameterType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HideoutsField_IsDictionaryOfCultureToHideoutList()
    {
        var field = Behavior().GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "_hideouts is gone: injecting ____hideouts fails the whole Patch125 category.");
        var args = field.FieldType.GetGenericArguments();
        Assert.AreEqual(typeof(Dictionary<,>), field.FieldType.GetGenericTypeDefinition());
        Assert.AreEqual("TaleWorlds.CampaignSystem.CultureObject", args[0].FullName);
        Assert.AreEqual("TaleWorlds.CampaignSystem.Settlements.Hideout", args[1].GetGenericArguments()[0].FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AddBanditToHideout_ResolvesPublic_WithNamedHideoutParameter()
    {
        var method = Behavior().GetMethod("AddBanditToHideout", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, "AddBanditToHideout is gone: Patch125 cannot guard a clanless hideout.");
        Assert.AreEqual("TaleWorlds.CampaignSystem.Party.MobileParty", method.ReturnType.FullName);
        CollectionAssert.AreEqual(new[] { "hideoutComponent", "overridenPartyTemplate", "isBanditBossParty" },
            method.GetParameters().Select(p => p.Name).ToArray());
    }
}
