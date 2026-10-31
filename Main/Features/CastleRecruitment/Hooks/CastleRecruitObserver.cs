using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Engine glue for the #918 in-game check. Every AI lord's volunteer recruit in vanilla v1.5.6 raises
/// OnTroopRecruitedEvent from RecruitmentCampaignBehavior.ApplyInternal (the garrison auto-recruit raises
/// none); this reads four flags from it and hands them to <see cref="ICastleRecruitmentService"/>, which
/// tells an AI lord's recruit from the rest and keeps the tally, and at each daily tick asks the service for the
/// [Check] line of the window since the previous daily tick. Built anew with the behavior at each campaign
/// start or load, so the tally restarts with the campaign; after a reload in the same process the lines of
/// every day already logged there are not written again (the line's key is the day). Main thread only.
/// </summary>
internal sealed class CastleRecruitObserver
{
    private readonly ICastleRecruitmentService _service;

    public CastleRecruitObserver(ICastleRecruitmentService service)
    {
        _service = service;
        service.BeginCastleRecruitDays();
    }

    internal void Register(object owner)
    {
        CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(owner, OnTroopRecruited);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(owner, OnDailyTick);
    }

    // Runs for every lord's volunteer, mercenary and prisoner recruit on the map: the non-castle path reads two
    // properties and returns.
    private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero recruitmentSource, CharacterObject troop, int count)
    {
        if (!_service.IsAiCastleRecruit(settlement?.IsCastle == true, recruitmentSource != null,
                recruiter == null, recruiter == Hero.MainHero))
            return;
        TryGetDay(out var day); // day 0 when the clock is unreadable: the key only dedups
        _service.NoteCastleRecruit(day, settlement?.StringId, recruiter?.StringId, recruitmentSource?.StringId, troop?.StringId);
    }

    private void OnDailyTick()
    {
        if (TryGetDay(out var day))
            _service.ReportCastleRecruitDay(day);
    }

    private static bool TryGetDay(out int day) => KingdomWarSnapshotAdapter.TryFloorElapsedDays(
        Campaign.Current?.Models?.CampaignTimeModel?.CampaignStartTime.ElapsedDaysUntilNow, out day);
}
