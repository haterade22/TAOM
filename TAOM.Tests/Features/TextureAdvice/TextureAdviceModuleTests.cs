using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.TextureAdvice;

namespace TAOM.Tests.Features.TextureAdvice;

/// <summary>The module's wiring against a real DryIoc container, and its place in the module list.</summary>
[TestClass]
public class TextureAdviceModuleTests
{
    private Container _container = null!;
    private readonly TextureAdviceModule _sut = new TextureAdviceModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _container.RegisterInstance(Substitute.For<IModLogger>());
        _sut.RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup() => _container.Dispose();

    private ITextureAdviceService SubstituteTheService()
    {
        var service = Substitute.For<ITextureAdviceService>();
        _container.RegisterInstance(service, IfAlreadyRegistered.Replace);
        return service;
    }

    [TestMethod]
    public void RegisterServices_TheServiceResolvesAsASingleton()
    {
        var service = _container.Resolve<ITextureAdviceService>();

        Assert.IsInstanceOfType(service, typeof(TextureAdviceService));
        Assert.AreSame(service, _container.Resolve<ITextureAdviceService>());
    }

    [TestMethod]
    public void OnPhase_MainMenu_AdvisesOnce()
    {
        var service = SubstituteTheService();

        _sut.OnPhase(ApplyPhase.MainMenu, _container);

        service.Received(1).AdviseOnce();
    }

    [TestMethod]
    public void OnPhase_OtherPhases_DoNothing()
    {
        var service = SubstituteTheService();

        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.GameInit, ApplyPhase.FirstMission })
            _sut.OnPhase(phase, _container);

        service.DidNotReceive().AdviseOnce();
    }

    [TestMethod]
    public void Module_IsListedOnce_WithNoCategories()
    {
        var modules = FeatureModules.All.OfType<TextureAdviceModule>().ToList();

        Assert.AreEqual(1, modules.Count, "TextureAdviceModule is not in FeatureModules.All exactly once.");
        Assert.AreEqual("TextureAdvice", modules[0].Id);
        Assert.IsNull(modules[0].ParkedReason);
        Assert.AreEqual(0, modules[0].PatchCategories.Count);
    }

    [TestMethod]
    public void RegisterServices_TheRealAdapterIsRegistered()
    {
        var fresh = new Container();

        new TextureAdviceModule().RegisterServices(fresh);

        Assert.IsTrue(fresh.IsRegistered<ITextureAdviceAdapter>());
        fresh.Dispose();
    }
}
