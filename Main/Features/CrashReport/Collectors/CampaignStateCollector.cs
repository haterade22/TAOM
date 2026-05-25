using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class CampaignStateCollector
{
    private readonly Func<IReadOnlyList<CampaignEventEntry>> _recentEventsSnapshot;

    public CampaignStateCollector(Func<IReadOnlyList<CampaignEventEntry>> recentEventsSnapshot)
    {
        _recentEventsSnapshot = recentEventsSnapshot;
    }

    public CampaignSnapshot? Collect()
    {
        Campaign? campaign;
        try { campaign = Campaign.Current; } catch { return null; }
        if (campaign == null) return null;
        bool started = false;
        try { started = campaign.GameStarted; } catch { }
        if (!started) return null;

        string? gameId = SafeStr(() => campaign.UniqueGameId);
        string? timeNow = SafeStr(() => CampaignTime.Now.ToString());
        int? day = SafeInt(() => CampaignTime.Now.GetDayOfSeason);
        int? year = SafeInt(() => CampaignTime.Now.GetYear);
        string? season = SafeStr(() => CampaignTime.Now.GetSeasonOfYear.ToString());
        string? lastSave = SafeStr(() => campaign.LastTimeControlMode.ToString()); // best-effort surrogate; engine has no explicit "last save time"
        // v1.4.5: SaveHandler.AutoSaveInterval > -1 means autosave is enabled
        // (per SaveHandler._isAutoSaveEnabled implementation). Deep-review GAP 1 fix.
        bool? autosaveOn = null;
        try
        {
            var interval = campaign.SaveHandler?.AutoSaveInterval;
            if (interval.HasValue) autosaveOn = interval.Value > -1;
        }
        catch { }

        var hero = SafeRead(() => campaign.MainParty?.LeaderHero) ?? SafeRead(() => Hero.MainHero);
        HeroSnapshot? heroSnap = hero != null ? CollectHero(hero) : null;

        PartySnapshot? partySnap = SafeRead(() => campaign.MainParty) is MobileParty mp ? CollectParty(mp) : null;
        SettlementContextSnapshot? locSnap = CollectLocation(campaign, hero);

        IReadOnlyList<CampaignEventEntry> events;
        try { events = _recentEventsSnapshot(); } catch { events = Array.Empty<CampaignEventEntry>(); }

        return new CampaignSnapshot(
            UniqueGameId: gameId,
            CampaignTimeNow: timeNow,
            DayOfSeason: day,
            Year: year,
            Season: season,
            LastSaveTimestampUtc: lastSave,
            AutosaveEnabled: autosaveOn,
            PlayerHero: heroSnap,
            PlayerParty: partySnap,
            CurrentLocation: locSnap,
            RecentEvents: events);
    }

    private static HeroSnapshot CollectHero(Hero h)
    {
        string name = SafeStr(() => h.Name?.ToString()) ?? "(unknown)";
        int level = SafeIntNonNull(() => h.Level);
        int age = SafeIntNonNull(() => (int)h.Age);
        string? culture = SafeStr(() => h.Culture?.StringId);
        string? clan = SafeStr(() => h.Clan?.StringId);
        string? kingdom = SafeStr(() => h.Clan?.Kingdom?.StringId);
        int gold = SafeIntNonNull(() => h.Gold);
        int renown = SafeIntNonNull(() => h.Clan != null ? (int?)h.Clan.Renown : null);
        int influence = SafeIntNonNull(() => h.Clan != null ? (int?)h.Clan.Influence : null);
        string? race = SafeStr(() => h.CharacterObject?.Race.ToString());
        return new HeroSnapshot(name, level, age, culture, clan, kingdom, gold, renown, influence, race);
    }

    private static PartySnapshot CollectParty(MobileParty mp)
    {
        int members = SafeIntNonNull(() => mp.MemberRoster?.TotalManCount);
        int wounded = SafeIntNonNull(() => mp.MemberRoster?.TotalWounded);
        int prisoners = SafeIntNonNull(() => mp.PrisonRoster?.TotalManCount);
        int morale = SafeIntNonNull(() => (int)mp.Morale);
        int food = SafeIntNonNull(() => (int)mp.Food);

        var histo = new List<TroopTierEntry>();
        try
        {
            var roster = mp.MemberRoster;
            if (roster != null)
            {
                var byTier = new Dictionary<int, int>();
                int count = roster.Count;
                for (int i = 0; i < count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    int tier = el.Character?.Tier ?? 0;
                    int n = el.Number;
                    byTier.TryGetValue(tier, out var existing);
                    byTier[tier] = existing + n;
                }
                histo = byTier.OrderBy(kv => kv.Key).Select(kv => new TroopTierEntry(kv.Key, kv.Value)).ToList();
            }
        }
        catch { }

        return new PartySnapshot(members, wounded, prisoners, morale, food, histo);
    }

    private static SettlementContextSnapshot? CollectLocation(Campaign campaign, Hero? hero)
    {
        try
        {
            var settlement = hero?.CurrentSettlement ?? campaign.MainParty?.CurrentSettlement;
            if (settlement != null)
            {
                return new SettlementContextSnapshot(
                    SettlementId: SafeStr(() => settlement.StringId),
                    SettlementName: SafeStr(() => settlement.Name?.ToString()),
                    SettlementCulture: SafeStr(() => settlement.Culture?.StringId),
                    IsOnMap: false,
                    MapPositionX: 0f,
                    MapPositionY: 0f);
            }
            var party = campaign.MainParty;
            float x = 0, y = 0;
            try { if (party != null) { var p = party.Position; x = p.X; y = p.Y; } } catch { }
            return new SettlementContextSnapshot(
                SettlementId: null,
                SettlementName: null,
                SettlementCulture: null,
                IsOnMap: true,
                MapPositionX: x,
                MapPositionY: y);
        }
        catch { return null; }
    }

    private static T? SafeRead<T>(Func<T?> f) where T : class { try { return f(); } catch { return null; } }
    private static string? SafeStr(Func<string?> f) { try { return f(); } catch { return null; } }
    private static int? SafeInt(Func<int> f) { try { return f(); } catch { return null; } }
    private static int SafeIntNonNull(Func<int?> f) { try { return f() ?? 0; } catch { return 0; } }
}
