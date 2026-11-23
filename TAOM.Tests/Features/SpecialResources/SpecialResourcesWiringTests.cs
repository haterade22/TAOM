using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Features.TroopWeight;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #768: SpecialResourceService takes ICareerPassiveService as an OPTIONAL constructor parameter, so a
/// dependency that stops resolving (CareerPassiveService gained IPartySizeCacheInvalidator, which
/// production registers in IoC.RegisterCoreServices) would silently leave the service career-blind with
/// no error. This pins the graph across the two features, in the Arena/CompanionTactics wiring shape.
/// </summary>
[TestClass]
public class SpecialResourcesWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        // Production registers this in IoC.RegisterCoreServices, outside both features.
        container.RegisterInstance(Substitute.For<IPartySizeCacheInvalidator>());
        // Production registers these in the CoopInterop and TroopWeight features, outside this one.
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        container.RegisterInstance(Substitute.For<ITroopWeightService>());
        // Production registers this in the CareerSync feature module (CoopCareerTierTrust).
        container.RegisterInstance(Substitute.For<ICareerTierTrust>());
        // Production registers this in the CheatMenu feature module.
        container.RegisterInstance(Substitute.For<TAOM.Features.CheatMenu.ICheatPartyRules>());
        CareerSystemIoC.RegisterCareerSystemFeature(container);
        SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
        return container;
    }

    [TestMethod]
    public void RegisterSpecialResources_WithCareerSystem_GraphResolvesWithoutError()
    {
        var container = BuildContainer();

        var errors = container.Validate(
            typeof(ISpecialResourceService), typeof(ICareerPassiveService),
            typeof(ICareerLifecycleService), typeof(CareerSwitchService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void RegisterSpecialResources_EarningServiceAndEventAdapter_ResolveWithoutError()
    {
        var container = BuildContainer();

        var errors = container.Validate(typeof(SpecialResourceEarningService), typeof(ISpecialResourceEventAdapter));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    public void RegisterSpecialResources_PartyUpgradeHook_ResolvesWithItsSessionProvider()
    {
        // SubModule resolves this hook for Patch26, and since #563 decision A it needs ICoopSessionProvider.
        var container = BuildContainer();

        var errors = container.Validate(typeof(TAOM.Features.SpecialResources.Hooks.IOnPartyUpgradeResourceCheck));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ResolveEarningService_Twice_ReturnsFreshInstances()
    {
        // SubModule resolves the service once per campaign, and it holds the campaign's one-tick desertion
        // grace and its log-once latches, so a second campaign must not inherit the first one's.
        var container = BuildContainer();

        var first = container.Resolve<SpecialResourceEarningService>();
        var second = container.Resolve<SpecialResourceEarningService>();

        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ResolveSpecialResourceService_WithCareerSystem_HasACareerPassiveService()
    {
        var container = BuildContainer();

        var service = container.Resolve<ISpecialResourceService>();

        var field = typeof(SpecialResourceService).GetField("_passiveService", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "SpecialResourceService._passiveService was renamed: update this wiring test.");
        Assert.IsNotNull(field.GetValue(service),
            "The optional ICareerPassiveService did not resolve, so SpecialResources runs career-blind.");
    }

    [TestMethod]
    public void MainIoC_RegistersThePartySizeCacheInvalidator_InRegisterCoreServices()
    {
        // Both CareerSystem and AiPartySize take this dependency; the tests above fake it, so only the
        // source can show the production registration still exists (#768).
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        int start = src.IndexOf("void RegisterCoreServices(", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "IoC.RegisterCoreServices was renamed: update this pin.");
        int open = src.IndexOf('{', start);
        int depth = 0, end = -1;
        for (int i = open; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}' && --depth == 0) { end = i; break; }
        }
        Assert.AreNotEqual(-1, end, "RegisterCoreServices body not found.");

        StringAssert.Contains(src.Substring(open, end - open),
            "container.Register<IPartySizeCacheInvalidator, PartySizeCacheInvalidator>(Reuse.Singleton);");
    }
}
