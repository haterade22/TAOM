using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.TrainingGrounds.Teams;
using TAOM.Features.TrainingGrounds.Teams.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TrainingGrounds.Teams;

[TestClass]
public class TeamPracticeWiringTests
{
    private static Container BuildContainer(TeamPracticeModule module)
    {
        var container = new Container();
        module.RegisterServices(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<TeamPracticeModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.TrainingGrounds.Teams.TeamPracticeModule()"));
    }

    [TestMethod]
    public void TheModule_OwnsNoSaveData_AndAppliesPatch174AtGameInit()
    {
        var module = new TeamPracticeModule();

        Assert.AreEqual("TeamPractice", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch174_TeamPractice", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, module.PatchCategories[0].Phase);
    }

    [TestMethod]
    public void TheModule_DeclaresTheDialogBehavior()
    {
        var module = new TeamPracticeModule();

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(TeamPracticeDialogBehavior), module.CampaignBehaviors[0].BehaviorType);
    }

    [TestMethod]
    public void BothPatchClasses_CarryTheModulesCategory()
    {
        foreach (var type in new[] { typeof(ArenaPracticeFightMissionController_TeamPractice_Patch), typeof(ArenaMasterCampaignBehavior_PostFightDebrief_Patch) })
        {
            var category = type.GetCustomAttributes<HarmonyPatchCategory>().Single().info.category;
            Assert.AreEqual(TeamPracticeModule.PatchCategory, category, type.Name);
        }
    }

    [TestMethod]
    public void TheModule_RegistersTheServiceAndItsSettingsProvider()
    {
        var container = BuildContainer(new TeamPracticeModule());

        Assert.IsNotNull(container.Resolve<TeamPracticeService>());
        Assert.IsInstanceOfType(container.Resolve<ITeamPracticeSettingsProvider>(), typeof(TeamPracticeSettingsProvider));
    }

    [TestMethod]
    public void InitializeStatics_ResolvesTheServiceAndLogger()
    {
        var module = new TeamPracticeModule();
        var container = BuildContainer(module);

        try
        {
            module.InitializeStatics(container);
        }
        finally
        {
            ArenaPracticeFightMissionController_TeamPractice_Patch.Initialize(null, null);
        }
    }

    [TestMethod]
    public void ModeDecision_GoesThroughTheRulesWithTheAccessFlag()
    {
        var src = RepoPaths.ReadSource("Main/Features/TrainingGrounds/Teams/Hooks/ArenaPracticeFightMissionController_TeamPractice_Patch.cs", stripComments: true);

        Assert.IsTrue(src.Contains("TeamPracticeRules.IsTeamMode(TeamPracticeAccess.IsAvailable, "),
            "the mode is not teams when the reflection access did not resolve (behaviour: TeamPracticeRulesTests.IsTeamMode_*)");
        Assert.IsTrue(src.Contains("TeamPracticeState.Decide(__instance, DecideTeamTown)"));
    }

    [TestMethod]
    public void DebriefPostfix_AsksTheRulesWithThePlayersStanding()
    {
        var src = RepoPaths.ReadSource("Main/Features/TrainingGrounds/Teams/Hooks/ArenaMasterCampaignBehavior_PostFightDebrief_Patch.cs", stripComments: true);

        Assert.IsTrue(src.Contains("TeamPracticeRules.ShowsTeamWin(TeamPracticeState.IsTeams(controller), controller.RemainingOpponentCountFromLastPractice, controller.IsPlayerSurvived)"),
            "the debrief reads the mode, vanilla's remaining count and vanilla's IsPlayerSurvived (behaviour: TeamPracticeRulesTests.ShowsTeamWin_*)");
    }

    [TestMethod]
    public void DialogLines_PriorityIsAboveVanillas()
    {
        Assert.IsTrue(TeamPracticeDialogBehavior.Priority > 100, "vanilla adds its arena lines at the default priority, 100");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void DialogLines_TokensAreTheOnesVanillaRegistersItsLinesOn()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // AddDialogLine(id, inputToken, outputToken, text, ...): three ldstr in a row. Read vanilla's, not ours.
        var vanilla = VanillaDialogStrings();
        AssertVanillaLine(vanilla, "arena_training_practice_fight_intro_1a", TeamPracticeDialogBehavior.RulesInput, TeamPracticeDialogBehavior.RulesOutput);
        AssertVanillaLine(vanilla, "arena_training_practice_fight_intro_reward", TeamPracticeDialogBehavior.RewardInput, TeamPracticeDialogBehavior.RewardOutput);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void RewardLine_VanillasConditionResolvesByName()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("SandBox.CampaignBehaviors.ArenaMasterCampaignBehavior");
        var method = AccessTools.Method(type, TeamPracticeDialogBehavior.VanillaRewardCondition);

        Assert.IsNotNull(method);
        Assert.IsTrue(method.IsStatic);
        Assert.AreEqual(typeof(bool), method.ReturnType);
        Assert.AreEqual(0, method.GetParameters().Length);
    }

    private static string[] VanillaDialogStrings()
    {
        var type = AccessTools.TypeByName("SandBox.CampaignBehaviors.ArenaMasterCampaignBehavior");
        var addDialogs = type.GetMethod("AddDialogs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(addDialogs, "ArenaMasterCampaignBehavior.AddDialogs");
        var il = addDialogs.GetMethodBody()!.GetILAsByteArray()!;
        var module = addDialogs.Module;
        var strings = new System.Collections.Generic.List<string>();
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x72)
                continue;
            try { strings.Add(module.ResolveString(BitConverter.ToInt32(il, i + 1))); }
            catch (ArgumentException) { /* not an ldstr token */ }
        }
        return strings.ToArray();
    }

    private static void AssertVanillaLine(string[] strings, string id, string input, string output)
    {
        var at = Array.IndexOf(strings, id);
        Assert.IsTrue(at >= 0 && at + 2 < strings.Length, $"vanilla registers no line '{id}'");
        Assert.AreEqual(input, strings[at + 1], $"input token of '{id}'");
        Assert.AreEqual(output, strings[at + 2], $"output token of '{id}'");
    }
}
