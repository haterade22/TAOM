using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.StartupAlerts;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.StartupAlerts;

/// <summary>
/// Wiring guard for the startup alerts: the module is listed once, declares no patch or behavior,
/// and reads no folder or module list outside MainMenu or on a dedicated server.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StartupAlertsWiringTests
{
    private Container _container = null!;
    private IModuleFolderReader _reader = null!;
    private ILoadedModuleIds _loaded = null!;
    private readonly StartupAlertsModule _sut = new StartupAlertsModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _container.RegisterInstance(Substitute.For<IModLogger>());
        _container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        _sut.RegisterServices(_container);
        _reader = Substitute.For<IModuleFolderReader>();
        _loaded = Substitute.For<ILoadedModuleIds>();
    }

    [TestCleanup]
    public void Cleanup() => _container.Dispose();

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<StartupAlertsModule>().Count());
    }

    [TestMethod]
    public void Module_IsEnabledAndDeclaresNoPatchOrBehavior()
    {
        Assert.AreEqual("StartupAlerts", _sut.Id);
        Assert.IsNull(_sut.ParkedReason);
        Assert.IsFalse(_sut.OwnsSaveData);
        Assert.AreEqual(0, _sut.PatchCategories.Count);
        Assert.AreEqual(0, _sut.CampaignBehaviors.Count);
        Assert.AreEqual(0, _sut.GameModels.Count);
        Assert.AreEqual(0, _sut.MissionBehaviors.Count);
    }

    [TestMethod]
    public void RegisterServices_TheServiceAndTheReaderResolveAsSingletons()
    {
        var service = _container.Resolve<BundledLibraryCopyService>();

        Assert.AreSame(service, _container.Resolve<BundledLibraryCopyService>());
        Assert.IsInstanceOfType(_container.Resolve<IModuleFolderReader>(), typeof(ModuleFolderReader));
        Assert.IsInstanceOfType(_container.Resolve<ILoadedModuleIds>(), typeof(LoadedModuleIds));
    }

    [TestMethod]
    public void OnPhase_OtherPhases_ReadNoFolderOrModuleList()
    {
        _container.RegisterInstance(_reader, IfAlreadyRegistered.Replace);
        _container.RegisterInstance(_loaded, IfAlreadyRegistered.Replace);

        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.GameInit, ApplyPhase.FirstMission })
            _sut.OnPhase(phase, _container);

        Assert.AreEqual(0, _reader.ReceivedCalls().Count());
        Assert.AreEqual(0, _loaded.ReceivedCalls().Count());
    }

    [TestMethod]
    public void OnPhase_MainMenuOnADedicatedServer_ReadsNoFolder()
    {
        _container.RegisterInstance(_reader, IfAlreadyRegistered.Replace);
        _container.RegisterInstance(_loaded, IfAlreadyRegistered.Replace);
        _container.Resolve<IDedicatedServerProvider>().IsDedicatedServer.Returns(true);

        _sut.OnPhase(ApplyPhase.MainMenu, _container);

        Assert.AreEqual(0, _reader.ReceivedCalls().Count());
        Assert.AreEqual(0, _loaded.ReceivedCalls().Count());
    }
}
