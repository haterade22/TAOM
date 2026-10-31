using DryIoc;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Client.Features.CustomBattles;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Client.Features.FiefManagement;
using TAOM.Client.Features.HallOfFire;
using TAOM.Client.Features.HostReview;
using TAOM.Client.Features.ShaderCompileNotice;
using TAOM.Client.Features.ShaderPrecompilation;
using TAOM.Composition;
using TAOM.Features.CustomBattles;
using TAOM.Features.LinesOfDescent.UI;
using TAOM.Features.FieldCamp;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FiefManagement;
using TAOM.Features.HallOfFire;
using TAOM.Features.HostReview;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Visuals;
using TAOM.Features.ShaderCompileNotice;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.SupplyLines;
using TAOM.Features.WarOfTheRingMomentum.UI;

namespace TAOM.Client;

/// <summary>
/// The client's registrations in core's container (ADR-013), applied by IoC.Configure after every core
/// registration with replace as the default. Each row replaces a core null adapter with the client
/// implementation; the type moves of the later batches (B4b to B5) add theirs here.
/// </summary>
internal sealed class TaomClientComposition : IClientComposition
{
    public void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IFactionMapWidgetAdapter, FactionMapWidgetAdapter>(Reuse.Singleton);
        registrator.Register<IFrontEndSpriteTableAdapter, FrontEndSpriteTableAdapter>(Reuse.Singleton);
        registrator.Register<ICustomBattleFactionSelectionFactory, TaomFactionSelectionFactory>(Reuse.Singleton);
        registrator.Register<IMomentumUIService, MomentumUIService>(Reuse.Singleton);
        registrator.Register<IRealmBordersMapViewAdapter, RealmBordersMapViewAdapter>(Reuse.Singleton);
        registrator.Register<IFieldCampOverlayAdapter, FieldCampOverlayAdapter>(Reuse.Singleton);
        registrator.Register<IFiefsNavigationElementFactory, TaomFiefsNavigationElementFactory>(Reuse.Singleton);
        registrator.Register<IMapConversationProviderWrapper, HallOfFireConversationWrapper>(Reuse.Singleton);
        registrator.Register<IHostReviewOrderProviderRegistrar, HostReviewOrderProviderRegistrar>(Reuse.Singleton);
        registrator.Register<IShaderPrecompileGameManagerFactory, ShaderPrecompileGameManagerFactory>(Reuse.Singleton);
        registrator.Register<IShaderCompileNoticeWindowHost, ShaderCompileNoticeWindowHost>(Reuse.Singleton);
        registrator.Register<IFordsOfWarGameManagerFactory, FordsOfWarGameManagerFactory>(Reuse.Singleton);
        registrator.Register<ICampVisualService, CampVisualService>(Reuse.Singleton);
        registrator.Register<IRefugeVisualService, RefugeVisualService>(Reuse.Singleton);
        registrator.Register<ISupplyRouteVisualService, SupplyRouteVisualService>(Reuse.Singleton);
        registrator.Register<FamilyTreePopup>(Reuse.Singleton);
        registrator.Register<IQuestJournalAdapter, QuestJournalAdapter>(Reuse.Singleton);
    }
}
