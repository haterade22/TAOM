using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.Arachnophobia;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.EliteEmissary;
using TAOM.Features.SpecialResources;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.CoopInterop.GuestTrade;

/// <summary>
/// TAOM.CoopCompat resolves the client and the host inside the try that guards its Coop transport, so one
/// registration that cannot resolve turns the whole module off.
/// </summary>
[TestClass]
public class GuestTradeWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheGuestTradeModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<GuestTradeModule>().Count());
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_ResolvesTheClientAndTheHost_AsSingletons()
    {
        using var container = new Container();
        foreach (var service in new[]
        {
            typeof(ICoopSessionProvider), typeof(IPlayerPartyAdapter), typeof(ISpecialResourceStorageService), typeof(IModLogger),
            typeof(ISettlementOwnerAdapter), typeof(ITroopWeightService), typeof(IEliteEmissaryConfigProvider),
            typeof(IEliteEmissarySettingsProvider), typeof(ISpecialResourceService), typeof(ISpecialResourceConfigProvider),
            typeof(ISpecialResourceSettingsProvider), typeof(IOutcomeLog), typeof(IArachnophobiaService), typeof(IArmourGateService),
            typeof(IArmourAcquisitionConfigProvider), typeof(IArmourAcquisitionSettingsProvider), typeof(IArmouryTownAdapter),
            typeof(ISpecialResourceSpender), typeof(ICultureMarketplaceConfigProvider),
        })
            container.RegisterInstance(service, Substitute.For(new[] { service }, new object[0]));
        container.Register<ArmourAcquisitionState>(Reuse.Singleton);
        container.Register<VisitingArmourerService>(Reuse.Singleton);
        container.Register<ArmouryLevelService>(Reuse.Singleton);

        new GuestTradeModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<GuestTradeClient>(), container.Resolve<GuestTradeClient>());
        Assert.AreSame(container.Resolve<GuestTradeHost>(), container.Resolve<GuestTradeHost>());
        Assert.IsInstanceOfType(container.Resolve<IGuestTradeShops>(), typeof(GuestTradeShops));
        Assert.IsInstanceOfType(container.Resolve<IGuestTradeHeroAdapter>(), typeof(GuestTradeHeroAdapter));
    }
}
