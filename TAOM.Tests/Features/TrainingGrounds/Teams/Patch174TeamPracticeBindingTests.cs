using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.TrainingGrounds.Teams;
using TAOM.Features.TrainingGrounds.Teams.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TrainingGrounds.Teams;

/// <summary>
/// Patch174 (#1528): the four vanilla targets resolve to one method each with the shape the patches bind, and the two
/// private fields have the types the field references are declared with. <c>HarmonyPatchBindingTests</c> and
/// <c>ReflectionSiteBindingTests</c> already prove each name resolves; these pin what they do not: the parameters a
/// patch method binds by name, and that the target is not small enough for the JIT to inline (#1189).
/// </summary>
[TestClass]
public class Patch174TeamPracticeBindingTests
{
    private const string Controller = "SandBox.Missions.MissionLogics.Arena.ArenaPracticeFightMissionController";
    private const string Master = "SandBox.CampaignBehaviors.ArenaMasterCampaignBehavior";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo Declared(string typeName, string method)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(typeName);
        Assert.IsNotNull(type, typeName);
        var matches = type.GetMethods(Any).Where(m => m.Name == method).ToArray();
        Assert.AreEqual(1, matches.Length, $"{typeName}.{method} must resolve to exactly one method declared on the type (Patch174).");
        return matches[0];
    }

    private static string[] ParameterTypes(MethodInfo method) => method.GetParameters().Select(p => p.ParameterType.FullName!).ToArray();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ArrangePlayerTeamEnmity_IsPrivateInstanceVoidWithNoArguments()
    {
        var method = Declared(Controller, "ArrangePlayerTeamEnmity");

        Assert.IsTrue(method.IsPrivate && !method.IsStatic);
        Assert.AreEqual(typeof(void), method.ReturnType);
        Assert.AreEqual(0, method.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ArrangePlayerTeamEnmity_CannotBeInlined()
    {
        var body = Declared(Controller, "ArrangePlayerTeamEnmity").GetMethodBody()!;

        // The method holds a try/finally from its foreach, and the CLR does not inline a method with an exception region.
        Assert.IsTrue(body.ExceptionHandlingClauses.Count > 0,
            "ArrangePlayerTeamEnmity has no exception region any more: patch a method on the path the CLR cannot inline (lessons/harmony-il.md, #1189).");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnArenaAgent_TakesTeamAndFrame_AndReturnsAgent()
    {
        var method = Declared(Controller, "SpawnArenaAgent");

        CollectionAssert.AreEqual(new[] { "TaleWorlds.MountAndBlade.Team", "TaleWorlds.Library.MatrixFrame" }, ParameterTypes(method));
        Assert.AreEqual("team", method.GetParameters()[0].Name, "SpawnPostfix binds the team by this name");
        Assert.AreEqual("TaleWorlds.MountAndBlade.Agent", method.ReturnType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnMissionTick_IsDeclaredOnTheControllerWithOneFloat()
    {
        var method = Declared(Controller, "OnMissionTick");

        CollectionAssert.AreEqual(new[] { "System.Single" }, ParameterTypes(method));
        Assert.AreEqual(typeof(void), method.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PostFightCondition_IsPrivateInstanceBoolWithNoArguments()
    {
        var method = Declared(Master, "conversation_arena_master_post_fight_on_condition");

        Assert.IsTrue(method.IsPrivate && !method.IsStatic);
        Assert.AreEqual(typeof(bool), method.ReturnType);
        Assert.AreEqual(0, method.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ControllerFields_HaveTheTypesTheFieldReferencesAreDeclaredWith()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(Controller);

        Assert.AreEqual(typeof(List<TaleWorlds.MountAndBlade.Team>), AccessTools.Field(type, "_AIParticipantTeams").FieldType);
        Assert.AreEqual(typeof(int), AccessTools.Field(type, "_aliveOpponentCount").FieldType);
        Assert.IsTrue(TeamPracticeAccess.IsAvailable, "both field references resolved");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchMethods_BindOnlyParametersTheTargetsOrHarmonyProvide()
    {
        var patch = typeof(ArenaPracticeFightMissionController_TeamPractice_Patch);

        Assert.AreEqual("__instance", patch.GetMethod("ArrangeEnmityPostfix")!.GetParameters().Single().Name);
        CollectionAssert.AreEqual(new[] { "__instance", "team", "__result" },
            patch.GetMethod("SpawnPostfix")!.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual("__instance", patch.GetMethod("TickPrefix")!.GetParameters().Single().Name);
        Assert.AreEqual("__result", typeof(ArenaMasterCampaignBehavior_PostFightDebrief_Patch).GetMethod("Postfix")!.GetParameters().Single().Name);
    }

    // The patch methods name SandBox types, which this project does not reference, so they are called by reflection on a
    // controller built without its constructor.
    private static object UnbuiltController()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        return FormatterServices.GetUninitializedObject(AccessTools.TypeByName(Controller));
    }

    private static void Call(string method, object controller) =>
        typeof(ArenaPracticeFightMissionController_TeamPractice_Patch).GetMethod(method)!.Invoke(null, new[] { controller });

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ArrangeEnmityPostfix_NoCampaign_NeverThrowsAndDecidesNothing()
    {
        var controller = UnbuiltController();
        var service = new TeamPracticeService(
            Substitute.For<TAOM.Features.Execution.IAlignmentService>(), Substitute.For<ITeamPracticeSettingsProvider>(),
            Substitute.For<TAOM.Features.CoopInterop.ICoopSessionProvider>(), Substitute.For<TAOM.Features.CoopInterop.IDedicatedServerProvider>());
        ArenaPracticeFightMissionController_TeamPractice_Patch.Initialize(service, null);
        try
        {
            // The postfix runs inside AfterStart: whatever it meets (here no campaign, so no encounter), it must not throw.
            Call("ArrangeEnmityPostfix", controller);

            Assert.IsFalse(TeamPracticeState.IsTeams(controller));
        }
        finally
        {
            ArenaPracticeFightMissionController_TeamPractice_Patch.Initialize(null, null);
        }
    }
}
