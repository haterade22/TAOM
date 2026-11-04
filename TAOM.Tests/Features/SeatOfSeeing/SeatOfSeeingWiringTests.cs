using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The Seat's wiring: the layer rule (the Domain folder names no engine type), the module's declarations, what the container
/// resolves, and the source-text gates of the view, the adapters and the patches (their classes are entry points or need a live
/// mission).
/// </summary>
[TestClass]
public class SeatOfSeeingWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new SeatOfSeeingModule().RegisterServices(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        return container;
    }

    // ---- The layer rule of build-spec section 2 ----

    [TestMethod]
    public void Domain_HoldsNoTaleWorldsReference()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "SeatOfSeeing", "Domain"), "*.cs");
        Assert.IsTrue(files.Length >= 8, "the Domain folder holds the pure rules; the scan found only " + files.Length);

        foreach (var file in files)
        {
            var code = RepoPaths.StripComments(File.ReadAllText(file));
            Assert.IsFalse(Regex.IsMatch(code, @"\busing\s+TaleWorlds"), Path.GetFileName(file) + " has a using TaleWorlds: the Domain is BCL only");
            Assert.IsFalse(Regex.IsMatch(code, @"(?<!"")\bTaleWorlds\."),Path.GetFileName(file) + " names a TaleWorlds type: the Domain is BCL only");
            Assert.IsFalse(Regex.IsMatch(code, @"\busing\s+(SandBox|HarmonyLib|TAOM\.Adapters)"), Path.GetFileName(file) + " reaches past the BCL");
        }
    }

    // ---- The module ----

    [TestMethod]
    public void FeatureModules_ListTheSeatModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<SeatOfSeeingModule>().Count());
        StringAssert.Contains(RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true),
            "new Features.SeatOfSeeing.SeatOfSeeingModule()");
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndAddsNoMissionBehavior()
    {
        var module = new SeatOfSeeingModule();

        Assert.AreEqual("SeatOfSeeing", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.MissionBehaviors.Count, "the camera is a [DefaultView] in TAOM.Client, built by the engine");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void TheModule_AppliesPatch140AtGameInit()
    {
        var decls = new SeatOfSeeingModule().PatchCategories;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch140_SeatOfSeeing", decls[0].Category);
        Assert.AreEqual("Patch140_SeatOfSeeing", SeatOfSeeingModule.PatchCategory);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("SeatOfSeeing"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("SeatOfSeeing"));
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsInstanceOfType(container.Resolve<ISeatSettingsProvider>(), typeof(SeatSettingsProvider));
        Assert.IsInstanceOfType(container.Resolve<ISeatHeroAdapter>(), typeof(SeatHeroAdapter));
        Assert.IsNotNull(container.Resolve<SeatHotKeys>());
    }

    [TestMethod]
    public void TheServices_AreSingletons_BecauseTheAdaptersHoldNoState()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<ISeatHeroAdapter>(), container.Resolve<ISeatHeroAdapter>());
        Assert.AreSame(container.Resolve<SeatHotKeys>(), container.Resolve<SeatHotKeys>());
        Assert.AreSame(container.Resolve<ISeatSettingsProvider>(), container.Resolve<ISeatSettingsProvider>());
    }

    [TestMethod]
    public void NoScreenAdapterIsRegistered_ItIsBuiltPerMissionByTheView()
    {
        // No core code calls the screen adapter and TAOM.Client does not load on a server, so core has no null for it (build-spec 5.1).
        using var container = BuildContainer();

        Assert.AreEqual(0, container.GetServiceRegistrations().Count(r => r.ServiceType.Name == "ISeatScreenAdapter"));
        StringAssert.Contains(RepoPaths.ReadSource("Client/Features/SeatOfSeeing/SeatView.cs", stripComments: true),
            "new SeatScreenAdapter(MissionScreen, mission)");
    }

    // ---- The view ----

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void SeatView_IsADefaultView_OfTheMissionViewBase()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // By name: the test project does not reference TaleWorlds.MountAndBlade.View, which the game's Native module holds.
        var view = typeof(TAOM.Client.ClientSubModule).Assembly.GetType("TAOM.Client.Features.SeatOfSeeing.SeatView");
        Assert.IsNotNull(view, "SeatView must live in TAOM.Client");
        Assert.AreEqual(1, view!.GetCustomAttributesData().Count(a => a.AttributeType.Name == "DefaultView"),
            "a view TAOM adds in OnMissionBehaviorInitialize never registers and never ticks (#970)");
        var bases = BaseChain(view).Select(t => t.Name).ToList();
        CollectionAssert.Contains(bases, "MissionView");
        Assert.AreEqual(0, view.GetConstructors().Count(c => c.GetParameters().Length > 0), "the engine builds a default view with no arguments");
    }

    [TestMethod]
    public void TheView_ReadsTheKeyInTheScreenTick_NeverInTheMissionTick()
    {
        var code = RepoPaths.ReadSource("Client/Features/SeatOfSeeing/SeatView.cs", stripComments: true);

        var screenTick = Slice(code, "OnMissionScreenTick");
        StringAssert.Contains(screenTick, "IsKeyPressed");
        Assert.IsFalse(Slice(code, "OnMissionTick").Contains("IsKeyPressed"),
            "fast time repeats OnMissionTick several times in one frame (MissionState.cs:171-198)");
        Assert.IsFalse(code.Contains("Input.IsKeyDown"), "flight input is read through the screen adapter in UpdateOverridenCamera");
    }

    [TestMethod]
    public void TheViews_KeyTest_IsTheCheapStaticTestFirst_ThenTheContextsOwn()
    {
        // The context's test allocates a click key array on each call (InputContext.cs:310-312) and is the one that goes quiet behind a
        // menu, so the static test (cheap) comes first and the context's second (build-spec 3).
        StringAssert.Contains(ViewMember("public override void OnMissionScreenTick(float dt)"),
            "key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyPressed(key) && Input.IsKeyPressed(key)");
    }

    [TestMethod]
    public void TheView_DrivesTheSessionFromTheFrameAndTheMissionTick()
    {
        // The field trial's input (build-spec section 12) rides along, null in every session that is not armed.
        StringAssert.Contains(ViewMember("public override bool UpdateOverridenCamera(float dt)"), "_session != null && _session.OnFrame(dt, TrialInput)");
        StringAssert.Contains(ViewMember("public override void OnMissionTick(float dt)"), "base.OnMissionTick(dt); _session?.OnMissionTick();");
    }

    // ---- Teardown has two paths, and the deployment edge comes from the engine (5.3) ----

    private static string ViewMember(string signature)
    {
        var code = RepoPaths.ReadSource("Client/Features/SeatOfSeeing/SeatView.cs", stripComments: true);
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SeatView has no " + signature);
        var next = code.IndexOf("public override", start + signature.Length, StringComparison.Ordinal);
        return Regex.Replace(next < 0 ? code.Substring(start) : code.Substring(start, next - start), @"\s+", " ");
    }

    [TestMethod]
    public void TheView_EndsTheSessionFromBothTeardownPaths_AndKeepsTheBaseCalls()
    {
        // MissionState.OnFinalize reaches RemoveMissionBehavior with no EndMission, and MissionView overrides OnRemoveBehavior
        // (MissionView.cs:119), so the base call stays.
        StringAssert.Contains(ViewMember("public override void OnMissionScreenFinalize()"), "base.OnMissionScreenFinalize(); EndSession();");
        StringAssert.Contains(ViewMember("public override void OnRemoveBehavior()"), "base.OnRemoveBehavior(); EndSession();");
    }

    [TestMethod]
    public void TheView_EndingTheSession_IsTerminal_AndRemovesTheMainAgentHandler()
    {
        // RemoveMissionBehavior does not take the view out of the screen's collection (MissionScreen.cs:3688), so a screen tick can
        // still run after it and must not re-enter (build-spec 5.3).
        StringAssert.Contains(ViewMember("private void EndSession()"),
            "_session?.Finish(); _session = null; if (Mission != null) Mission.OnMainAgentChanged -= OnMainAgentChanged; }");
    }

    [TestMethod]
    public void TheView_SubscribesToTheMainAgentEventOnce_RightAfterBuildingTheSession()
    {
        var init = ViewMember("public override void OnMissionScreenInitialize()");
        var built = init.IndexOf("_session = new SeatSession(", StringComparison.Ordinal);

        Assert.IsTrue(built >= 0);
        Assert.IsTrue(init.IndexOf("mission.OnMainAgentChanged += OnMainAgentChanged;", StringComparison.Ordinal) > built);
        Assert.AreEqual(1, Regex.Matches(init, @"OnMainAgentChanged \+=").Count);
        StringAssert.Contains(ViewMember("private void OnMainAgentChanged(Agent oldAgent)"),
            "private void OnMainAgentChanged(Agent oldAgent) => _session?.OnMainAgentChanged();");
    }

    [TestMethod]
    public void TheView_TakesTheDeploymentEdgeFromOnMissionModeChange()
    {
        var change = ViewMember("public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)");

        StringAssert.Contains(change, "base.OnMissionModeChange(oldMissionMode, atStart);");
        StringAssert.Contains(change, "oldMissionMode == MissionMode.Deployment");
        StringAssert.Contains(change, "Mission.Mode == MissionMode.Battle");
        StringAssert.Contains(change, "_session?.OnDeploymentEnded();");
    }

    [TestMethod]
    public void TheHeroAdapter_MarksItsWriteForTheThreadGuard_AndWritesTheFourStandFastFields()
    {
        var squashed = Regex.Replace(RepoPaths.ReadSource("Main/Adapters/SeatHeroAdapter.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(squashed, "MissionThreadGuard.NoteCall(\"SeatHeroAdapter.StandFast\", _logger.LogError);");
        StringAssert.Contains(squashed, "if (agent == null || !agent.IsActive()) return;");
        StringAssert.Contains(squashed,
            "agent.MovementFlags = default; agent.MovementInputVector = Vec2.Zero; agent.EventControlFlags = default; agent.HeadCameraMode = false;");
    }

    [TestMethod]
    public void TheView_DoesNotCallUpdateFreeCamera()
    {
        // UpdateFreeCamera writes CameraElevation with the opposite sign of UpdateCamera's, and the return glide reads it (5.4).
        foreach (var path in new[] { "Client/Adapters/SeatScreenAdapter.cs", "Client/Features/SeatOfSeeing/SeatView.cs", "Client/Features/SeatOfSeeing/SeatSession.cs" })
            Assert.IsFalse(RepoPaths.ReadSource(path, stripComments: true).Contains("UpdateFreeCamera"), path);
    }

    [TestMethod]
    public void TheHeroController_IsShutThroughDisable_NotIsDisabled()
    {
        // UpdateCamera overwrites IsDisabled each frame it runs, so the Seat must stay safe on any frame that returns false (5.5).
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);

        StringAssert.Contains(code, ".Disable()");
        StringAssert.Contains(code, ".Enable()");
        Assert.IsFalse(code.Contains("IsDisabled"));
    }

    // ---- The frame keeps the view sharp (5.4 step 5) ----

    private static string ApplyFrameBody()
    {
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);
        var start = code.IndexOf("public void ApplyFrame(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SeatScreenAdapter has no ApplyFrame");
        var end = code.IndexOf("public void SetHeroController(", start, StringComparison.Ordinal);
        return Regex.Replace(code.Substring(start, end - start), @"\s+", " ");
    }

    [TestMethod]
    public void ApplyFrame_EndsWithTheTwoSceneCallsOfTheTailOfUpdateCamera()
    {
        // MissionScreen.cs:2354-2355 (v1.5.6): the tail of UpdateCamera, which a Seat frame skips. Without them the view stays blurred.
        var body = ApplyFrameBody();

        StringAssert.Contains(body, "scene.SetDepthOfFieldParameters(0f, 0f, false); scene.SetDepthOfFieldFocus(0f); }",
            "the two calls close the method, with the engine's own arguments");
        Assert.IsTrue(body.IndexOf("SetCameraFrame", StringComparison.Ordinal) < body.IndexOf("SetDepthOfFieldParameters", StringComparison.Ordinal),
            "they come after the camera frame");
        Assert.IsTrue(body.IndexOf("SetDepthOfFieldParameters", StringComparison.Ordinal) < body.IndexOf("SetDepthOfFieldFocus", StringComparison.Ordinal));
    }

    // ---- The ground (5.4) ----

    [TestMethod]
    public void GroundHeight_UsesTheEnginesNamedMask()
    {
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);

        StringAssert.Contains(code, "BodyFlags.CommonCollisionExcludeFlags");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheEnginesGroundMask_IsTheValueMissionScreenUsesForItsFreeCamera()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // MissionScreen.cs:1762 and 1885 cast this literal to BodyFlags. If the named flag drifts, the Seat's ground drifts with it.
        Assert.AreEqual(544321929u, (uint)TaleWorlds.Engine.BodyFlags.CommonCollisionExcludeFlags);
    }

    [TestMethod]
    public void ReadHeightLimit_ReadsTheTaggedEntitysZ_OnceAtEntry()
    {
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);
        var squashed = Regex.Replace(code, @"\s+", " ");

        StringAssert.Contains(squashed, "private const string HeightLimiterTag = \"camera_height_limiter\";");
        StringAssert.Contains(squashed, "_mission.Scene?.FindEntityWithTag(HeightLimiterTag)");
        StringAssert.Contains(squashed, "tag.GetGlobalFrame().origin.z : 0f");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void SceneDepthOfFieldCalls_HaveTheSignaturesApplyFrameUses()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var scene = typeof(TaleWorlds.Engine.Scene);
        var parameters = scene.GetMethod("SetDepthOfFieldParameters", new[] { typeof(float), typeof(float), typeof(bool) });
        var focus = scene.GetMethod("SetDepthOfFieldFocus", new[] { typeof(float) });

        Assert.IsNotNull(parameters, "Scene.SetDepthOfFieldParameters(float, float, bool)");
        Assert.IsNotNull(focus, "Scene.SetDepthOfFieldFocus(float)");
    }

    // ---- Warg rage (5.5) ----

    [TestMethod]
    public void ReadFacts_AsksTheRageControllerThroughTheCoreInterface_WithACachedLookup()
    {
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);
        var squashed = Regex.Replace(code, @"\s+", " ");

        StringAssert.Contains(squashed, "_rage ??= _mission.GetMissionBehavior<IAutonomousMovementController>()");
        StringAssert.Contains(squashed, "_rage?.HoldsHero == true");
        StringAssert.Contains(squashed, "_rage?.HoldsHero == true && _mission.MainAgent?.MountAgent != null",
            "a rider who fell or dismounted during a rage is not held: the controller's own line 193 returns without a mount");
        Assert.IsFalse(code.Contains("AutonomousMovementPlayerController"), "core's interface is the seam (ADR-013)");
        Assert.IsFalse(code.Contains("_activated"), "that field defaults to true and only turns off at the first mission tick");
    }

    [TestMethod]
    public void TheFlightKeys_AreTheEnginesNamedGameKeys()
    {
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);

        foreach (var key in new[] { "GameKeyDefinition.Jump", "GameKeyDefinition.Crouch", "GameKeyDefinition.Zoom" })
            StringAssert.Contains(code, "(int)" + key);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheNamedGameKeys_AreTheNumbersTheContractNames()
    {
        // Build-spec 3: game keys 14 (Jump), 15 (Crouch) and 24 (Zoom), MissionScreen:1765, 1794-1801.
        Assert.AreEqual(14, (int)TaleWorlds.MountAndBlade.GameKeyDefinition.Jump);
        Assert.AreEqual(15, (int)TaleWorlds.MountAndBlade.GameKeyDefinition.Crouch);
        Assert.AreEqual(24, (int)TaleWorlds.MountAndBlade.GameKeyDefinition.Zoom);
    }

    [TestMethod]
    public void SetHeroController_True_DoesNothingWhileTheRageHolds()
    {
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);
        var start = code.IndexOf("public void SetHeroController(", StringComparison.Ordinal);
        var body = Regex.Replace(code.Substring(start, code.IndexOf("public bool WriteLook(", start, StringComparison.Ordinal) - start), @"\s+", " ");

        StringAssert.Contains(body, "if (enabled && RageHoldsHero()) return;");
        Assert.IsTrue(body.IndexOf("RageHoldsHero()", StringComparison.Ordinal) < body.IndexOf(".Enable()", StringComparison.Ordinal),
            "the guard comes before the controller is touched");
    }

    [TestMethod]
    public void BeginGlide_ReturnsBeforeTheListener_WithNoAgentToFollow()
    {
        // With no agent to follow, the listener's branch zeroes the bearing and the elevation (MissionScreen.cs:4576-4582).
        var code = RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs", stripComments: true);
        var start = code.IndexOf("public bool BeginGlide(", StringComparison.Ordinal);
        var body = code.Substring(start, code.IndexOf("public void Say(", start, StringComparison.Ordinal) - start);

        var guard = body.IndexOf("AgentToFollow == null", StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0, "the guard is there");
        Assert.IsTrue(guard < body.IndexOf("OnMissionModeChange", StringComparison.Ordinal), "the guard comes before the listener");
    }

    private static System.Collections.Generic.IEnumerable<Type> BaseChain(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
            yield return t;
    }

    private static string Slice(string code, string method)
    {
        var start = code.IndexOf("void " + method, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SeatView has no " + method);
        var next = code.IndexOf("public override", start + 10, StringComparison.Ordinal);
        return next < 0 ? code.Substring(start) : code.Substring(start, next - start);
    }
}
