using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MapViewRelease;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapViewRelease;

/// <summary>The module's wiring against a real DryIoc container, and its place in the module list.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MapViewReleaseModuleTests
{
    private static bool _gameLoaded;

    private Container _container = null!;
    private IModLogger _logger = null!;
    private readonly MapViewReleaseModule _sut = new MapViewReleaseModule();

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _container = new Container();
        _logger = Substitute.For<IModLogger>();
        _container.RegisterInstance(_logger);
        _sut.RegisterServices(_container);
        _container.RegisterInstance(Substitute.For<IMapViewReleaseAdapter>(), IfAlreadyRegistered.Replace);
    }

    [TestCleanup]
    public void Cleanup()
    {
        MapViewReleaseCalls.Initialize(null);
        _container?.Dispose();
    }

    [TestMethod]
    public void RegisterServices_TheServiceResolvesAsASingleton()
    {
        var service = _container.Resolve<IMapViewReleaseService>();

        Assert.IsInstanceOfType(service, typeof(MapViewReleaseService));
        Assert.AreSame(service, _container.Resolve<IMapViewReleaseService>());
    }

    [TestMethod]
    public void RegisterServices_TheProviderResolvesFromARealContainer_WithItsInternalConstructorBesideThePublicOne()
    {
        Assert.IsInstanceOfType(_container.Resolve<IMapViewReleaseSettingsProvider>(), typeof(MapViewReleaseSettingsProvider));
    }

    [TestMethod]
    public void RegisterServices_TheRealAdapterIsRegistered()
    {
        var fresh = new Container();

        new MapViewReleaseModule().RegisterServices(fresh);

        Assert.IsTrue(fresh.IsRegistered<IMapViewReleaseAdapter>());
        fresh.Dispose();
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToTheHandler()
    {
        _sut.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<IMapViewReleaseService>(), MapViewReleaseCalls.Service);
    }

    [TestMethod]
    public void OnPhase_GameInit_WritesTheInstallLine()
    {
        _sut.OnPhase(ApplyPhase.GameInit, _container);

        var infos = _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!).ToList();
        StringAssert.StartsWith(infos.Single(), "[MapViewRelease] ");
    }

    [TestMethod]
    public void OnPhase_OtherPhases_DoNothing()
    {
        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.MainMenu, ApplyPhase.FirstMission })
            _sut.OnPhase(phase, _container);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Module_DeclaresNoHarmonyPatch_AndIsListedOnce()
    {
        var modules = FeatureModules.All.OfType<MapViewReleaseModule>().ToList();

        Assert.AreEqual(1, modules.Count, "MapViewReleaseModule is not in FeatureModules.All exactly once.");
        Assert.AreEqual("MapViewRelease", modules[0].Id);
        Assert.IsNull(modules[0].ParkedReason);
        Assert.AreEqual(0, modules[0].PatchCategories.Count, "the event subscription replaced Patch105");
    }

    [TestMethod]
    public void OnPhase_GameInit_SubscribesTheLayerEventHandlerOnceHoweverOftenItRuns()
    {
        _sut.OnPhase(ApplyPhase.GameInit, _container);
        _sut.OnPhase(ApplyPhase.GameInit, _container);

        var field = typeof(TaleWorlds.ScreenSystem.ScreenLayer).GetField("OnLayerActiveStateChanged",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        var handlers = ((System.Delegate?)field.GetValue(null))?.GetInvocationList()
            .Count(d => d.Method.DeclaringType == typeof(TAOM.Features.MapViewRelease.Hooks.MapViewReleaseLayerEvents));
        Assert.AreEqual(1, handlers);
    }
}
