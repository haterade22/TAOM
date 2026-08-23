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

    /// <summary>Engine contract: PartyComponent.ChangePartyLeader notifies subclasses through this
    /// override; a component that caches its leader and ignores it (the base body is empty) keeps
    /// a dead or dismissed hero as Leader while the engine believes the change took. Keeps
    /// _warden in sync with whatever the engine (KillCharacterAction, disband flows,
    /// RefugeService.AttachWarden's ChangePartyLeader) installs, null included.</summary>
    protected override void OnChangePartyLeader(Hero newLeader) => _warden = newLeader;

    // A refuge never initiates anything; it is attacked, it does not attack. NOTE the narrow
    // reality of this flag on 1.4.8: AvoidHostileActions is consulted at exactly TWO vanilla call
    // sites - PlayerEncounter's narrative-text formatting and ApplyEncounterHostileAction's
    // relation-penalty gate. It does NOT stop hostile AI from choosing to attack this party
    // (target selection never reads it); the refuge's own passivity comes from the pinned AI
    // (SetMoveModeHold + SetDoNotMakeNewDecisions in RefugeService). Do not rely on this flag as
    // a combat-prevention gate.
    public override bool AvoidHostileActions => true;

    public override Banner GetDefaultComponentBanner() => Hero.MainHero?.ClanBanner;
}
