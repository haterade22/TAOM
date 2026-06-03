using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Boundary adapter (ADR-007) wrapping the sealed TaleWorlds settlement/culture/notable surface the
/// CultureConversion service needs. All access is by settlement <c>StringId</c> so the service stays
/// free of TaleWorlds types.
/// </summary>
public interface ICultureConversionAdapter
{
    /// <summary>True if the settlement is a town or castle (vanilla <c>Settlement.IsFortification</c>).</summary>
    bool IsFortification(string settlementId);

    /// <summary>True if the settlement is currently owned by the player's clan.</summary>
    bool IsPlayerOwned(string settlementId);

    /// <summary>The settlement's current culture id (reflects any applied override), or null.</summary>
    string GetCurrentCultureId(string settlementId);

    /// <summary>The current owner clan's culture id, or null (no owner / no clan / no culture).</summary>
    string GetOwnerCultureId(string settlementId);

    /// <summary>The town/castle loyalty (0–100), or null if the settlement is not a fortification.</summary>
    float? GetLoyalty(string settlementId);

    /// <summary>Settlement StringIds of the bound villages of a town/castle (empty if none).</summary>
    IReadOnlyList<string> GetBoundVillageSettlementIds(string settlementId);

    /// <summary>Sets <c>Settlement.Culture</c> to the resolved culture. Returns false if the id or culture can't be resolved.</summary>
    bool SetSettlementCulture(string settlementId, string cultureId);

    /// <summary>Clears every notable's volunteer slots so the daily refill repopulates from the new culture's pool.</summary>
    void ResetVolunteers(string settlementId);
}
