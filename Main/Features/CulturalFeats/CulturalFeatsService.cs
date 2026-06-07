using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats;

/// <summary>
/// Concrete dispatch for <see cref="ICulturalFeatsService"/>. All methods are
/// pure (no side effects beyond mutating the by-ref <see cref="ExplainedNumber"/>),
/// no singleton lookups in the hot path, no per-call allocations beyond the
/// shared <see cref="CultureText"/>. Each method's structure mirrors the
/// original inline body of the corresponding <c>Taom*Model</c> override 1:1
/// to keep the behavior-preserving refactor reviewable line-by-line.
/// </summary>
public sealed class CulturalFeatsService : ICulturalFeatsService
{
    // Phase 9b #144 — preserved verbatim from the original models so the
    // ExplainedNumber description strings on the world-map tooltip stay
    // identical post-refactor. The `try` guard is for unit tests where the
    // TaleWorlds <c>GameTexts</c> static is uninitialised — the description
    // is a pure UI string and `Add`/`AddFactor` accept null descriptions.
    private static TextObject? _cultureText;
    private static bool _cultureTextResolved;
    private static TextObject? CultureText
    {
        get
        {
            if (_cultureTextResolved)
                return _cultureText;
            try { _cultureText = GameTexts.FindText("str_culture"); }
            catch { _cultureText = null; }
            _cultureTextResolved = true;
            return _cultureText;
        }
    }

    // ── ArmyManagement ──────────────────────────────────────────────────

