// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine.Screens;
using TaleWorlds.ScreenSystem;
using TAOM.Features.MapViewRelease;
using TAOM.Features.MapViewRelease.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapViewRelease;

/// <summary>
/// The thin handler of <c>ScreenLayer.OnLayerActiveStateChanged</c>: it ignores every layer but a scene layer, hands an
/// activated one and a deactivated one to the service's two edges and never throws into the engine's layer change. Layers are built without their constructors
/// (a scene layer needs the native engine), with <c>IsActive</c> set by reflection.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MapViewReleaseHookTests
{
    private static bool _gameLoaded;
    private IMapViewReleaseService _service = null!;

    private sealed class OtherLayer : ScreenLayer
    {
        private OtherLayer() : base("other", 0) { }
    }

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _service = Substitute.For<IMapViewReleaseService>();
        MapViewReleaseCalls.Initialize(_service);
    }

    [TestCleanup]
    public void Cleanup() => MapViewReleaseCalls.Initialize(null);

    private static T Layer<T>(bool active) where T : ScreenLayer
    {
        var layer = (T)FormatterServices.GetUninitializedObject(typeof(T));
        typeof(ScreenLayer).GetProperty(nameof(ScreenLayer.IsActive))!.GetSetMethod(true)!.Invoke(layer, new object[] { active });
        return layer;
    }

    [TestMethod]
    public void Handler_ADeactivatedSceneLayer_ForwardsItToTheDeactivationEdgeOnly()
    {
        var layer = Layer<SceneLayer>(active: false);

        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(layer);

        _service.Received(1).OnSceneLayerDeactivated(Arg.Is<object>(o => ReferenceEquals(o, layer)));
        _service.DidNotReceiveWithAnyArgs().OnSceneLayerActivated(default!);
    }

    [TestMethod]
    public void Handler_AnActivatedSceneLayer_ForwardsItToTheActivationEdgeOnly()
    {
        var layer = Layer<SceneLayer>(active: true);

        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(layer);

        _service.Received(1).OnSceneLayerActivated(Arg.Is<object>(o => ReferenceEquals(o, layer)));
        _service.DidNotReceiveWithAnyArgs().OnSceneLayerDeactivated(default!);
    }

    [TestMethod]
    public void Handler_ALayerThatIsNotASceneLayer_GoesToNeitherEdge()
    {
        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(Layer<OtherLayer>(active: false));
        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(Layer<OtherLayer>(active: true));

        _service.DidNotReceiveWithAnyArgs().OnSceneLayerDeactivated(default!);
        _service.DidNotReceiveWithAnyArgs().OnSceneLayerActivated(default!);
    }

    [TestMethod]
    public void Handler_ANullLayer_IsIgnored()
    {
        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(null!);

        _service.DidNotReceiveWithAnyArgs().OnSceneLayerDeactivated(default!);
        _service.DidNotReceiveWithAnyArgs().OnSceneLayerActivated(default!);
    }

    [TestMethod]
    public void Handler_NoService_DoesNothing()
    {
        MapViewReleaseCalls.Initialize(null);

        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(Layer<SceneLayer>(active: false));
        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(Layer<SceneLayer>(active: true));
    }

    [TestMethod]
    public void Handler_TheServiceThrows_DoesNotThrowIntoTheEngine()
    {
        _service.When(s => s.OnSceneLayerDeactivated(Arg.Any<object>())).Do(_ => throw new InvalidOperationException("boom"));
        _service.When(s => s.OnSceneLayerActivated(Arg.Any<object>())).Do(_ => throw new InvalidOperationException("boom"));

        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(Layer<SceneLayer>(active: false));
        MapViewReleaseLayerEvents.OnLayerActiveStateChanged(Layer<SceneLayer>(active: true));
    }

    private static int Subscriptions() =>
        ((Delegate?)typeof(ScreenLayer).GetField(nameof(ScreenLayer.OnLayerActiveStateChanged), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetValue(null))?.GetInvocationList()
        .Count(d => d.Method.DeclaringType == typeof(MapViewReleaseLayerEvents)) ?? 0;

    [TestMethod]
    public void SubscribeOnce_CalledTwice_LeavesExactlyOneSubscriptionOfTheHandler()
    {
        MapViewReleaseLayerEvents.SubscribeOnce();
        MapViewReleaseLayerEvents.SubscribeOnce();

        Assert.AreEqual(1, Subscriptions());
        var handler = ((Delegate)typeof(ScreenLayer).GetField(nameof(ScreenLayer.OnLayerActiveStateChanged), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetValue(null)!).GetInvocationList().Single(d => d.Method.DeclaringType == typeof(MapViewReleaseLayerEvents));
        Assert.AreEqual(nameof(MapViewReleaseLayerEvents.OnLayerActiveStateChanged), handler.Method.Name);
    }

    // Compiling the handler and the subscription here, in a test host with no campaign, is the check that installing at game
    // init resolves no map type and runs no initializer that needs one.
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void HandlerAndSubscription_CompileWithoutACampaign()
    {
        foreach (var name in new[] { nameof(MapViewReleaseLayerEvents.OnLayerActiveStateChanged), nameof(MapViewReleaseLayerEvents.SubscribeOnce) })
            RuntimeHelpers.PrepareMethod(typeof(MapViewReleaseLayerEvents)
                .GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.MethodHandle);
    }
}
