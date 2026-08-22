using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.Refuge.Components;

/// <summary>
/// Party component for a standing refuge: a stationary, player-clan map party that stores troops,
/// prisoners and goods. Persisted (definer id 102); the party AI is pinned at spawn AND re-pinned
/// on load (the source only pinned at spawn, so refuges could wander after a reload).
/// </summary>
public sealed class RefugePartyComponent : PartyComponent
{
    [SaveableField(101)] private readonly string _refugeId;

    [SaveableField(102)] private readonly Settlement _homeSettlement;

    [SaveableField(103)] private Hero _warden;

    public RefugePartyComponent(string refugeId, Settlement homeSettlement, Hero warden)
    {
        _refugeId = refugeId;
        _homeSettlement = homeSettlement;
        _warden = warden;
    }

    public string RefugeId => _refugeId;

    public Hero Warden => _warden;

    public void SetWarden(Hero warden) => _warden = warden;

    public override Hero PartyOwner => Hero.MainHero;

    public override Hero Leader => _warden;

    public override TextObject Name => new TextObject("{=taom_rf_party_name}Refuge");

    public override Settlement HomeSettlement => _homeSettlement;

    // A refuge never initiates anything; it is attacked, it does not attack.
    public override bool AvoidHostileActions => true;

    public override Banner GetDefaultComponentBanner() => Hero.MainHero?.ClanBanner;
}
