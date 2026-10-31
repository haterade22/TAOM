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
    [DataRow("FordsOfWarMissionViews")]
    [DataRow("FordsCommanderCameraView")]
    [DataRow("FordsCommandView")]
    [DataRow("FordsCommandVM")]
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
    public void TheViewList_HasTheEscapeMenuTheBoundaryWallAndBothFordsViews()
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
    [DataRow("CreateMissionAgentStatusUIHandler")]
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
    public void TheClientSource_NeverReadsOrSpawnsAMainAgent()
    {
        foreach (var file in new[] { "FordsCommanderCameraView.cs", "FordsCommandView.cs", "FordsCommandVM.cs", "FordsOfWarGameManager.cs" })
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
        foreach (var type in new[] { typeof(FordsCommanderCameraView), typeof(FordsCommandView) })
        {
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), type.Name + " is built with new() by the view list");
            Assert.AreEqual(0, type.GetCustomAttributesData().Count(a => a.AttributeType.Name == "DefaultView"),
                type.Name + " is built by the [ViewMethod] list; a [DefaultView] would build a second copy");
        }
    }
}
