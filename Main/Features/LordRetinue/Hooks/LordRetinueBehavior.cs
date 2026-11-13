using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.LordRetinue.Hooks;

/// <summary>
/// Thin entry point (ADR-002) for LordRetinue (#1122). Once a day an AI lord party that sits in a settlement
/// may take one idle adult hero of its own clan from that same settlement and give the hero an empty party
/// role, or send its retinue back when the clan could field another party. When a party's disband starts,
/// every hero but the leader leaves it. The engine reads and writes live in <see cref="LordRetinueWorld"/>
/// and every decision in <see cref="LordRetinueService"/>. Stateless: heroes, rosters and roles are saved by the
/// engine, so there is nothing to persist in <see cref="SyncData"/>.
/// </summary>
public sealed class LordRetinueBehavior : CampaignBehaviorBase
{
    private readonly LordRetinueService _service;
    private readonly LordRetinueWorld _world;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IPlayerControlQuery _playerControl;

    public LordRetinueBehavior(
        LordRetinueService service, LordRetinueWorld world,
        ICoopSessionProvider coopSession, IPlayerControlQuery playerControl)
    {
        _service = service;
        _world = world;
        _coopSession = coopSession;
        _playerControl = playerControl;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTickParty);
        CampaignEvents.OnPartyDisbandStartedEvent.AddNonSerializedListener(this, OnPartyDisbandStarted);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Stateless: see the class summary.
    }

    private void OnDailyTickParty(MobileParty party)
    {
        if (!IsAiLordParty(party))
            return;

        var snapshot = _world.ReadParty(party);
        if (snapshot == null)
            return;

        if (_service.ShouldRelease(snapshot))
        {
            _world.Release(party, snapshot.RetinueHeroIds);
            return;
        }

        if (!_service.ShouldEvaluate(snapshot))
            return;

        var pick = _service.Choose(snapshot, _world.ReadCandidates(party));
        if (pick != null)
            _world.Join(party, pick.HeroId, pick.Role);
    }

    // No enable switch here: heroes taken earlier stay aboard after the feature is turned off.
    private void OnPartyDisbandStarted(MobileParty party)
    {
        if (IsAiLordParty(party))
            _world.ReleaseAtDisband(party);
    }

    private bool IsAiLordParty(MobileParty? party)
    {
        if (party == null || !party.IsLordParty)
            return false;
        return RetinueGate.MayChange(
            party.IsLordParty, party.IsMainParty, _coopSession.IsAuthority, _coopSession.ShouldDeferToHost,
            _playerControl.OfParty(party), _playerControl.OfClan(party.ActualClan));
    }
}
