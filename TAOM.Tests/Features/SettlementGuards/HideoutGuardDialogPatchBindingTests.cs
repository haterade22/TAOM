using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SettlementGuards;

/// <summary>
/// Drift-guards for the hideout guard-dialog suppression (#258 regression). The patch is manual because the three
/// targets are private; a rename makes the lookup null, which the applicator logs and survives, so the boss
/// fight would silently break again. Pins the literal method names, the parameterless bool signature, and the
/// wiring of the patch.
/// </summary>
[TestClass]
public class HideoutGuardDialogPatchBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [DataTestMethod]
    [DataRow("conversation_guard_start_on_condition")]
    [DataRow("conversation_disguised_start_on_condition")]
    [DataRow("conversation_disguised_start_on_condition_alt")]
    [TestCategory("BindingVerification")]
    public void GuardStartCondition_ResolvesPrivateInstance_ParameterlessBool(string name)
    {
        RequireGame();

        var type = AccessTools.TypeByName("SandBox.CampaignBehaviors.GuardsCampaignBehavior");
        Assert.IsNotNull(type, "GuardsCampaignBehavior did not resolve.");
        var method = AccessTools.Method(type, name);
        Assert.IsNotNull(method, $"GuardsCampaignBehavior.{name} did not resolve - the hideout guard-dialog suppression has no target.");
        Assert.IsFalse(method.IsStatic, name + " became static.");
        Assert.AreEqual(typeof(bool), method.ReturnType, name + " no longer returns bool - the postfix's ref bool __result would not bind.");
        Assert.AreEqual(0, method.GetParameters().Length, name + " gained parameters.");
        Assert.AreEqual(1, type.GetMethods(AccessTools.all).Count(m => m.Name == name), name + " gained an overload.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HideoutControllers_ResolveAsMissionLogics()
    {
        RequireGame();

        foreach (var name in new[]
        {
            "SandBox.Missions.MissionLogics.Hideout.HideoutMissionController",
            "SandBox.Missions.MissionLogics.Hideout.HideoutAmbushMissionController",
        })
            Assert.IsNotNull(AccessTools.TypeByName(name), name + " did not resolve - the patch's mission check would not compile or find it.");
    }

    [TestMethod]
    public void PatchClass_NamesThePinnedTargetsAndAsksBothControllers()
    {
        var src = RepoPaths.ReadSource("Main/Features/SettlementGuards/Hooks/GuardsCampaignBehavior_HideoutNoGuards_Patch.cs", stripComments: true);
        foreach (var name in new[] { "conversation_guard_start_on_condition", "conversation_disguised_start_on_condition", "conversation_disguised_start_on_condition_alt" })
            StringAssert.Contains(src, "\"" + name + "\"", name + " is no longer a patch target.");
        StringAssert.Contains(src, "GetMissionBehavior<HideoutMissionController>()");
        StringAssert.Contains(src, "GetMissionBehavior<HideoutAmbushMissionController>()");
        StringAssert.Contains(src, "HideoutGuardDialogRule.ApplyToResult(");
    }

    [TestMethod]
    public void Applicator_PatchesAllThreeTargetsWithThePostfixAndWarnsWhenMissing()
    {
        var src = RepoPaths.ReadSource("Main/ManualPatchApplicator.cs", stripComments: false);
        StringAssert.Contains(src, "GuardsCampaignBehavior_HideoutNoGuards_Patch.TargetMethods()");
        StringAssert.Contains(src, "nameof(GuardsCampaignBehavior_HideoutNoGuards_Patch.Postfix)");
        StringAssert.Contains(src, "[SettlementGuards] hideout guard-dialog suppression");
    }
}
