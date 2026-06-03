using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ICultureConversionAdapter"/>. Resolves settlements by
/// <c>Settlement.Find</c> (same as <c>NamedCompanionAdapter</c>) and delegates culture-id resolution
/// to the existing <see cref="ICultureObjectAdapter"/>. Computed TaleWorlds properties use <c>?.</c>
/// per the adapter rules (they can throw before a plain null check).
/// </summary>
public class CultureConversionAdapter : ICultureConversionAdapter
{
    private readonly ICultureObjectAdapter _cultureObjectAdapter;
    private readonly IModLogger _logger;

    public CultureConversionAdapter(ICultureObjectAdapter cultureObjectAdapter, IModLogger logger)
    {
        _cultureObjectAdapter = cultureObjectAdapter;
        _logger = logger;
    }

    public bool IsFortification(string settlementId)
        => Settlement.Find(settlementId)?.IsFortification ?? false;

    public bool IsPlayerOwned(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        return settlement?.OwnerClan != null && settlement.OwnerClan == Clan.PlayerClan;
    }

    public string GetCurrentCultureId(string settlementId)
        => Settlement.Find(settlementId)?.Culture?.StringId;

    public string GetOwnerCultureId(string settlementId)
        => Settlement.Find(settlementId)?.OwnerClan?.Culture?.StringId;

    public float? GetLoyalty(string settlementId)
    {
        var town = Settlement.Find(settlementId)?.Town;
        return town?.Loyalty;
    }

    public IReadOnlyList<string> GetBoundVillageSettlementIds(string settlementId)
    {
        var result = new List<string>();
        var settlement = Settlement.Find(settlementId);
        var villages = settlement?.BoundVillages;
        if (villages == null)
            return result;

        foreach (var village in villages)
        {
            var villageSettlementId = village?.Settlement?.StringId;
            if (!string.IsNullOrEmpty(villageSettlementId))
                result.Add(villageSettlementId);
        }
        return result;
    }

    public bool SetSettlementCulture(string settlementId, string cultureId)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement == null)
        {
            _logger.LogWarning($"CultureConversionAdapter: cannot set culture — settlement '{settlementId}' not found");
            return false;
        }

        if (!(_cultureObjectAdapter.ResolveCulture(cultureId) is CultureObject culture))
        {
            _logger.LogWarning($"CultureConversionAdapter: cannot resolve culture '{cultureId}' for settlement '{settlementId}'");
            return false;
        }

        settlement.Culture = culture;
        return true;
    }

    public void ResetVolunteers(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement == null)
            return;

        foreach (Hero notable in settlement.Notables)
        {
            if (notable == null || !notable.IsAlive || !notable.CanHaveRecruits)
                continue;
            var slots = notable.VolunteerTypes;
            if (slots == null)
                continue;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = null;
        }
    }
}
