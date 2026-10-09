using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Screens;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Dependencies.Foundation;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MapViewRelease;
using TAOM.Features.MapViewRelease.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapViewRelease;

/// <summary>
/// Pins what the map-view release and its adapter depend on in the installed engine (v1.5.4 at the time of writing): the
/// layer event the handler subscribes to and when the engine raises it, the scene layer and map screen members the release
/// reads and calls, and the property the early-install question turns on: only the adapter's one NoInlining method names
/// the map screen, so compiling the handler, the service or the rest of the adapter resolves no map type.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MapViewReleaseBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private const string MapScreenName = "SandBox.View.Map.MapScreen";

    // By name: the test project references the TaleWorlds assemblies only, not SandBox.View.
    private static Type MapScreenType()
    {
        var type = AccessTools.TypeByName(MapScreenName);
        Assert.IsNotNull(type, MapScreenName + " did not resolve.");
        return type;
    }

    private static bool ReferencesMapScreen(MethodBase method) =>
        PatchProcessor.GetOriginalInstructions(method).Any(ci =>
            ci.operand is MemberInfo member && (member.DeclaringType?.FullName == MapScreenName || member is Type t && t.FullName == MapScreenName));

    // ---- The layer event ----

    private static List<string> HandleBody(string method) =>
        PatchProcessor.GetOriginalInstructions(typeof(ScreenLayer).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!)
            .Select(ci => ci.operand is MemberInfo m ? m.Name : ci.opcode.Name!).ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LayerEvent_IsAPublicStaticActionOfScreenLayer_TheEngineRaisesOnActivationAndDeactivation()
    {
        RequireGame();

        var evt = typeof(ScreenLayer).GetEvent("OnLayerActiveStateChanged", BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(evt, "ScreenLayer.OnLayerActiveStateChanged did not resolve as a public static event.");
        Assert.AreEqual(typeof(Action<ScreenLayer>), evt.EventHandlerType);
        Assert.IsNotNull(evt.GetAddMethod(), "a handler can subscribe");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void LayerEvent_IsRaisedByHandleDeactivateAfterOnDeactivateTheActiveFlagAndTheFocusLoss_AndByHandleActivate()
    {
        RequireGame();

        // The handler reads layer.IsActive: false means the raise came from HandleDeactivate, and the layer's own OnDeactivate
        // (SceneLayer's, which the postfix used to follow) has run by then.
        var body = HandleBody("HandleDeactivate");
        var onDeactivate = body.IndexOf("OnDeactivate");
        var active = body.IndexOf("set_IsActive");
        var focus = body.IndexOf("TryLoseFocus");
        var raise = body.IndexOf("OnLayerActiveStateChanged");

        Assert.IsTrue(onDeactivate >= 0 && active > onDeactivate && focus > active && raise > focus, "HandleDeactivate: " + string.Join(", ", body));

        // Activation raises it too, and the handler picks the edge from IsActive, so HandleActivate must set the flag true before
        // the raise. Were it raised first, every return would read as a cover and nothing would release.
        var activateBody = HandleBody("HandleActivate");
        var activeOn = activateBody.IndexOf("set_IsActive");
        var activateRaise = activateBody.IndexOf("OnLayerActiveStateChanged");

        Assert.IsTrue(activeOn >= 0 && activateRaise > activeOn, "HandleActivate: " + string.Join(", ", activateBody));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void HandleDeactivate_IsTheOnlyCallerOfOnDeactivate_SoTheEventCoversEveryLayerDeactivation()
    {
        RequireGame();

        var callers = IlCallScanner.FindCallers(typeof(ScreenLayer).Assembly,
            m => m.Name == "OnDeactivate" && m.DeclaringType == typeof(ScreenLayer), out _, out var scanned);

        Assert.IsTrue(scanned > 100);
        CollectionAssert.AreEquivalent(new[] { "TaleWorlds.ScreenSystem.ScreenLayer.HandleDeactivate" }, callers.Distinct().ToArray());
    }

    private static List<string> ScreenBaseCalls(string method) =>
        PatchProcessor.GetOriginalInstructions(typeof(ScreenBase).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!)
            .Where(ci => ci.operand is MethodBase).Select(ci => ((MethodBase)ci.operand).DeclaringType!.Name + "." + ((MethodBase)ci.operand).Name).ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ScreenBase_ActivatesItsLayersBeforeOnActivateAndOnResume_AndDeactivatesThemBeforeOnPause()
    {
        RequireGame();

        // The release runs on the map layer's activation, so it comes before the map screen's own ready check in OnActivate
        // (OnResume has none; on a pop, HandleActivate activates the layers first). The cover edge comes before OnPause, which
        // is why the release cannot run there.
        var activate = ScreenBaseCalls("HandleActivate");
        Assert.IsTrue(activate.IndexOf("ScreenLayer.HandleActivate") >= 0 && activate.IndexOf("ScreenBase.OnActivate") > activate.IndexOf("ScreenLayer.HandleActivate"),
            "HandleActivate: " + string.Join(", ", activate));

        var resume = ScreenBaseCalls("HandleResume");
        Assert.IsTrue(resume.IndexOf("ScreenLayer.HandleActivate") >= 0 && resume.IndexOf("ScreenBase.OnResume") > resume.IndexOf("ScreenLayer.HandleActivate"),
            "HandleResume: " + string.Join(", ", resume));

        var pause = ScreenBaseCalls("HandlePause");
        Assert.IsTrue(pause.IndexOf("ScreenLayer.HandleDeactivate") >= 0 && pause.IndexOf("ScreenBase.OnPause") > pause.IndexOf("ScreenLayer.HandleDeactivate"),
            "HandlePause: " + string.Join(", ", pause));
    }

    // ---- The members the release reads and calls ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EngineMembers_TheReleaseReadsAndCalls_ExistWithTheirTypes()
    {
        RequireGame();

        var release = typeof(SceneLayer).GetMethod("ClearRuntimeGPUMemory", new[] { typeof(bool) });
        Assert.IsNotNull(release, "SceneLayer.ClearRuntimeGPUMemory(bool) did not resolve.");
        Assert.IsTrue(release.IsPublic);
        Assert.IsFalse(release.IsStatic);
        Assert.AreEqual(typeof(void), release.ReturnType);
        CollectionAssert.AreEqual(new[] { "remove_terrain" }, release.GetParameters().Select(p => p.Name).ToArray(),
            "the call passes false: the terrain stays");

        var isActive = typeof(ScreenLayer).GetProperty("IsActive", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(isActive, "ScreenLayer.IsActive did not resolve.");
        Assert.AreEqual(typeof(bool), isActive.PropertyType);
        Assert.IsTrue(typeof(ScreenLayer).IsAssignableFrom(typeof(SceneLayer)));

        var mapScreen = MapScreenType();
        var instance = mapScreen.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(instance, "MapScreen.Instance did not resolve.");
        Assert.AreEqual(mapScreen, instance.PropertyType);

        var layer = mapScreen.GetProperty("SceneLayer", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(layer, "MapScreen.SceneLayer did not resolve.");
        Assert.AreEqual(typeof(SceneLayer), layer.PropertyType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ClearRuntimeGPUMemory_IsExactlySceneViewClearAllWithClearSceneFalse_AndTheMapScreenCallsItToo()
    {
        RequireGame();

        // The release uses the engine's own entry point (MapScreen.ClearGPUMemory calls it with true) rather than reaching for
        // the scene view: its body is one ClearAll(clearScene: false, remove_terrain).
        var release = typeof(SceneLayer).GetMethod("ClearRuntimeGPUMemory", new[] { typeof(bool) })!;
        var instructions = PatchProcessor.GetOriginalInstructions(release);
        var calls = instructions.Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodBase)
            .Select(ci => ((MethodBase)ci.operand).DeclaringType!.Name + "." + ((MethodBase)ci.operand).Name).ToList();
        CollectionAssert.AreEqual(new[] { "SceneView.ClearAll" }, calls, "calls: " + string.Join(", ", calls));
        var clearAll = (MethodBase)instructions.Single(ci => ci.operand is MethodBase).operand;
        Assert.AreEqual("clearScene", clearAll.GetParameters()[0].Name);
        Assert.IsTrue(instructions.Any(ci => ci.opcode == OpCodes.Ldc_I4_0), "clearScene false");

        var clearGpu = MapScreenType().GetMethod("ClearGPUMemory", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(clearGpu, "MapScreen.ClearGPUMemory did not resolve.");
        Assert.IsTrue(PatchProcessor.GetOriginalInstructions(clearGpu).Any(ci => ci.operand is MethodBase m && m.Name == "ClearRuntimeGPUMemory"),
            "the map screen's own path is the one the release takes");
    }

    // ---- The early-install property: nothing but one NoInlining method names the map screen ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void OnlyTheAdaptersNoInliningMethod_NamesTheMapScreen()
    {
        RequireGame();

        foreach (var method in typeof(MapViewReleaseLayerEvents).GetMethods(All).Where(m => !m.IsAbstract))
            Assert.IsFalse(ReferencesMapScreen(method), "layer events: " + method.Name);
        foreach (var method in typeof(MapViewReleaseService).GetMethods(All).Where(m => !m.IsAbstract))
            Assert.IsFalse(ReferencesMapScreen(method), "service: " + method.Name);
        foreach (var method in typeof(MapViewReleaseCalls).GetMethods(All))
            Assert.IsFalse(ReferencesMapScreen(method), "calls: " + method.Name);

        var naming = typeof(MapViewReleaseAdapter).GetMethods(All).Concat(typeof(MapViewReleaseAdapter).GetProperties(All).SelectMany(p => p.GetAccessors(true)))
            .Where(m => !m.IsAbstract && ReferencesMapScreen(m)).Select(m => m.Name).Distinct().ToList();
        Assert.AreEqual(1, naming.Count, "the adapter methods that name MapScreen: " + string.Join(", ", naming));
        var helper = typeof(MapViewReleaseAdapter).GetMethod(naming[0], All)!;
        Assert.IsTrue(helper.GetMethodImplementationFlags().HasFlag(MethodImplAttributes.NoInlining),
            naming[0] + " must stay NoInlining, or the JIT may fold it into its caller and resolve MapScreen there");
        Assert.IsTrue(helper.IsPrivate || helper.IsAssembly, "not part of the adapter's public surface");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CampaignCheck_RunsBeforeTheMapScreenIsNamed_InTheService()
    {
        RequireGame();

        // The service asks IsCampaignRunning before IsMapSceneLayer; the adapter's check is a bare Campaign.Current read.
        var calls = PatchProcessor.GetOriginalInstructions(typeof(MapViewReleaseService).GetMethod("OnSceneLayerDeactivated")!)
            .Where(ci => ci.operand is MethodBase).Select(ci => ((MethodBase)ci.operand).Name).ToList();
        var campaign = calls.IndexOf("get_IsCampaignRunning");
        var layer = calls.IndexOf("IsMapSceneLayer");

        Assert.IsTrue(campaign >= 0 && layer > campaign, "calls: " + string.Join(", ", calls));
    }

    // ---- The adapter on the installed engine ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Adapter_WithNoCampaign_SaysSoAndTouchesNoMapScreen()
    {
        RequireGame();
        var sut = new MapViewReleaseAdapter();

        Assert.IsFalse(sut.IsCampaignRunning, "no campaign runs in the test host");
    }

    [TestMethod]
    public void Adapter_IsMapSceneLayer_AnObjectThatIsNotASceneLayer_IsFalse()
    {
        RequireGame();

        Assert.IsFalse(new MapViewReleaseAdapter().IsMapSceneLayer(new object()));
    }

    // ---- The MCM settings ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Setting_ReleaseMapViewMemory_IsAMapPerformanceBoolOnByDefaultThatNeedsNoRestart()
    {
        var property = typeof(BattleLoadDiagnosticsSettings).GetProperty("ReleaseMapViewMemory");

        Assert.IsNotNull(property, "BattleLoadDiagnosticsSettings.ReleaseMapViewMemory is missing.");
        Assert.AreEqual(typeof(bool), property.PropertyType);
        var attribute = property.GetCustomAttribute<SettingPropertyBoolAttribute>();
        Assert.IsNotNull(attribute);
        Assert.AreEqual("Release Map View Memory", attribute.DisplayName);
        Assert.IsFalse(attribute.RequireRestart);
        Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.HintText));
        Assert.AreEqual("Map Performance", property.GetCustomAttribute<SettingPropertyGroupAttribute>()!.GroupName);
        Assert.AreEqual(true, new BattleLoadDiagnosticsSettings().ReleaseMapViewMemory);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Setting_MapViewReleaseInterval_IsAnIntegerOneToAThousandDefaultingToTwenty()
    {
        var property = typeof(BattleLoadDiagnosticsSettings).GetProperty("MapViewReleaseInterval");

        Assert.IsNotNull(property, "BattleLoadDiagnosticsSettings.MapViewReleaseInterval is missing.");
        Assert.AreEqual(typeof(int), property.PropertyType);
        var attribute = property.GetCustomAttribute<SettingPropertyIntegerAttribute>();
        Assert.IsNotNull(attribute);
        Assert.AreEqual("Map View Release Interval", attribute.DisplayName);
        Assert.AreEqual(1, (int)attribute.MinValue);
        Assert.AreEqual(1000, (int)attribute.MaxValue);
        Assert.AreEqual(MapViewReleaseCounter.MinInterval, (int)attribute.MinValue);
        Assert.AreEqual(MapViewReleaseCounter.MaxInterval, (int)attribute.MaxValue);
        Assert.IsFalse(attribute.RequireRestart);
        Assert.AreEqual("Map Performance", property.GetCustomAttribute<SettingPropertyGroupAttribute>()!.GroupName);
        Assert.AreEqual(MapViewReleaseCounter.DefaultInterval, new BattleLoadDiagnosticsSettings().MapViewReleaseInterval);
    }
}
