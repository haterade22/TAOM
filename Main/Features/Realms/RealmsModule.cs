using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.CoopInterop;
using TAOM.Features.Realms.Hooks;

namespace TAOM.Features.Realms;

/// <summary>
/// Realms, the feudal title system formerly shipped as the separate TAOM.Realms module (docs/features/realms.md):
/// generated titles over the fiefs and realms, their succession, claims, vassal duties, offices and the coronation.
/// The behavior is built fresh for each campaign, so its ledger never outlives the campaign it belongs to.
/// </summary>
internal sealed class RealmsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new RealmsCampaignBehavior(r.Resolve<IRealmsPopupAdapter>(), r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IRealmsSettingsProvider>())),
    };

    public override string Id => "Realms";

    /// <summary>RealmsCampaignBehavior saves the titles and their ledger (taom_realms_chunks).</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        // The windows are client-only (ADR-013): core's null adapter, replaced by TAOM.Client's.
        registrator.Register<IRealmsPopupAdapter, NullRealmsPopupAdapter>(Reuse.Singleton);
        // The "World/Realms" mod options.
        registrator.Register<IRealmsSettingsProvider, RealmsSettingsProvider>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.GameInit)
            PartyNames.Patch();
    }
}
