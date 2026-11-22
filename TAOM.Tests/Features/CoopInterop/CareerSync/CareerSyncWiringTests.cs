using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.CareerSync;

namespace TAOM.Tests.Features.CoopInterop.CareerSync;

/// <summary>
/// TAOM.CoopCompat resolves the career sync client and host inside the try that guards its Coop transport, so one
/// registration that cannot resolve turns the whole module off.
/// </summary>
[TestClass]
public class CareerSyncWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheCareerSyncModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<CareerSyncModule>().Count());
    }

    // CareerPassiveService needs ICareerTierTrust in every game, and only this module registers it.
    [TestMethod]
    public void Module_RegistersTheCareerTierTrust_ThatThePassiveServiceNeeds()
    {
        using var container = new Container();
        foreach (var service in new[] { typeof(ICareerRegistry), typeof(ICoopSessionProvider), typeof(ICareerHeroAdapterFactory) })
            container.RegisterInstance(service, Substitute.For(new[] { service }, new object[0]));

        new CareerSyncModule().RegisterServices(container);

        Assert.IsInstanceOfType(container.Resolve<ICareerTierTrust>(), typeof(CoopCareerTierTrust));
        Assert.AreSame(container.Resolve<ICareerTierTrust>(), container.Resolve<ICareerTierTrust>());
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_ResolvesTheClientAndTheHost_AsSingletons()
    {
        using var container = new Container();
        foreach (var service in new[]
        {
            typeof(ICareerDataService), typeof(ICareerRegistry), typeof(ICareerPassiveService), typeof(ICareerQuestConfigProvider),
            typeof(ICoopSessionProvider), typeof(IPlayerPartyAdapter), typeof(ICareerHeroAdapterFactory), typeof(IModLogger),
        })
            container.RegisterInstance(service, Substitute.For(new[] { service }, new object[0]));

        new CareerSyncModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<CareerSyncClient>(), container.Resolve<CareerSyncClient>());
        Assert.AreSame(container.Resolve<CareerSyncHost>(), container.Resolve<CareerSyncHost>());
        Assert.IsInstanceOfType(container.Resolve<ICareerSyncHeroAdapter>(), typeof(CareerSyncHeroAdapter));
        Assert.IsInstanceOfType(container.Resolve<ICareerSyncNotice>(), typeof(CareerSyncNotice));
    }
}
