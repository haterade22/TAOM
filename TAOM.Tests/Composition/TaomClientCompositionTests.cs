using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Client;
using TAOM.Client.Adapters;
using TAOM.Client.Features.CustomBattles;
using TAOM.Client.Features.FiefManagement;
using TAOM.Client.Features.HallOfFire;
using TAOM.Client.Features.HostReview;
using TAOM.Client.Features.ShaderCompileNotice;
using TAOM.Client.Features.ShaderPrecompilation;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp;
using TAOM.Features.FiefManagement;
using TAOM.Features.HallOfFire;
using TAOM.Features.HostReview;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Visuals;
using TAOM.Features.ShaderCompileNotice;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.SupplyLines;
using TAOM.Features.WarOfTheRingMomentum.UI;
using TAOM.Tests.Migration;
using NSubstitute;

namespace TAOM.Tests.Composition;

/// <summary>ADR-013, B4a and B4b. What TAOM.Client replaces in core's container: the front-end seams and the campaign map seams.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomClientCompositionTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    public void RegisterServices_ReplacesEveryCoreSeam()
    {
        var container = new Container();
        container.Register<IFactionMapWidgetAdapter, NullFactionMapWidgetAdapter>(Reuse.Singleton);
        container.Register<IFrontEndSpriteTableAdapter, NullFrontEndSpriteTableAdapter>(Reuse.Singleton);
        container.Register<ICustomBattleFactionSelectionFactory, NullCustomBattleFactionSelectionFactory>(Reuse.Singleton);
        // The factory and the wrapper take these from the container when first resolved.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(new FiefHubOpener(
            Substitute.For<IFiefManagementSettingsProvider>(), Substitute.For<IFiefHubService>(),
            Substitute.For<IEnlistmentStateQuery>(), Substitute.For<IFiefHubHostAdapter>(), Substitute.For<IModLogger>()));
        container.RegisterInstance(new HallOfFireService(Substitute.For<IHallOfFireConfigProvider>()));
        container.Register<IMomentumUIService, NullMomentumUIService>(Reuse.Singleton);
        container.Register<IRealmBordersMapViewAdapter, NullRealmBordersMapViewAdapter>(Reuse.Singleton);
        container.Register<IFieldCampOverlayAdapter, NullFieldCampOverlayAdapter>(Reuse.Singleton);
        container.Register<IFiefsNavigationElementFactory, NullFiefsNavigationElementFactory>(Reuse.Singleton);
        container.Register<IMapConversationProviderWrapper, NullMapConversationProviderWrapper>(Reuse.Singleton);
        container.Register<IHostReviewOrderProviderRegistrar, NullHostReviewOrderProviderRegistrar>(Reuse.Singleton);
        container.Register<IShaderPrecompileGameManagerFactory, NullShaderPrecompileGameManagerFactory>(Reuse.Singleton);
        container.Register<IShaderCompileNoticeWindowHost, NullShaderCompileNoticeWindowHost>(Reuse.Singleton);
        // The shader factory's constructor dependencies come from core's feature registrations.
        container.RegisterInstance(Substitute.For<IShaderPrecompilationService>());
        container.Register<ICampVisualService, NullCampVisualService>(Reuse.Singleton);
        container.Register<IRefugeVisualService, NullRefugeVisualService>(Reuse.Singleton);
        container.Register<ISupplyRouteVisualService, NullSupplyRouteVisualService>(Reuse.Singleton);
        container.Register<IQuestJournalAdapter, NullQuestJournalAdapter>(Reuse.Singleton);
        // The map visual services' constructor dependencies come from the camps features' registrations.
        container.RegisterInstance(Substitute.For<ISupplyOrderService>());
        container.RegisterInstance(Substitute.For<ISupplyLinesSettingsProvider>());
        container.RegisterInstance(Substitute.For<IRefugeSettingsProvider>());
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IFactionMapWidgetAdapter>(), typeof(FactionMapWidgetAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFrontEndSpriteTableAdapter>(), typeof(FrontEndSpriteTableAdapter));
        Assert.IsInstanceOfType(container.Resolve<ICustomBattleFactionSelectionFactory>(), typeof(TaomFactionSelectionFactory));
        Assert.IsInstanceOfType(container.Resolve<IMomentumUIService>(), typeof(MomentumUIService));
        Assert.IsInstanceOfType(container.Resolve<IRealmBordersMapViewAdapter>(), typeof(RealmBordersMapViewAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFieldCampOverlayAdapter>(), typeof(FieldCampOverlayAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFiefsNavigationElementFactory>(), typeof(TaomFiefsNavigationElementFactory));
        Assert.IsInstanceOfType(container.Resolve<IMapConversationProviderWrapper>(), typeof(HallOfFireConversationWrapper));
        Assert.IsInstanceOfType(container.Resolve<IHostReviewOrderProviderRegistrar>(), typeof(HostReviewOrderProviderRegistrar));
        Assert.IsInstanceOfType(container.Resolve<IShaderPrecompileGameManagerFactory>(), typeof(ShaderPrecompileGameManagerFactory));
        Assert.IsInstanceOfType(container.Resolve<IShaderCompileNoticeWindowHost>(), typeof(ShaderCompileNoticeWindowHost));
        Assert.IsInstanceOfType(container.Resolve<ICampVisualService>(), typeof(CampVisualService));
        Assert.IsInstanceOfType(container.Resolve<IRefugeVisualService>(), typeof(RefugeVisualService));
        Assert.IsInstanceOfType(container.Resolve<ISupplyRouteVisualService>(), typeof(SupplyRouteVisualService));
        Assert.IsInstanceOfType(container.Resolve<IQuestJournalAdapter>(), typeof(QuestJournalAdapter));
        var seams = new[]
        {
            typeof(IFactionMapWidgetAdapter), typeof(IFrontEndSpriteTableAdapter), typeof(ICustomBattleFactionSelectionFactory),
            typeof(IMomentumUIService), typeof(IRealmBordersMapViewAdapter), typeof(IFieldCampOverlayAdapter),
            typeof(IFiefsNavigationElementFactory), typeof(IMapConversationProviderWrapper),
            typeof(IHostReviewOrderProviderRegistrar), typeof(IShaderPrecompileGameManagerFactory),
            typeof(IShaderCompileNoticeWindowHost), typeof(ICampVisualService), typeof(IRefugeVisualService),
            typeof(ISupplyRouteVisualService), typeof(IQuestJournalAdapter),
        };
        Assert.AreEqual(15, container.GetServiceRegistrations().Count(r => seams.Contains(r.ServiceType)),
            "each seam is replaced, never appended");
    }
}
