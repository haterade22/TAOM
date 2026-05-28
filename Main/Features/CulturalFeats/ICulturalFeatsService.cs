using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats;

/// <summary>
/// Centralises the per-feat dispatch logic for the 16 <c>Taom*Model</c>
/// overrides in <see cref="Models"/>. Each method takes a boundary-converted
/// <see cref="ICultureFeatAdapter"/> (or null when the source has no owning
/// culture) and an in-flight <see cref="ExplainedNumber"/> by-ref, applies any
/// matching cultural feats to that result, and returns.
///
/// Career-passive integration is intentionally NOT part of this service — those
/// remain owned by <c>ICareerPassiveService</c> and the model overrides still
/// call them directly at the boundary. The single-responsibility line:
/// <i>cultural feats only</i>.
///
/// All methods are no-ops when <paramref name="culture"/> is null. Issues #144,
/// #176.
/// </summary>
public interface ICulturalFeatsService
{
    // ── ArmyManagement ──────────────────────────────────────────────────
    /// <summary>Applies Rivendell + Gondor army-influence-award factors (additive).</summary>
    float ApplyArmyInfluenceAward(ICultureFeatAdapter? culture, float baseAward);

    /// <summary>Applies Rivendell, Gundabad, Dol Guldur, Mordor army-influence-COST factors (additive).</summary>
    int ApplyArmyInfluenceCost(ICultureFeatAdapter? culture, int baseCost);

    // ── PartySpeed ──────────────────────────────────────────────────────
    /// <summary>
    /// Applies the per-culture terrain movement-speed feats whose terrain matches
    /// <paramref name="terrain"/> (forest/snow/steppe/desert/plain/swamp), plus the
    /// Mordor night-speed feat when <paramref name="isNight"/>. Flat <c>AddFactor</c>
    /// of each matching feat's <c>EffectBonus</c>; no-op for <see cref="TerrainKind.None"/>.
    /// </summary>
    void ApplyTerrainSpeedFeats(ICultureFeatAdapter? culture, TerrainKind terrain, bool isNight, ref ExplainedNumber result);

    /// <summary>Applies the Rohan infantry-speed penalty when &gt;50% of the party is infantry.</summary>
    void ApplyRohanInfantryPenalty(ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result);

    // ── SettlementProsperity ───────────────────────────────────────────
    /// <summary>Applies Rivendell/Mirkwood/Gondor hearth-growth factors. Skipped when current change is negative.</summary>
    void ApplyHearthGrowthFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── SettlementMilitia ──────────────────────────────────────────────
    /// <summary>Applies Mirkwood/Dol Guldur veteran-militia spawn-chance bonuses (additive).</summary>
    void ApplyVeteranMilitiaFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── BuildingConstruction ───────────────────────────────────────────
    /// <summary>Applies Erebor/Lothlorien/Dol Guldur/Isengard construction-speed factors.</summary>
    void ApplyConstructionSpeedFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── VillageProduction ──────────────────────────────────────────────
    /// <summary>Applies Erebor production + Gundabad/Mordor grain-production factors.</summary>
    void ApplyVillageProductionFeats(ICultureFeatAdapter? culture, bool isGrain, ref ExplainedNumber result);

    // ── Caravan ────────────────────────────────────────────────────────
    /// <summary>Applies the Umbar cheaper-caravans factor to a base cost. Rounds to int via MathF.Round-equivalent banker rounding semantics matching the original model.</summary>
    int ApplyCaravanCost(ICultureFeatAdapter? culture, int baseCost);

    // ── BattleReward ───────────────────────────────────────────────────
    /// <summary>Applies the Umbar renown factor.</summary>
    void ApplyRenownFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── PartyTroopUpgrade ──────────────────────────────────────────────
    /// <summary>Applies the Isengard/Rohan mounted-upgrade-cost factors. No-op when troop is not mounted.</summary>
    void ApplyTroopUpgradeFeats(ICultureFeatAdapter? culture, bool isMounted, ref ExplainedNumber result);

    // ── PartySize ──────────────────────────────────────────────────────
    /// <summary>Applies Mordor/Gundabad/Dol Guldur/Isengard/Gondor party-size factors.</summary>
    void ApplyPartySizeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── FoodConsumption ────────────────────────────────────────────────
    /// <summary>Applies Rivendell/Mirkwood/Lothlorien/Dol Guldur food-consumption factors.</summary>
    void ApplyFoodConsumptionFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── SettlementLoyalty ──────────────────────────────────────────────
    /// <summary>Applies Gondor/Erebor/Lothlorien/Rivendell/Rohan loyalty bonuses (Add — not AddFactor).</summary>
    void ApplyLoyaltyFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── PartyMorale ────────────────────────────────────────────────────
    /// <summary>Applies Gondor/Rohan/Erebor/Mirkwood/Lothlorien morale bonuses (Add).</summary>
    void ApplyMoraleFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── Smithing ───────────────────────────────────────────────────────
    /// <summary>Applies Erebor/Isengard smithing-energy-cost factors.</summary>
    void ApplySmithingFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── ClanFinance (tariffs) ──────────────────────────────────────────
    /// <summary>Applies the Umbar tariff-income factor.</summary>
    void ApplyTariffIncomeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);

    // ── Raid ───────────────────────────────────────────────────────────
    /// <summary>Applies Mordor/Gundabad/Isengard raid-damage factors.</summary>
    void ApplyRaidDamageFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
}
