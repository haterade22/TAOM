using System;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Client;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Hooks;
using TAOM.Features.SupplyLines;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Composition;

/// <summary>
/// ADR-013, B7 slice 1. CampService and RefugeService capture their visual service in the constructor, so the
/// client's implementation reaches them only when nothing resolves them before ClientComposition.Apply. This builds
/// the graph in IoC.Configure's order (the three features, the client composition, then the two patch statics)
/// and pins what each consumer holds.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClientMapVisualsCompositionTests
{
    private static readonly BindingFlags NonPublicStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [TestInitialize]
    public void RequireGame()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestCleanup]
    public void Cleanup()
    {
        ClientComposition.Current = null;
        ClientComposition.IsDedicatedServer = ClientComposition.ReadDedicatedServer;
        FieldCampCampaignBehavior.Overlay = new NullFieldCampOverlayAdapter();
        Clear(typeof(PartyNameplateCampIconPatch), "_campService");
        Clear(typeof(RefugeClanScreenPatch), "_refuges");
        Clear(typeof(RefugeEncounterPatch), "_refuges");
        Clear(typeof(RefugeEncounterPatch), "_enlistment");
        Clear(typeof(RefugeEncounterPatch), "_menus");
        Clear(typeof(RefugeEncounterPatch), "_coop");
    }

    private static void Clear(Type type, string field) =>
        type.GetField(field, NonPublicStatic)!.SetValue(null, null);

    private static object? Held(object service, string field) =>
        service.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service);

    private static Container Configure(IClientComposition? client)
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());
        // Fire and Tribute's module registers this in production (IoC.cs, before the Refuge resolve); the Refuge raid takes it lazily (#1033).
        container.RegisterInstance(Substitute.For<TAOM.Features.FireAndTribute.IRaidHostOwnSideQuery>());
        // CoopInterop's module registers this in production, ahead of the camps features (IoC.cs).
        container.RegisterInstance(Substitute.For<TAOM.Features.CoopInterop.ICoopSessionProvider>());
        SupplyLinesIoC.RegisterSupplyLinesFeature(container);
        FieldCampIoC.RegisterFieldCampFeature(container);
        RefugeIoC.RegisterRefugeFeature(container);
        ClientComposition.Current = client;

        ClientComposition.Apply(container);
        FieldCampIoC.InitializePatchStatics(container);
        RefugeIoC.InitializePatchStatics(container);
        return container;
    }

    [TestMethod]
    public void Apply_ThenThePatchStatics_TheCampsServicesHoldTheClientVisuals()
    {
        var container = Configure(new TaomClientComposition());

        var clientAssembly = typeof(ClientSubModule).Assembly;
        var camp = Held(container.Resolve<ICampService>(), "_visuals")!;
        var refuge = Held(container.Resolve<IRefugeService>(), "_visuals")!;
        var route = container.Resolve<ISupplyRouteVisualService>();
        // One singleton each: a transient registration would hand ClearAll a different instance than Show used.
        Assert.AreSame(container.Resolve<ICampVisualService>(), camp);
        Assert.AreSame(container.Resolve<IRefugeVisualService>(), refuge);
        Assert.AreSame(clientAssembly, camp.GetType().Assembly, "CampService holds " + camp.GetType().FullName);
        Assert.AreSame(clientAssembly, refuge.GetType().Assembly, "RefugeService holds " + refuge.GetType().FullName);
        Assert.AreSame(clientAssembly, route.GetType().Assembly, "the route visual is " + route.GetType().FullName);
        Assert.AreEqual("CampVisualService", camp.GetType().Name);
        Assert.AreEqual("RefugeVisualService", refuge.GetType().Name);
        Assert.AreEqual("SupplyRouteVisualService", route.GetType().Name);
    }

    [TestMethod]
    public void Apply_NoClientOnAServer_TheCampsServicesHoldTheNullVisuals()
    {
        ClientComposition.IsDedicatedServer = () => true;

        var container = Configure(null);

        Assert.IsInstanceOfType(Held(container.Resolve<ICampService>(), "_visuals"), typeof(NullCampVisualService));
        Assert.IsInstanceOfType(Held(container.Resolve<IRefugeService>(), "_visuals"), typeof(NullRefugeVisualService));
        Assert.AreSame(container.Resolve<ICampVisualService>(), Held(container.Resolve<ICampService>(), "_visuals"));
        Assert.AreSame(container.Resolve<IRefugeVisualService>(), Held(container.Resolve<IRefugeService>(), "_visuals"));
        Assert.IsInstanceOfType(container.Resolve<ISupplyRouteVisualService>(), typeof(NullSupplyRouteVisualService));
    }
}
