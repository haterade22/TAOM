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
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat.EffectBonus;
        if (culture.HasFeat(TaomCulturalFeats.MordorArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.MordorArmyInfluenceCostFeat.EffectBonus;

        return multiplier != 0f ? (int)(baseCost * (1f + multiplier)) : baseCost;
    }

    // ── PartySpeed ──────────────────────────────────────────────────────

    public void ApplyForestSpeedFeats(
        ICultureFeatAdapter? culture, bool isForest, float forestPenaltyMagnitude, ref ExplainedNumber result)
    {
        if (culture == null || !isForest)
            return;

        if (culture.HasFeat(TaomCulturalFeats.MirkwoodForestSpeedFeat))
        {
            float bonus = TaomCulturalFeats.MirkwoodForestSpeedFeat.EffectBonus * forestPenaltyMagnitude;
            result.AddFactor(bonus, CultureText);
        }
        if (culture.HasFeat(TaomCulturalFeats.LothlorienForestSpeedFeat))
        {
            float bonus = TaomCulturalFeats.LothlorienForestSpeedFeat.EffectBonus * forestPenaltyMagnitude;
            result.AddFactor(bonus, CultureText);
        }
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
        if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.IsengardPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.IsengardPartySizeFeat.EffectBonus, CultureText);
        if (culture.HasFeat(TaomCulturalFeats.GondorPartySizeFeat))
            result.AddFactor(TaomCulturalFeats.GondorPartySizeFeat.EffectBonus, CultureText);
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
    }

    // ── ClanFinance (tariffs) ──────────────────────────────────────────

    public void ApplyTariffIncomeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
    {
        if (culture == null)
            return;
        if (culture.HasFeat(TaomCulturalFeats.UmbarTariffIncomeFeat))
            result.AddFactor(TaomCulturalFeats.UmbarTariffIncomeFeat.EffectBonus, CultureText);
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
    }
}
