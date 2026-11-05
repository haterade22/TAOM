using System.Collections.Generic;

namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// Pure decision logic for Castle Recruitment — no TaleWorlds types. The behavior boundary maps
/// <see cref="CastleNotableOccupation"/> to the engine occupation and applies these decisions.
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
    /// Garrisons are out of scope: they use MaximumIndexGarrisonCanRecruitFromHero.</summary>
    bool BlocksAiRecruitAtCastle(bool sellerInCastle, bool buyerIsPlayer);

    /// <summary>Target notable headcount per castle, distributed round-robin across the four
    /// castle-safe occupations. e.g. 3 → {GangLeader:1, Headman:1, Merchant:1};
    /// 5 → {GangLeader:2, Headman:1, Merchant:1, Artisan:1}.</summary>
    IReadOnlyDictionary<CastleNotableOccupation, int> GetOccupationTargets();

    /// <summary>Per-slot daily probability that a notable produces/upgrades a volunteer in
    /// volunteer slot <paramref name="slotIndex"/> (0..5). A monotonically-decreasing curve
    /// mirroring vanilla's shape; pure so it is unit-testable and castle-safe (vanilla's own
    /// production model NREs for castles).</summary>
    float GetSlotProductionProbability(int slotIndex);
}
