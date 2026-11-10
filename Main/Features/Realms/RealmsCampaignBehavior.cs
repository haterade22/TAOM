using TaleWorlds.CampaignSystem;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Realms;

/// <summary>
/// The campaign entry point of the title system (docs/features/realms.md). Kept thin (ADR-002): the work is in
/// <see cref="RealmsCampaign"/>. The class name is the behavior's save key (CampaignBehaviorBase uses the type name as
/// StringId), and it is the name the separate TAOM.Realms module used, so its saves keep their titles.
/// </summary>
public sealed class RealmsCampaignBehavior : CampaignBehaviorBase
{
    public RealmsCampaignBehavior(IRealmsPopupAdapter popups, ICoopSessionProvider coop) =>
        Realms = new RealmsCampaign(popups, () => coop.IsAuthority);

    public static RealmsCampaignBehavior? Current =>
        TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<RealmsCampaignBehavior>();

    public RealmsCampaign Realms { get; }

    public override void RegisterEvents() => Realms.RegisterEvents(this);

    public override void SyncData(IDataStore dataStore) => Realms.SyncData(dataStore);
}
