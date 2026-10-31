using System.Collections.Generic;

namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// Pure decision logic for Castle Recruitment — no TaleWorlds types. The behavior boundary maps
/// <see cref="CastleNotableOccupation"/> to the engine occupation and applies these decisions.
/// It also keeps the #918 day tally and writes its <c>[Check] id=taom.castle-recruit.ai-off</c> line.
/// </summary>
public interface ICastleRecruitmentService
{
    /// <summary>Master toggle (MCM/JSON).</summary>
    bool IsEnabled { get; }

    /// <summary>AI castle recruitment toggle, already AND-ed with the master toggle.</summary>
    bool IsAiEnabled { get; }

    /// <summary>True when an AI lord must not recruit from this notable: the seller stands in a castle,
    /// the buyer is not the player, and AI castle recruitment is off (the master toggle is folded into
    /// <see cref="IsAiEnabled"/>). The one chokepoint for every AI lord recruit from a castle notable,
    /// on entry through vanilla's OnBeforeSettlementEntered and hourly through CastleAiRecruitDispatcher.
    /// Garrisons are out of scope: they use MaximumIndexGarrisonCanRecruitFromHero.
    /// A true answer counts <c>taom.castle-recruit.ai-blocked</c> (a counter: this is a model query).</summary>
    bool BlocksAiRecruitAtCastle(bool sellerInCastle, bool buyerIsPlayer);

    /// <summary>Who took a volunteer in one OnTroopRecruited event: <see cref="CastleRecruitKind.None"/> unless the
    /// settlement is a castle and a notable was the source, then a null recruiter is a garrison, the main hero is
    /// the player, and anyone else is an AI lord.</summary>
    CastleRecruitKind ClassifyCastleRecruit(bool settlementIsCastle, bool hasSource, bool recruiterIsNull, bool recruiterIsPlayer);

    /// <summary>Zeroes the #918 day tally at a campaign start or load, so counts never carry between saves.</summary>
    void BeginCastleRecruitDays();

    /// <summary>Tallies one classified recruit; only an AI lord's counts. An AI recruit while AI castle recruitment is off writes a fail
    /// line at once (key <c>ai-&lt;day&gt;</c>) naming the castle, the lord, the notable and the troop.</summary>
    void NoteCastleRecruit(CastleRecruitKind kind, int day, string? settlementId, string? recruiterId, string? sourceId, string? troopId);

    /// <summary>The day's line: skip <c>reason=toggle-on</c> while AI castle recruitment is on, else fail when
    /// an AI lord recruited at a castle that day, else pass; then starts a new day. Key: the campaign day.</summary>
    void ReportCastleRecruitDay(int day);

    /// <summary>Target notable headcount per castle, distributed round-robin across the four
    /// castle-safe occupations. e.g. 3 → {GangLeader:1, Headman:1, Merchant:1};
    /// 5 → {GangLeader:2, Headman:1, Merchant:1, Artisan:1}.</summary>
    IReadOnlyDictionary<CastleNotableOccupation, int> GetOccupationTargets();

    /// <summary>Per-slot daily probability that a notable produces/upgrades a volunteer in
    /// volunteer slot <paramref name="slotIndex"/> (0..5). A monotonically-decreasing curve
    /// mirroring vanilla's shape; pure so it is unit-testable and castle-safe (vanilla's own
    /// production model NREs for castles). The curve is then scaled by the War Chronicle's
    /// VolunteerRate multiplier of <paramref name="kingdomKey"/> (the owner clan's kingdom id, null
    /// for none), except for a castle of the player's own clan.</summary>
    float GetSlotProductionProbability(int slotIndex, string? kingdomKey, bool ownerIsPlayerClan);
}
