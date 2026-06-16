using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;

namespace TAOM.Features.Diplomacy;

public class DiplomacyService : IDiplomacyService
{
    private readonly IAllianceAdapter _allianceAdapter;
    private readonly IAlignmentService _alignmentService;
    private readonly IModLogger _logger;
    private readonly Dictionary<(string, string), AllianceTier> _relationships;
    private readonly List<KingdomRelationship> _permanentRelationships;

    public DiplomacyService(
        IDiplomacyConfigProvider configProvider,
        IAllianceAdapter allianceAdapter,
        IAlignmentService alignmentService,
        IModLogger logger)
    {
        _allianceAdapter = allianceAdapter;
        _alignmentService = alignmentService;
        _logger = logger;
        _relationships = new Dictionary<(string, string), AllianceTier>();
        _permanentRelationships = new List<KingdomRelationship>();

        var config = configProvider.LoadConfig();
        foreach (var rel in config.Relationships)
        {
            var key = MakeKey(rel.KingdomA, rel.KingdomB);
            _relationships[key] = rel.Tier;

            if (rel.Tier == AllianceTier.Permanent)
            {
                _permanentRelationships.Add(rel);
            }
        }
    }

    public AllianceTier GetRelationshipTier(string kingdomAId, string kingdomBId)
    {
        var key = MakeKey(kingdomAId, kingdomBId);
        return _relationships.TryGetValue(key, out var tier) ? tier : AllianceTier.Neutral;
    }

    // Large enough that base.GetScoreOfStartingAlliance (≈0 for a new player kingdom)
    // + this clears the vanilla CanMakeAlliance threshold (>= 50f), so AI kingdoms
    // will offer alliances to the player regardless of borders / neighbor threat.
    private const float PlayerAllianceScoreBonus = 1000f;

    public float GetAllianceScoreModifier(string kingdomAId, string kingdomBId)
    {
        return GetAllianceScoreModifier(kingdomAId, kingdomBId, involvesPlayer: false);
    }

    public float GetAllianceScoreModifier(string kingdomAId, string kingdomBId, bool involvesPlayer)
    {
        if (involvesPlayer)
            return PlayerAllianceScoreBonus;

        var tier = GetRelationshipTier(kingdomAId, kingdomBId);
        switch (tier)
        {
            case AllianceTier.Permanent: return 1000f;
            case AllianceTier.Natural: return 500f;
            case AllianceTier.Hostile: return -10000f;
            default: return 0f;
        }
    }

    public bool IsAllianceAllowed(string kingdomAId, string kingdomBId)
    {
        return GetRelationshipTier(kingdomAId, kingdomBId) != AllianceTier.Hostile;
    }

    public bool IsAllianceDecisionAllowed(string kingdomAId, string kingdomBId, bool involvesPlayer)
    {
        // Full freedom: lore Hostile never blocks a decision involving the player.
        return involvesPlayer || IsAllianceAllowed(kingdomAId, kingdomBId);
    }

    public bool CanPlayerProposeAlliance(string playerKingdomId, string targetKingdomId)
    {
        if (string.IsNullOrEmpty(playerKingdomId) || string.IsNullOrEmpty(targetKingdomId))
            return false;
        if (playerKingdomId == targetKingdomId)
            return false;
        if (_allianceAdapter.AreAtWar(playerKingdomId, targetKingdomId))
            return false;
        if (_allianceAdapter.AreAllied(playerKingdomId, targetKingdomId))
            return false;
        return true;
    }

    public bool FormPlayerAlliance(string playerKingdomId, string targetKingdomId)
    {
        if (!CanPlayerProposeAlliance(playerKingdomId, targetKingdomId))
            return false;

        _logger.LogInfo($"[Diplomacy] Player-initiated alliance: {playerKingdomId} <-> {targetKingdomId}");
        _allianceAdapter.StartAlliance(playerKingdomId, targetKingdomId);

        // Confirm the engine actually formed it (StartAlliance no-ops on a missing kingdom).
        bool formed = _allianceAdapter.AreAllied(playerKingdomId, targetKingdomId);
        if (!formed)
            _logger.LogWarning($"[Diplomacy] Player-initiated alliance had no effect: {playerKingdomId} <-> {targetKingdomId}");
        return formed;
    }

