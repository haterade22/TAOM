using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Client;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Autorun;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar.Client;

/// <summary>
/// Wiring of the Fords of War client half (ADR-013, build-spec 6): the types live in TAOM.Client only, the factory replaces core's
/// null one, the view list is the cut-down no-main-agent list, and the camera and panel views carry the engine hooks the design
/// rests on. A CustomGameManager and a MissionView are never constructed here (side effects unverified), so the checks are
/// reflection and source scans.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsClientWiringTests
{
    private static readonly Assembly Client = typeof(ClientSubModule).Assembly;
    private static readonly Assembly Core = typeof(SubModule).Assembly;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    private static string Source(string file) => RepoPaths.ReadSource("Client/Features/FordsOfWar/" + file, stripComments: true);

    [DataTestMethod]
    [DataRow("FordsOfWarGameManager")]
    [DataRow("FordsOfWarGameManagerFactory")]
    [DataRow("FordsAutorunHost")]
    [DataRow("FordsOfWarMissionViews")]
    [DataRow("FordsCommanderCameraView")]
    [DataRow("FordsCommandView")]
    [DataRow("FordsCommandVM")]
    [DataRow("FordsControlView")]
    [DataRow("FordsPanelStandAside")]
    [DataRow("FordsPointerPick")]
    public void TheClientTypes_LiveInTaomClientOnly(string name)
    {
        var full = "TAOM.Client.Features.FordsOfWar." + name;

        Assert.IsNotNull(Client.GetType(full), full + " must be declared in TAOM.Client");
        Assert.IsNull(Core.GetType(full), full + " must not be declared in TAOM.dll");
    }

    [TestMethod]
    public void TheGameManager_IsACustomGameManager()
    {
        Assert.AreEqual("CustomGameManager", typeof(FordsOfWarGameManager).BaseType!.Name);
    }

    [TestMethod]
    public void TheFactory_ImplementsTheCoreInterface()
    {
        Assert.IsTrue(typeof(IFordsOfWarGameManagerFactory).IsAssignableFrom(typeof(FordsOfWarGameManagerFactory)));
    }

    [TestMethod]
    public void RegisterServices_ReplacesCoresNullFactoryWithTheClientOne()
    {
        var container = new Container();
        container.Register<IFordsOfWarGameManagerFactory, NullFordsOfWarGameManagerFactory>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IFordsMenuAdapter>());
        var report = new TAOM.Features.FordsOfWar.Diagnostics.FordsRunReport(Substitute.For<IModLogger>());
        container.RegisterInstance(report);
        container.RegisterInstance(new FordsMissionLauncher(
            new FordsConfigCatalog(Substitute.For<IPathService>(), Substitute.For<IModLogger>()), new FordsSession(), Substitute.For<IModLogger>(),
            report));
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IFordsOfWarGameManagerFactory>(), typeof(FordsOfWarGameManagerFactory));
    }

    [TestMethod]
    public void Apply_OverCoresNullAutorunHost_ResolvesTheClientHost()
    {
        var container = new Container();
        var logger = Substitute.For<IModLogger>();
        container.Register<IFordsAutorunHost, NullFordsAutorunHost>(Reuse.Singleton);
        container.RegisterInstance(logger);
        container.RegisterInstance(FordsAutorunPlan.Resolve(_ => "arm.txt", _ => true, _ => "out=E:\\out"));
        container.RegisterInstance(new FordsSetupPrompt(new FordsConfigCatalog(Substitute.For<IPathService>(), logger),
            Substitute.For<IFordsMenuAdapter>(), Substitute.For<IFordsOfWarGameManagerFactory>(), logger, new FordsRunReport(logger)));
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IFordsAutorunHost>(), typeof(FordsAutorunHost));
    }

    [TestMethod]
    public void AutorunHostSource_TakingItsPartsByConstructor_ResolvesNothingItselfAndHasNoDriverFactory()
    {
        var src = Source("FordsAutorunHost.cs");

        Assert.IsFalse(src.Contains("IoC."), "the host takes its plan, prompt and logger by constructor");
        Assert.IsFalse(src.Contains("CreateDriver"), "the driver is a core registration the client builds only for an armed plan");
    }

    [TestMethod]
    public void ClientSubModuleSource_AnArmedPlan_BuildsTheDriverOnlyThroughTheStartHelper()
    {
        var src = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);

        StringAssert.Contains(src, "FordsAutorunStart.Driver(IoC.Resolve<FordsAutorunPlan>(), IoC.Resolve<FordsAutorunDriver>, IoC.Resolve<IModLogger>())");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(src, "IoC\\.Resolve<FordsAutorunDriver>").Count);
        Assert.IsFalse(src.Contains("FordsAutorunHost"), "the submodule does not name the host");
    }

    [TestMethod]
    public void ClientSubModuleSource_AnAutorunFault_GuardsTheLoggerResolveInItsCatch()
    {
        var src = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);

        var catchAt = src.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
        var resolveAt = src.IndexOf("IoC.Resolve<IModLogger>().LogError", catchAt, StringComparison.Ordinal);
        var tryAt = src.IndexOf("try", catchAt, StringComparison.Ordinal);
        Assert.IsTrue(catchAt >= 0 && resolveAt > catchAt, "the catch logs the fault");
        Assert.IsTrue(tryAt > catchAt && tryAt < resolveAt, "a failing logger resolve must not escape the catch into the engine tick");
    }

    [TestMethod]
    public void TheGameManager_ShowsTheStartFailedNoticeAfterTheReturnToTheMenu()
    {
        var src = Source("FordsOfWarGameManager.cs");

        var load = src.IndexOf("public override void OnLoadFinished()", StringComparison.Ordinal);
        var back = src.IndexOf("ReturnToMainMenu(_logger);", load, StringComparison.Ordinal);
        var notice = src.IndexOf("FordsSetupPrompt.ShowStartFailed(_menu, _logger, _report);", load, StringComparison.Ordinal);
        Assert.IsTrue(load >= 0 && back > load && notice > back, "one tail serves a launch that returned false and one that threw");
    }

    [TestMethod]
    public void TheGameManager_ReportsTheLoadStageAndBothLoadFailuresAsRouteB()
    {
        var src = Source("FordsOfWarGameManager.cs");

        StringAssert.Contains(src, "_report.Stage(\"loadFinished\");");
        StringAssert.Contains(src, "_report.Failure(\"B\", \"the launcher opened no mission\");");
        StringAssert.Contains(src, "_report.Failure(\"B\", ex.GetType().Name + \": \" + ex.Message);");
    }

    [TestMethod]
    public void TheCommandView_ReportsThePanelThePauseAndTheExitButton()
    {
        var src = Source("FordsCommandView.cs");

        StringAssert.Contains(src, "_report?.PanelAttached();");
        StringAssert.Contains(src, "_report?.Paused(MBCommon.IsPaused);");
        StringAssert.Contains(src, "_report?.EndGameRequested(\"button\");");
        StringAssert.Contains(src, "new FordsCommandVM(_session!, ExitClicked, _report)");
    }

    [TestMethod]
    public void TheViewList_IsAViewCreatorModuleForTheFordsMission_WithOnlyViewMethodStatics()
    {
        var type = typeof(FordsOfWarMissionViews);

        Assert.IsTrue(type.GetCustomAttributesData().Any(a => a.AttributeType.Name == "ViewCreatorModule"));
        var statics = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.AreEqual(1, statics.Length);
        var attribute = statics[0].GetCustomAttributesData().Single(a => a.AttributeType.Name == "ViewMethod");
        Assert.AreEqual(FordsOfWarMission.MissionName, attribute.ConstructorArguments[0].Value);
    }

    [TestMethod]
    public void TheViewList_HasTheEscapeMenuTheBoundaryWallAndTheCameraAndPanelViews()
    {
        var src = Source("FordsOfWarMissionViews.cs");

        StringAssert.Contains(src, "ViewCreator.CreateMissionSingleplayerEscapeMenu(false)");
        StringAssert.Contains(src, "new MissionBoundaryWallView()");
        StringAssert.Contains(src, "new FordsCommanderCameraView()");
        StringAssert.Contains(src, "new FordsCommandView()");
    }

    [TestMethod]
    public void TheViewList_ListsTheOptionsHandlerOnlyWhenTheMissionHasTheOptionsComponent()
    {
        var src = Source("FordsOfWarMissionViews.cs");

        var guard = src.IndexOf("GetMissionBehavior<MissionOptionsComponent>() != null", StringComparison.Ordinal);
        var add = src.IndexOf("ViewCreator.CreateOptionsUIHandler()", StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0 && add > guard, "its OnMissionScreenInitialize dereferences the component unguarded");
    }

    [DataTestMethod]
    [DataRow("CreateMissionMainAgentEquipmentController")]
    [DataRow("CreateMissionMainAgentCheerBarkControllerView")]
    [DataRow("CreateMissionMainAgentEquipDropView")]
    [DataRow("CreateMissionOrderUIHandler")]
    [DataRow("CreateMissionAgentLockVisualizerView")]
    [DataRow("CreateMissionBoundaryCrossingView")]
    [DataRow("CreateMissionBattleScoreUIHandler")]
    [DataRow("OrderTroopPlacer")]
    [DataRow("DeploymentMissionView")]
    [DataRow("MissionCampaignView")]
    public void TheViewList_LeavesOutTheViewsThatNeedAMainAgent(string view)
    {
        Assert.IsFalse(Source("FordsOfWarMissionViews.cs").Contains(view), view + " reads the main agent or the campaign");
    }

    [TestMethod]
    public void TheViewList_CarriesTheControlView_AndTheHudOfAControlledSoldier()
    {
        var src = Source("FordsOfWarMissionViews.cs");
        StringAssert.Contains(src, "new FordsControlView()");
        StringAssert.Contains(src, "ViewCreator.CreateMissionAgentStatusUIHandler(mission)");
    }

    [TestMethod]
    public void TheControlKey_IsInTheControlView_AndNotInThePanelsKeyTable()
    {
        StringAssert.Contains(Source("FordsControlView.cs"), "InputKey.P");
        Assert.IsFalse(Source("FordsCommandHotkeys.cs").Contains("InputKey.P"), "the panel's table must leave the control key alone");
    }

    [TestMethod]
    public void TheCameraView_StandsAsideWhileASoldierIsControlled()
    {
        var src = Source("FordsCommanderCameraView.cs");
        var stand = src.IndexOf("FordsAgentControl.IsControlling(Mission)", StringComparison.Ordinal);
        var step = src.IndexOf("FordsCameraMath.Step", StringComparison.Ordinal);
        Assert.IsTrue(stand >= 0 && stand < step, "the engine's camera follows the soldier only when this view answers false");
    }

    [TestMethod]
    public void TheCommandView_WhileASoldierIsControlled_FreesTheMouse_HoldsTheSpeedAndReadsNoPanelKeys()
    {
        var aside = Source("FordsPanelStandAside.cs");
        StringAssert.Contains(aside, "FordsAgentControl.IsControlling(mission)");
        StringAssert.Contains(aside, "ResetInputRestrictions");
        StringAssert.Contains(aside, "_controlling ? _fightSpeed : vm.Speed");
        var view = Source("FordsCommandView.cs");
        StringAssert.Contains(view, "_standAside.Speed(_vm)");
        StringAssert.Contains(view, "!_vm.IsResultVisible && !controlling");
    }

    [TestMethod]
    public void TheControlView_ReturnsTheSoldier_WhenTheMatchIsOver_AndTakesNoOneThen()
    {
        var src = Source("FordsControlView.cs");
        StringAssert.Contains(src, "controlling && _session!.IsOver");
        StringAssert.Contains(src, "if (_session!.IsOver)");
        Assert.IsFalse(Source("FordsPanelStandAside.cs").Contains("FordsAgentControl.Return"), "a view tick overlaps the agent tick, so only the mission tick hands back");
    }

    [TestMethod]
    public void TheControlView_ReadsTheKeyOnTheScreenTick_AndChangesTheAgentOnlyOnTheMissionTick()
    {
        var src = Source("FordsControlView.cs");
        var screen = src.IndexOf("void OnMissionScreenTick", StringComparison.Ordinal);
        var key = src.IndexOf("InputKey.P", StringComparison.Ordinal);
        var mission = src.IndexOf("void OnMissionTick", StringComparison.Ordinal);
        var step = src.IndexOf("Step(", StringComparison.Ordinal);
        Assert.IsTrue(screen >= 0 && screen < key && key < mission && mission < step, "a fast forward frame ticks the mission many times, and a view tick overlaps the agent tick");
    }

    [TestMethod]
    public void ThePointerPick_KeepsANaNPixelOutOfThePick()
    {
        StringAssert.Contains(Source("FordsPointerPick.cs"), "!(w > 0f)");
    }

    [TestMethod]
    public void TheCameraView_UsesItsOwnViewAngle_BecauseAFightLeavesTheEnginesAngleZoomed()
    {
        Assert.IsFalse(Source("FordsCommanderCameraView.cs").Contains("CameraViewAngle"), "only UpdateCamera writes it, and it stops running when the fight ends");
    }

    [DataTestMethod]
    [DataRow("IsDetachableFromFormation", "seated howdah and mumak crew stay out: the seat teleports them each frame")]
    [DataRow("IsRunningAway", "a soldier in flight stays out")]
    [DataRow("IsFadingOut", "a soldier fading out stays out")]
    [DataRow("TrollBruteForceConfig.ActionSetsByMonster", "a troll stays out: its release and blocked clips have never run in a logged battle")]
    [DataRow("FordsControlPick.MayRide", "a creature's rider stays out: a behavior tree drives the creature")]
    [DataRow("SwitchMainAgentController(false)", "a warg's rage can leave vanilla's controller off, so a take turns it back on")]
    [DataRow("RefreshBehaviorValues", "a new AI component starts on the Default set until the lane's next order")]
    [DataRow("CommonAIComponent?.Initialize()", "AddComponent does not initialize, so morale would sit at a flat 50 with no recovery")]
    [DataRow("EventControlFlag.Stand", "only the player's controller stands a crouching soldier up")]
    public void TheAgentControl_CarriesEachRuleItsDocStates(string needed, string why)
    {
        StringAssert.Contains(RepoPaths.ReadSource("Main/Adapters/FordsAgentControl.cs", stripComments: true), needed, why);
    }

    [TestMethod]
    public void TheClientSource_ReachesTheMainAgentOnlyThroughFordsAgentControl()
    {
        foreach (var file in new[] { "FordsCommanderCameraView.cs", "FordsCommandView.cs", "FordsCommandVM.cs", "FordsOfWarGameManager.cs", "FordsControlView.cs", "FordsPanelStandAside.cs", "FordsPointerPick.cs" })
        {
            var src = Source(file);
            foreach (var banned in new[] { "MainAgent", "Agent.Main", "PlayerTroop", "AgentControllerType" })
                Assert.IsFalse(src.Contains(banned), file + " mentions " + banned);
        }
    }

    [TestMethod]
    public void TheCameraView_FeedsTheProbeEachFrame_AndAsksItWhetherPhotoModeIsVetoed()
    {
        var src = Source("FordsCommanderCameraView.cs");

        StringAssert.Contains(src, "Probe().Frame(");
        StringAssert.Contains(src, "Probe().PhotoVetoed()");
        StringAssert.Contains(src, "Probe().Failed()");
        Assert.AreEqual(1, src.Split(new[] { "_session.BoundaryPolygon" }, StringSplitOptions.None).Length - 1, "read once per frame, passed to Step and the probe");
    }

    [TestMethod]
    public void TheCameraView_OwnsTheFrameTheWayTheVanillaTailDoes()
    {
        var src = Source("FordsCommanderCameraView.cs");

        Assert.IsNotNull(typeof(FordsCommanderCameraView).GetMethod("UpdateOverridenCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        StringAssert.Contains(src, "SuppressSpectatorCyclingThisFrame()");
        StringAssert.Contains(src, "MissionScreen.UpdateFreeCamera(frame)");
        StringAssert.Contains(src, "SetFovVertical(");
        StringAssert.Contains(src, "MissionScreen.SceneView.SetCamera(camera)");
        StringAssert.Contains(src, "Mission.SetCameraFrame(ref frame");
        StringAssert.Contains(src, "FordsCameraMath.Step(");
        Assert.AreEqual(2, src.Split(new[] { "SuppressSpectatorCyclingThisFrame()" }, StringSplitOptions.None).Length - 1,
            "once in the tick and once in the camera update, every frame");
    }

    [TestMethod]
    public void TheCameraView_RefusesPhotoMode_BecauseNoViewHereCanTurnItOff()
    {
        // Built without its constructor: a MissionView is never constructed in the test host.
        var view = FormatterServices.GetUninitializedObject(typeof(FordsCommanderCameraView));
        typeof(FordsCommanderCameraView).GetField("_probe", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, new FordsCameraProbe(null, (_, _) => false));
        var allowed = typeof(FordsCommanderCameraView).GetMethod("IsPhotoModeAllowed", BindingFlags.Public | BindingFlags.Instance)!;

        Assert.IsFalse((bool)allowed.Invoke(view, null)!, "the escape menu would offer Photo Mode and nothing in the Fords view list ends it");
    }

    [TestMethod]
    public void TheCommandLayer_SitsAboveTheHudViewsAndBelowTheOptionsAndEscapeMenus()
    {
        // Vanilla mission HUD layers use 1 to 17 (orders, agent status, kill feed 13 to 17); the options handler is 49 and the escape
        // menu 50. Above 50 the panel stayed clickable and drawn over the paused menu.
        Assert.IsTrue(FordsCommandView.LayerOrder > 17 && FordsCommandView.LayerOrder < 49, "layer order " + FordsCommandView.LayerOrder);
        StringAssert.Contains(Source("FordsCommandView.cs"), "new GauntletLayer(\"GauntletLayer\", LayerOrder, false)");
    }

    [TestMethod]
    public void TheCommandView_ShowsTheCursorBeforeTheMovieLoads_AndGivesItBackOnDetach()
    {
        var src = Source("FordsCommandView.cs");

        var restrict = src.IndexOf("SetInputRestrictions(true, InputUsageMask.Mouse)", StringComparison.Ordinal);
        var load = src.IndexOf("LoadMovie(\"FordsCommand\"", StringComparison.Ordinal);
        Assert.IsTrue(restrict >= 0 && load > restrict, "a hidden cursor is pinned to the screen centre and the layer gets no clicks (#970)");
        StringAssert.Contains(src, "ResetInputRestrictions()");
        StringAssert.Contains(src, "ReleaseMovie(");
        StringAssert.Contains(src, "RemoveLayer(");
    }

    [TestMethod]
    public void TheCommandView_LoadsAMovieThatShipsInTheModule()
    {
        Assert.IsTrue(System.IO.File.Exists(RepoPaths.RepoPath("Main", "_Module", "GUI", "PreFabs", "FordsOfWar", "FordsCommand.xml")));
    }

    [TestMethod]
    public void TheViews_AreNotDefaultViewsAndHaveParameterlessConstructors()
    {
        foreach (var type in new[] { typeof(FordsCommanderCameraView), typeof(FordsCommandView), typeof(FordsControlView) })
        {
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), type.Name + " is built with new() by the view list");
            Assert.AreEqual(0, type.GetCustomAttributesData().Count(a => a.AttributeType.Name == "DefaultView"),
                type.Name + " is built by the [ViewMethod] list; a [DefaultView] would build a second copy");
        }
    }
}
