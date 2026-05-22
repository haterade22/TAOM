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

    public float GetAllianceScoreModifier(string kingdomAId, string kingdomBId)
    {
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
        int restored = 0, stillMissing = 0, ok = 0;
        foreach (var rel in _permanentRelationships)
        {
            if (_allianceAdapter.AreAllied(rel.KingdomA, rel.KingdomB))
            {
                ok++;
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
            $"[Diplomacy] EnforcePermanentAlliances summary: {ok} already-ok, " +
            $"{restored} restored, {stillMissing} STILL MISSING (vanilla StartAlliance rejected the call)");
    }

    private static (string, string) MakeKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0 ? (a, b) : (b, a);
    }
}
