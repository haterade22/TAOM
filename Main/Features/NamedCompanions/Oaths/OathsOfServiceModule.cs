using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions.Oaths.Hooks;

namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>
/// Oaths of Service (#904) as a feature module: hire terms, extra dialog topics and a home anchor for the named
/// companions. The reused adapters (roster, purse, item pool, armour catalog) and the config provider are
/// registered by their own features earlier in IoC.Configure; DryIoc resolves lazily, so order does not matter.
/// Both behaviors are built with <c>new</c> inside the declarations' factories, which the runner calls once per
/// campaign start, so the campaign behavior's constructor resets the ledger for every campaign. The module owns
/// save data (the ledger), so a fault fails closed.
/// </summary>
internal sealed class OathsOfServiceModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new OathsOfServiceCampaignBehavior(
            r.Resolve<OathLedger>(), r.Resolve<HomeAnchorService>(), r.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(r => new OathsOfServiceDialogBehavior(
            r.Resolve<HireTermsService>(), r.Resolve<OathTopicService>(), r.Resolve<INamedCompanionConfigProvider>(),
            r.Resolve<INamedCompanionAdapter>(), r.Resolve<ICoopSessionProvider>(), r.Resolve<IItemPoolAdapter>(),
            r.Resolve<IArmourItemCatalogAdapter>(), r.Resolve<WandererAllegiance.WandererAllegianceService>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "OathsOfService";

    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<OathLedger>(Reuse.Singleton);
        registrator.RegisterMapping<IOathFlagStore, OathLedger>();
        registrator.Register<HireTermsService>(Reuse.Singleton);
        registrator.Register<OathTopicService>(Reuse.Singleton);
        registrator.Register<HomeAnchorService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
