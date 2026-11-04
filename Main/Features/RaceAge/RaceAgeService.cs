using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Features.RaceAge.Models;

namespace TAOM.Features.RaceAge;

public class RaceAgeService : IRaceAgeService
{
    private readonly IRaceManager _raceManager;
    private readonly Dictionary<string, RaceAgeEntry> _races;
    private readonly Dictionary<int, RaceAgeEntry> _raceIdCache = new Dictionary<int, RaceAgeEntry>();
    private readonly RaceAgeEntry _defaultEntry;

    public RaceAgeService(IRaceAgeConfigProvider configProvider, IRaceManager raceManager)
    {
        _raceManager = raceManager;

        var config = configProvider.LoadConfig();
        _races = config.Races;

        if (!_races.TryGetValue(config.DefaultRace, out _defaultEntry))
        {
            _defaultEntry = new RaceAgeEntry();
        }
    }

    public int GetMaxAge(int raceId) => GetEntry(raceId).MaxAge;
    public int GetBecomeOldAge(int raceId) => GetEntry(raceId).BecomeOld;
    public int GetComesOfAge(int raceId) => GetEntry(raceId).ComesOfAge;
    public int GetMiddleAge(int raceId) => GetEntry(raceId).MiddleAge;
    public int GetFertilityEndAge(int raceId) => GetEntry(raceId).FertilityEnd;
    public float GetFertilityModifier(int raceId) => GetEntry(raceId).FertilityMod;
    public bool IsImmortal(int raceId) => GetEntry(raceId).Immortal;

    public bool ShouldDieOfOldAge(int raceId, float currentAge)
    {
        var entry = GetEntry(raceId);
        if (entry.Immortal) return false;
        return currentAge > entry.MaxAge;
    }

    public bool CanConceive(int raceId, int spouseRaceId, float heroAge)
    {
        if (IsImmortal(raceId))
            return false;

        // The engine only ever passes the FEMALE to the pregnancy model
        // (PregnancyCampaignBehavior.DailyTickHero gates on hero.IsFemale), so an immortal FATHER
        // (Sauron, Saruman, a wraith) must be blocked via the spouse's race or his immortal flag
        // never gates conception (#321).
        if (IsImmortal(spouseRaceId))
            return false;

        return heroAge >= GetComesOfAge(raceId) && heroAge <= GetFertilityEndAge(raceId);
    }

    public float GetDailyPregnancyBaseChance(
        int raceId, float heroAge, int childCount, int clanTier,
        int aliveLords, bool playerOrSpouseInvolved)
    {
        return ComputeBaseChance(
            heroAge: heroAge,
            comesOfAge: GetComesOfAge(raceId),
            fertilityEnd: GetFertilityEndAge(raceId),
            childCount: childCount,
            clanTier: clanTier,
            aliveLords: aliveLords,
            playerOrSpouseInvolved: playerOrSpouseInvolved,
            raceFertilityModifier: GetFertilityModifier(raceId));
    }

    /// <summary>
    /// Pure-math reimplementation of vanilla DefaultPregnancyModel with race-specific age bounds.
    /// Vanilla hardcodes age 18-45; we replace with the race's comesOfAge..fertilityEnd window.
    ///
    /// Public static so its branches (the Phase 7 audit, #179) are testable without the service.
    /// </summary>
    public static float ComputeBaseChance(
        float heroAge,
        int comesOfAge,
        int fertilityEnd,
        int childCount,
        int clanTier,
        int aliveLords,
        bool playerOrSpouseInvolved,
        float raceFertilityModifier)
    {
        int fertilityWindow = fertilityEnd - comesOfAge;
        float declineRate = fertilityWindow > 0 ? 1.08f / fertilityWindow : 0.04f;
        float ageFactor = 1.2f - (heroAge - comesOfAge) * declineRate;

        int effectiveChildCount = childCount + 1;
        float clanCap = 4 + 4 * clanTier;
        float populationFactor = !playerOrSpouseInvolved
            ? Math.Min(1f, (2f * clanCap - aliveLords) / clanCap)
            : 1f;

        // #628: a race bonus applies only while the clan is at or under its cap. Past the cap the
        // brake wins, and a player marriage (which skips the brake, as in vanilla) never takes the
        // bonus either. A penalty (elves, dwarves) applies everywhere. Pre-fix, orc clans at x2 bred at
        // the unbraked vanilla rate at 1.5x their cap and sat at the 2*cap ceiling.
        bool bonusAllowed = !playerOrSpouseInvolved && aliveLords <= clanCap;
        float effectiveModifier = bonusAllowed ? raceFertilityModifier : Math.Min(raceFertilityModifier, 1f);

        float baseChance = ageFactor / (effectiveChildCount * effectiveChildCount) * 0.12f * populationFactor;
        baseChance *= effectiveModifier;
        return baseChance;
    }

    private RaceAgeEntry GetEntry(int raceId)
    {
        if (_raceIdCache.TryGetValue(raceId, out var cached))
            return cached;

        // Phase 9b #131 P2 — validate BEFORE the lookup. _raceManager.GetRaceNameFromId returns
        // "human" as fallback for unknown IDs (RaceManager.cs:126-131). Without this guard, an
        // invalid raceId would resolve to the human RaceAgeEntry — silently treating dwarves/elves
        // with corrupted IDs as humans for ALL age + fertility calculations. See
        // feedback_validate_before_lookup_with_fallback.md (Codex review #33).
        if (!_raceManager.IsValidRaceId(raceId))
        {
            _raceIdCache[raceId] = _defaultEntry;
            return _defaultEntry;
        }

        var raceName = _raceManager.GetRaceNameFromId(raceId);
        var entry = (raceName != null && _races.TryGetValue(raceName, out var found))
            ? found
            : _defaultEntry;

        _raceIdCache[raceId] = entry;
        return entry;
    }

    // Phase 9b #131 P1 R1 — clear cache between campaigns. If race integer IDs shift (HeroRace #130
    // would have caused this), cache served entries mapped from old integer→name pairings to wrong
    // RaceAgeEntry. Now exposed for the behavior to call on session launch.
    public void ResetCache()
    {
        _raceIdCache.Clear();
    }
}
