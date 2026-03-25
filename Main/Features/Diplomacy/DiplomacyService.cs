using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public class DiplomacyService : IDiplomacyService
{
    private readonly IAllianceAdapter _allianceAdapter;
    private readonly IModLogger _logger;
    private readonly Dictionary<(string, string), AllianceTier> _relationships;
    private readonly List<KingdomRelationship> _permanentRelationships;

    public DiplomacyService(
        IDiplomacyConfigProvider configProvider,
        IAllianceAdapter allianceAdapter,
        IModLogger logger)
    {
        _allianceAdapter = allianceAdapter;
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

    public void EstablishInitialAlliances()
    {
        foreach (var rel in _permanentRelationships)
        {
            if (!_allianceAdapter.AreAllied(rel.KingdomA, rel.KingdomB))
            {
                _logger.LogInfo($"Establishing initial alliance: {rel.KingdomA} <-> {rel.KingdomB}");
                _allianceAdapter.StartAlliance(rel.KingdomA, rel.KingdomB);
            }
        }
    }

    public void EnforcePermanentAlliances()
    {
        foreach (var rel in _permanentRelationships)
        {
            if (!_allianceAdapter.AreAllied(rel.KingdomA, rel.KingdomB))
            {
                _logger.LogWarning($"Permanent alliance missing, restoring: {rel.KingdomA} <-> {rel.KingdomB}");
                _allianceAdapter.StartAlliance(rel.KingdomA, rel.KingdomB);
            }
        }
    }

    private static (string, string) MakeKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0 ? (a, b) : (b, a);
    }
}