    public bool IsWarAllowed(string kingdomAId, string kingdomBId)
    {
        if (GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Permanent)
            return false;
        if (_alignmentService.AreSameAlignment(kingdomAId, kingdomBId))
            return false;
        return true;
    }

    public void EstablishInitialAlliances()
    {
        int created = 0, alreadyAllied = 0, failed = 0;
        foreach (var rel in _permanentRelationships)
        {
            if (_allianceAdapter.AreAllied(rel.KingdomA, rel.KingdomB))
            {
                alreadyAllied++;
                continue;
            }
            _logger.LogInfo($"Establishing initial alliance: {rel.KingdomA} <-> {rel.KingdomB}");
            _allianceAdapter.StartAlliance(rel.KingdomA, rel.KingdomB);

            // DR2 diagnostic (2026-05-22): verify the call actually took effect.
            // Hypothesis: at index=0 of OnNewGameCreatedPartialFollowUpEvent,
            // IAllianceCampaignBehavior may not be registered yet, so the
            // AllianceAdapter's `behavior?.StartAlliance(...)` would silently
            // null-noop. If that's the case, the post-call AreAllied returns
            // false here even though we logged "Establishing".
            if (_allianceAdapter.AreAllied(rel.KingdomA, rel.KingdomB))
            {
                created++;
            }
            else
            {
                failed++;
                _logger.LogWarning(
                    $"[Diplomacy] StartAlliance had no effect for {rel.KingdomA} <-> {rel.KingdomB} " +
                    "— IAllianceCampaignBehavior likely not yet registered at this lifecycle phase. " +
                    "Will retry via OnSessionLaunched/EnforcePermanentAlliances.");
            }
        }
        _logger.LogInfo(
            $"[Diplomacy] EstablishInitialAlliances summary: {created} created, " +
            $"{alreadyAllied} already-allied, {failed} silent-noop (will retry)");
    }

    public void EnforcePermanentAlliances()
    {
        int restored = 0, stillMissing = 0, alreadyAllied = 0, warsEnded = 0, warsEndedWhileAllied = 0;
        foreach (var rel in _permanentRelationships)
        {
            // Two independent invariants for a Permanent pair:
            //   1. NOT at war
            //   2. Allied
            // Vanilla initial-state setup can declare wars AFTER EstablishInitialAlliances
            // ran during OnNewGameCreatedPartialFollowUp, producing an allied-AND-at-war
            // state. Each invariant must be checked independently — short-circuiting on
            // AreAllied=true (as the earlier impl did) skipped war cleanup for that case
            // and left Mordor visibly at war with Harad in the encyclopedia while the
            // alliance entry simultaneously listed them as allied.
            var atWar = _allianceAdapter.AreAtWar(rel.KingdomA, rel.KingdomB);
            var allied = _allianceAdapter.AreAllied(rel.KingdomA, rel.KingdomB);

            if (atWar)
            {
                _logger.LogWarning(
                    $"[Diplomacy] Permanent pair at war (allied={allied}), making peace: " +
                    $"{rel.KingdomA} <-> {rel.KingdomB}");
                _allianceAdapter.MakePeace(rel.KingdomA, rel.KingdomB);
                warsEnded++;
                if (allied) warsEndedWhileAllied++;
            }

            if (allied)
            {
                alreadyAllied++;
                continue;
            }

            _logger.LogWarning($"Permanent alliance missing, restoring: {rel.KingdomA} <-> {rel.KingdomB}");
            _allianceAdapter.StartAlliance(rel.KingdomA, rel.KingdomB);

            if (_allianceAdapter.AreAllied(rel.KingdomA, rel.KingdomB))
                restored++;
            else
                stillMissing++;
        }
        _logger.LogInfo(
            $"[Diplomacy] EnforcePermanentAlliances summary: {alreadyAllied} already-allied " +
            $"({warsEndedWhileAllied} of which were also at war — vanilla state corruption corrected), " +
            $"{restored} alliances restored, {warsEnded} total wars ended, " +
            $"{stillMissing} STILL MISSING (vanilla StartAlliance rejected the call)");
    }

    private static (string, string) MakeKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0 ? (a, b) : (b, a);
    }
}
