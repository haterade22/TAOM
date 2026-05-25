using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

// Populated only when Campaign.Current != null AND GameStarted == true.
// All sub-records are nullable so a partially-loaded campaign still snapshots.
public sealed record CampaignSnapshot(
    string? UniqueGameId,
    string? CampaignTimeNow,
    int? DayOfSeason,
    int? Year,
    string? Season,
    string? LastSaveTimestampUtc,
    bool? AutosaveEnabled,
    HeroSnapshot? PlayerHero,
    PartySnapshot? PlayerParty,
    SettlementContextSnapshot? CurrentLocation,
    IReadOnlyList<CampaignEventEntry> RecentEvents);

public sealed record HeroSnapshot(
    string Name,
    int Level,
    int Age,
    string? CultureId,
    string? ClanId,
    string? KingdomId,
    int Gold,
    int Renown,
    int Influence,
    string? Race);

public sealed record PartySnapshot(
    int MemberCount,
    int WoundedCount,
    int PrisonerCount,
    int Morale,
    int Food,
    IReadOnlyList<TroopTierEntry> TroopHistogram);

public sealed record TroopTierEntry(int Tier, int Count);

public sealed record SettlementContextSnapshot(
    string? SettlementId,
    string? SettlementName,
    string? SettlementCulture,
    bool IsOnMap,
    float MapPositionX,
    float MapPositionY);

public sealed record CampaignEventEntry(
    string CapturedAtUtc,
    string EventName);