    public float ApplyArmyInfluenceAward(ICultureFeatAdapter? culture, float baseAward)
    {
        if (culture == null)
            return baseAward;

        float result = baseAward;
        if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceFeat))
            result += baseAward * TaomCulturalFeats.RivendellArmyInfluenceFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.GondorArmyInfluenceFeat))
            result += baseAward * TaomCulturalFeats.GondorArmyInfluenceFeat.EffectBonus;
        return result;
    }

    public int ApplyArmyInfluenceCost(ICultureFeatAdapter? culture, int baseCost)
    {
        if (culture == null)
            return baseCost;

        float multiplier = 0f;
        if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.RivendellArmyInfluenceCostFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.GundabadArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.GundabadArmyInfluenceCostFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.MistyMountainOrcsArmyInfluenceCostFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.MordorArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.MordorArmyInfluenceCostFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.MirkwoodArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.MirkwoodArmyInfluenceCostFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.HaradArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.HaradArmyInfluenceCostFeat.EffectBonus;

        return multiplier != 0f ? (int)(baseCost * (1f + multiplier)) : baseCost;
    }

    // ── PartySpeed ──────────────────────────────────────────────────────

    public void ApplyTerrainSpeedFeats(
        ICultureFeatAdapter? culture, TerrainKind terrain, bool isNight, ref ExplainedNumber result)
    {
        if (culture == null)
            return;

        switch (terrain)
        {
            case TerrainKind.Forest:
                ApplyIfHas(culture, TaomCulturalFeats.MirkwoodForestSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.LothlorienForestSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.RivendellForestSpeedFeat, ref result);
                break;
            case TerrainKind.Snow:
                ApplyIfHas(culture, TaomCulturalFeats.EreborSnowSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.GundabadSnowSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.GoblinSnowSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.MistyMountainOrcsSnowSpeedFeat, ref result);
                break;
            case TerrainKind.Steppe:
                ApplyIfHas(culture, TaomCulturalFeats.KhandSteppeSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.RhunSteppeSpeedFeat, ref result);
                break;
            case TerrainKind.Desert:
                ApplyIfHas(culture, TaomCulturalFeats.UmbarDesertSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.HaradDesertSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.ShaghanaDesertSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.AbanissaDesertSpeedFeat, ref result);
                break;
            case TerrainKind.Plain:
                ApplyIfHas(culture, TaomCulturalFeats.MordorPlainSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.GondorPlainSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.RohanPlainSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.DalePlainSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.DunlandPlainSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.IsengardPlainSpeedFeat, ref result);
                break;
            case TerrainKind.Swamp:
                ApplyIfHas(culture, TaomCulturalFeats.MordorSwampSpeedFeat, ref result);
                ApplyIfHas(culture, TaomCulturalFeats.IsengardSwampSpeedFeat, ref result);
                break;
        }

        if (isNight)
            ApplyIfHas(culture, TaomCulturalFeats.MordorNightSpeedFeat, ref result);
    }

    private static void ApplyIfHas(ICultureFeatAdapter culture, FeatObject feat, ref ExplainedNumber result)
    {
        if (culture.HasFeat(feat))
            result.AddFactor(feat.EffectBonus, CultureText);
    }

    public void ApplyRohanInfantryPenalty(
        ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result)
    {
        if (culture == null || totalCount <= 0)
            return;
        if (!culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
            return;
        if (mountedCount * 2 < totalCount)
            result.AddFactor(TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus, CultureText);
    }

    // ── SettlementProsperity ───────────────────────────────────────────

    public void ApplyHearthGrowthFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;

        // Match the original model's guard: only buff/nerf growth when result is still
        // non-negative. Negative results are skipped wholesale per pre-refactor behavior.
        if (culture.HasFeat(TaomCulturalFeats.RivendellHearthGrowthFeat) && result.ResultNumber >= 0f)
            result.AddFactor(TaomCulturalFeats.RivendellHearthGrowthFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MirkwoodHearthGrowthFeat) && result.ResultNumber >= 0f)
            result.AddFactor(TaomCulturalFeats.MirkwoodHearthGrowthFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GondorHearthGrowthFeat) && result.ResultNumber >= 0f)
            result.AddFactor(TaomCulturalFeats.GondorHearthGrowthFeat.EffectBonus, CultureText);
    }

    // ── SettlementMilitia ──────────────────────────────────────────────

    public void ApplyVeteranMilitiaFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.MirkwoodMilitiaProductionFeat))
            result.Add(TaomCulturalFeats.MirkwoodMilitiaProductionFeat.EffectBonus);
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurMilitiaProductionFeat))
            result.Add(TaomCulturalFeats.DolGuldurMilitiaProductionFeat.EffectBonus);
    }

    // ── BuildingConstruction ───────────────────────────────────────────

    public void ApplyConstructionSpeedFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.EreborConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.EreborConstructionSpeedFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.LothlorienConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.LothlorienConstructionSpeedFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurConstructionSpeedFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.IsengardConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.IsengardConstructionSpeedFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsConstructionSpeedFeat.EffectBonus, CultureText);
    }

    // ── VillageProduction ──────────────────────────────────────────────

    public void ApplyVillageProductionFeats(ICultureFeatAdapter? culture, bool isGrain, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.EreborProductionFeat))
            result.AddFactor(TaomCulturalFeats.EreborProductionFeat.EffectBonus, CultureText);
        if (isGrain && culture.HasFeat(TaomCulturalFeats.GundabadGrainProductionFeat))
            result.AddFactor(TaomCulturalFeats.GundabadGrainProductionFeat.EffectBonus, CultureText);
        if (isGrain && culture.HasFeat(TaomCulturalFeats.MordorGrainProductionFeat))
            result.AddFactor(TaomCulturalFeats.MordorGrainProductionFeat.EffectBonus, CultureText);
    }

    // ── Caravan ────────────────────────────────────────────────────────

    public int ApplyCaravanCost(ICultureFeatAdapter? culture, int baseCost)
    {
        if (culture == null)
            return baseCost;
        if (culture.HasFeat(TaomCulturalFeats.UmbarCheaperCaravansFeat))
            return MathF.Round(baseCost * (1f + TaomCulturalFeats.UmbarCheaperCaravansFeat.EffectBonus));
        return baseCost;
    }

    // ── BattleReward ───────────────────────────────────────────────────

    public void ApplyRenownFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.UmbarRenownFeat))
            result.AddFactor(TaomCulturalFeats.UmbarRenownFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DaleRenownFeat))
            result.AddFactor(TaomCulturalFeats.DaleRenownFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.KhandRenownFeat))
            result.AddFactor(TaomCulturalFeats.KhandRenownFeat.EffectBonus, CultureText);
    }

    // ── PartyTroopUpgrade ──────────────────────────────────────────────

    public void ApplyTroopUpgradeFeats(ICultureFeatAdapter? culture, bool isMounted, ref ExplainedNumber result)
    {
        if (culture == null || !isMounted)
            return;
        if (culture.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat))
            result.AddFactor(TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.RohanMountedCostFeat))
            result.AddFactor(TaomCulturalFeats.RohanMountedCostFeat.EffectBonus, CultureText);
    }

    // ── PartySize ──────────────────────────────────────────────────────

    public void ApplyPartySizeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.MordorPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.MordorPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GundabadPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.GundabadPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GoblinPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.GoblinPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.IsengardPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.IsengardPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GondorPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.GondorPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DunlandPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.DunlandPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.RhunPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.RhunPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.HaradPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.HaradPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.KhandPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.KhandPartySizeFeat.EffectBonus, CultureText);
    }

    // ── VolunteerRespawn ──────────────────────────────────────────────

    public void ApplyVolunteerRespawnFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.DunlandVolunteerRateFeat))
            result.AddFactor(TaomCulturalFeats.DunlandVolunteerRateFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GundabadVolunteerRateFeat))
            result.AddFactor(TaomCulturalFeats.GundabadVolunteerRateFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GoblinVolunteerRateFeat))
            result.AddFactor(TaomCulturalFeats.GoblinVolunteerRateFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurVolunteerRateFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurVolunteerRateFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MordorVolunteerRateFeat))
            result.AddFactor(TaomCulturalFeats.MordorVolunteerRateFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.LothlorienVolunteerRateFeat))
            result.AddFactor(TaomCulturalFeats.LothlorienVolunteerRateFeat.EffectBonus, CultureText);
    }

    // ── NotableSpawn ──────────────────────────────────────────────────

    public int ApplyNotableCountFeat(ICultureFeatAdapter? culture, NotableOccupationKind occupation, int baseCount)
    {
        if (culture == null || baseCount <= 0)
            return baseCount;

        // Town occupations use per-(culture, occupation) AdditionType.Add feats — supports the
        // asymmetric Isengard/Dol Guldur gang-leader-heavy distributions a uniform multiplier
        // couldn't express ("a few Merchants, many Gang Leaders").
        switch (occupation)
        {
            case NotableOccupationKind.Merchant:
            {
                int add = 0;
                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownMerchantFeat))
                    add += (int)TaomCulturalFeats.IsengardNotableCountTownMerchantFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownMerchantFeat))
                    add += (int)TaomCulturalFeats.DolGuldurNotableCountTownMerchantFeat.EffectBonus;
                return baseCount + add;
            }
            case NotableOccupationKind.Artisan:
            {
                int add = 0;
                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownArtisanFeat))
                    add += (int)TaomCulturalFeats.IsengardNotableCountTownArtisanFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat))
                    add += (int)TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountTownArtisanFeat))
                    add += (int)TaomCulturalFeats.GundabadNotableCountTownArtisanFeat.EffectBonus;
                return baseCount + add;
            }
            case NotableOccupationKind.GangLeader:
            {
                int add = 0;
                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownGangLeaderFeat))
                    add += (int)TaomCulturalFeats.IsengardNotableCountTownGangLeaderFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownGangLeaderFeat))
                    add += (int)TaomCulturalFeats.DolGuldurNotableCountTownGangLeaderFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.MordorNotableCountTownGangLeaderFeat))
                    add += (int)TaomCulturalFeats.MordorNotableCountTownGangLeaderFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountTownGangLeaderFeat))
                    add += (int)TaomCulturalFeats.GundabadNotableCountTownGangLeaderFeat.EffectBonus;
                return baseCount + add;
            }
            case NotableOccupationKind.RuralNotable:
            case NotableOccupationKind.Headman:
            {
                // Village: legacy uniform per-(culture, village) AddFactor + ceiling. The 4 village
                // feats deliver the same +1/+1 distribution for all 4 cultures (the user's spec didn't
                // call for asymmetric village counts), so keeping the AddFactor shape is cleaner than
                // splitting them into per-occupation Add feats with identical values.
                float multiplier = 0f;
                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountVillageFeat))
                    multiplier += TaomCulturalFeats.IsengardNotableCountVillageFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountVillageFeat))
                    multiplier += TaomCulturalFeats.DolGuldurNotableCountVillageFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.MordorNotableCountVillageFeat))
                    multiplier += TaomCulturalFeats.MordorNotableCountVillageFeat.EffectBonus;
                if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountVillageFeat))
                    multiplier += TaomCulturalFeats.GundabadNotableCountVillageFeat.EffectBonus;
                if (multiplier <= 0f)
                    return baseCount;
                return (int)Math.Ceiling((double)baseCount * (1.0 + multiplier));
            }
            default:
                return baseCount;
        }
    }

    // ── FoodConsumption ────────────────────────────────────────────────

    public void ApplyFoodConsumptionFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.RivendellFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.RivendellFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MirkwoodFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.MirkwoodFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.LothlorienFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.LothlorienFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GoblinFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.GoblinFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.UmbarFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.UmbarFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.KhandFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.KhandFoodConsumptionFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.HaradFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.HaradFoodConsumptionFeat.EffectBonus, CultureText);
    }

    // ── SettlementLoyalty ──────────────────────────────────────────────

    public void ApplyLoyaltyFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.GondorLoyaltyFeat))
            result.Add(TaomCulturalFeats.GondorLoyaltyFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.EreborLoyaltyFeat))
            result.Add(TaomCulturalFeats.EreborLoyaltyFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.LothlorienLoyaltyFeat))
            result.Add(TaomCulturalFeats.LothlorienLoyaltyFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.RivendellLoyaltyFeat))
            result.Add(TaomCulturalFeats.RivendellLoyaltyFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.RohanLoyaltyFeat))
            result.Add(TaomCulturalFeats.RohanLoyaltyFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DaleLoyaltyFeat))
            result.Add(TaomCulturalFeats.DaleLoyaltyFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.RhunLoyaltyFeat))
            result.Add(TaomCulturalFeats.RhunLoyaltyFeat.EffectBonus, CultureText);
    }

    // ── PartyMorale ────────────────────────────────────────────────────

    public void ApplyMoraleFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.GondorMoraleFeat))
            result.Add(TaomCulturalFeats.GondorMoraleFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
            result.Add(TaomCulturalFeats.RohanMoraleFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.EreborMoraleFeat))
            result.Add(TaomCulturalFeats.EreborMoraleFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MirkwoodMoraleFeat))
            result.Add(TaomCulturalFeats.MirkwoodMoraleFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.LothlorienMoraleFeat))
            result.Add(TaomCulturalFeats.LothlorienMoraleFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.HaradMoraleFeat))
            result.Add(TaomCulturalFeats.HaradMoraleFeat.EffectBonus, CultureText);
    }

    // ── Smithing ───────────────────────────────────────────────────────

    public void ApplySmithingFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.EreborSmithingFeat))
            result.AddFactor(TaomCulturalFeats.EreborSmithingFeat.EffectBonus);
        if (culture.HasFeat(TaomCulturalFeats.IsengardSmithingFeat))
            result.AddFactor(TaomCulturalFeats.IsengardSmithingFeat.EffectBonus);
        if (culture.HasFeat(TaomCulturalFeats.MordorSmithingFeat))
            result.AddFactor(TaomCulturalFeats.MordorSmithingFeat.EffectBonus);
        if (culture.HasFeat(TaomCulturalFeats.GoblinSmithingFeat))
            result.AddFactor(TaomCulturalFeats.GoblinSmithingFeat.EffectBonus);
        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsSmithingFeat))
            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsSmithingFeat.EffectBonus);
    }

    // ── ClanFinance (tariffs) ──────────────────────────────────────────

    public void ApplyTariffIncomeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.UmbarTariffIncomeFeat))
            result.AddFactor(TaomCulturalFeats.UmbarTariffIncomeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.EreborTariffIncomeFeat))
            result.AddFactor(TaomCulturalFeats.EreborTariffIncomeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.DaleTariffIncomeFeat))
            result.AddFactor(TaomCulturalFeats.DaleTariffIncomeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.KhandTariffIncomeFeat))
            result.AddFactor(TaomCulturalFeats.KhandTariffIncomeFeat.EffectBonus, CultureText);
    }

    // ── Raid ───────────────────────────────────────────────────────────

    public void ApplyRaidDamageFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.MordorRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.MordorRaidDamageFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GundabadRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.GundabadRaidDamageFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.IsengardRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.IsengardRaidDamageFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.UmbarRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.UmbarRaidDamageFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GoblinRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.GoblinRaidDamageFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsRaidDamageFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.HaradRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.HaradRaidDamageFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.RhunRaidDamageFeat))
            result.AddFactor(TaomCulturalFeats.RhunRaidDamageFeat.EffectBonus, CultureText);
    }
}
