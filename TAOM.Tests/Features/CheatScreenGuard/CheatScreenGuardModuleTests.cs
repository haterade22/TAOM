using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CheatScreenGuard;
using TAOM.Features.CheatScreenGuard.Hooks;

namespace TAOM.Tests.Features.CheatScreenGuard;

/// <summary>The module's wiring against a real DryIoc container, and its place in the module list.</summary>
[TestClass]
public class CheatScreenGuardModuleTests
{
    private Container _container = null!;
    private IModLogger _logger = null!;
    private readonly CheatScreenGuardModule _sut = new CheatScreenGuardModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _logger = Substitute.For<IModLogger>();
        _container.RegisterInstance(_logger);
        _container.RegisterInstance(Substitute.For<IInquiryAdapter>());
        _sut.RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup()
    {
        CheatScreenGuardCalls.Initialize(null);
        CheatScreenGuardSwaps.LastInventorySwapped = -1;
        CheatScreenGuardSwaps.LastPartySwapped = -1;
        _container.Dispose();
    }

    [TestMethod]
    public void RegisterServices_TheServiceResolvesAsASingleton()
    {
        var service = _container.Resolve<ICheatScreenGuardService>();

        Assert.IsInstanceOfType(service, typeof(CheatScreenGuardService));
        Assert.AreSame(service, _container.Resolve<ICheatScreenGuardService>());
    }

    [TestMethod]
    public void RegisterServices_TheProviderResolvesFromARealContainer_WithItsInternalConstructorBesideThePublicOne()
        => Assert.IsInstanceOfType(_container.Resolve<ICheatScreenGuardSettingsProvider>(), typeof(CheatScreenGuardSettingsProvider));

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToTheCalls()
    {
        _sut.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<ICheatScreenGuardService>(), CheatScreenGuardCalls.Service);
    }

    [TestMethod]
    public void OnPhase_GameInit_WithBothSitesSwapped_WritesTheOnLineOnce()
    {
        CheatScreenGuardSwaps.LastInventorySwapped = 1;
        CheatScreenGuardSwaps.LastPartySwapped = 1;

        _sut.OnPhase(ApplyPhase.GameInit, _container);

        _logger.Received(1).LogInfo(NSubstitute.Arg.Is<string>(s => s.StartsWith("[CheatScreenGuard] ON")));
    }

    [TestMethod]
    public void OnPhase_OtherPhases_LogNothing()
    {
        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.MainMenu, ApplyPhase.FirstMission })
            _sut.OnPhase(phase, _container);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Module_DeclaresItsCategoryAtGameInit_AndIsListedOnce()
    {
        var modules = FeatureModules.All.OfType<CheatScreenGuardModule>().ToList();

        Assert.AreEqual(1, modules.Count, "CheatScreenGuardModule is not in FeatureModules.All exactly once.");
        Assert.AreEqual("CheatScreenGuard", modules[0].Id);
        Assert.IsNull(modules[0].ParkedReason);
        var decl = modules[0].PatchCategories.Single();
        Assert.AreEqual("Patch_CheatScreenGuard", decl.Category);
        Assert.AreEqual(ApplyPhase.GameInit, decl.Phase);
    }
}
