using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.NameplateCull;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>The module's wiring against a real DryIoc container, and its place in the module list.</summary>
[TestClass]
public class NameplateCullModuleTests
{
    private Container _container = null!;
    private IModLogger _logger = null!;
    private INameplateCullAdapter _adapter = null!;
    private readonly NameplateCullModule _sut = new NameplateCullModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _logger = Substitute.For<IModLogger>();
        _container.RegisterInstance(_logger);
        _sut.RegisterServices(_container);
        _adapter = Substitute.For<INameplateCullAdapter>();
        _container.RegisterInstance(_adapter, IfAlreadyRegistered.Replace);
    }

    [TestCleanup]
    public void Cleanup()
    {
        NameplateCullCalls.Initialize(null);
        _container.Dispose();
    }

    [TestMethod]
    public void RegisterServices_TheServiceResolvesAsASingleton()
    {
        var service = _container.Resolve<INameplateCullService>();

        Assert.IsInstanceOfType(service, typeof(NameplateCullService));
        Assert.AreSame(service, _container.Resolve<INameplateCullService>());
    }

    [TestMethod]
    public void RegisterServices_TheProviderResolvesFromARealContainer_WithItsInternalConstructorBesideThePublicOne()
    {
        var provider = _container.Resolve<INameplateCullSettingsProvider>();

        Assert.IsInstanceOfType(provider, typeof(NameplateCullSettingsProvider));
    }

    [TestMethod]
    public void RegisterServices_TheRealAdapterIsRegistered()
    {
        var fresh = new Container();

        new NameplateCullModule().RegisterServices(fresh);

        Assert.IsTrue(fresh.IsRegistered<INameplateCullAdapter>());
        fresh.Dispose();
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToThePatch()
    {
        _sut.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<INameplateCullService>(), NameplateCullCalls.Service);
    }

    [TestMethod]
    public void OnPhase_GameInit_BindsTheEngineMembersOnce()
    {
        _sut.OnPhase(ApplyPhase.GameInit, _container);

        _adapter.Received(1).Initialize();
    }

    [TestMethod]
    public void OnPhase_OtherPhases_DoNothing()
    {
        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.MainMenu, ApplyPhase.FirstMission })
            _sut.OnPhase(phase, _container);

        _adapter.DidNotReceive().Initialize();
        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Module_DeclaresPatch104AtGameInit_AndIsListedOnce()
    {
        var modules = FeatureModules.All.OfType<NameplateCullModule>().ToList();

        Assert.AreEqual(1, modules.Count, "NameplateCullModule is not in FeatureModules.All exactly once.");
        Assert.AreEqual("NameplateCull", modules[0].Id);
        Assert.IsNull(modules[0].ParkedReason);
        var decl = modules[0].PatchCategories.Single();
        Assert.AreEqual("Patch104_NameplateCull", decl.Category);
        Assert.AreEqual(ApplyPhase.GameInit, decl.Phase);
    }
}
