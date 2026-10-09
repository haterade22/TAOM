using System;
using System.Collections.Generic;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.CastleRecruitment;

/// <inheritdoc cref="ICastleRecruitmentService"/>
public class CastleRecruitmentService : ICastleRecruitmentService
{
    // Round-robin order. RuralNotable is intentionally absent (castle-NRE in vanilla GetBasicVolunteer).
    private static readonly CastleNotableOccupation[] OccupationOrder =
    {
        CastleNotableOccupation.GangLeader,
        CastleNotableOccupation.Headman,
        CastleNotableOccupation.Merchant,
        CastleNotableOccupation.Artisan,
    };

    private readonly ICastleRecruitmentSettingsProvider _settings;
    private readonly VolunteerProductionService _production;

    public CastleRecruitmentService(ICastleRecruitmentSettingsProvider settings, VolunteerProductionService production)
    {
        _settings = settings;
        _production = production;
    }

    public bool IsEnabled => _settings.IsEnabled;

    public bool IsAiEnabled => _settings.IsEnabled && _settings.IsAiEnabled;

    public IReadOnlyDictionary<CastleNotableOccupation, int> GetOccupationTargets()
    {
        var dict = new Dictionary<CastleNotableOccupation, int>();
        int total = _settings.NotablesPerCastle;
        if (total < 0)
            total = 0;
        for (int i = 0; i < total; i++)
        {
            var occ = OccupationOrder[i % OccupationOrder.Length];
            dict[occ] = dict.TryGetValue(occ, out var count) ? count + 1 : 1;
        }
        return dict;
    }

    public float GetSlotProductionProbability(int slotIndex, string? kingdomKey, bool ownerIsPlayerClan)
    {
        if (slotIndex < 0)
            return 0f;
        // 0.75 * 0.85^(index+1): slot 0 ≈ 0.64, slot 5 ≈ 0.28. Mirrors vanilla's decaying shape
        // (0.75 * pow(~0.85, index+1)) without the faction-fief term that requires the engine
        // settlement graph (and NREs for castles).
        float p = 0.75f * (float)Math.Pow(0.85, slotIndex + 1);
        p = p < 0f ? 0f : p > 1f ? 1f : p;
        // The War Chronicle's VolunteerRate multiplier of the owner's kingdom (#765), as towns and
        // villages get it through TaomVolunteerModel. No culture: castles never had the respawn feats.
        return _production.Compute(p, null, kingdomKey, ownerIsPlayerClan);
    }
}
