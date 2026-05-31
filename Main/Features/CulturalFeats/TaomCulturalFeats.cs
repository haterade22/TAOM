using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats;

public class TaomCulturalFeats
{
    private static TaomCulturalFeats _instance;

    private static TaomCulturalFeats Instance => _instance
        ?? throw new InvalidOperationException(
            "TaomCulturalFeats not initialized. Ensure Patch18_CulturalFeats is registered.");

    // Erebor
    private FeatObject _ereborGarrisonWage;
    private FeatObject _ereborProduction;
    private FeatObject _ereborConstructionSpeed;
    private FeatObject _ereborLoyalty;
    private FeatObject _ereborMorale;
    private FeatObject _ereborSmithing;
    private FeatObject _ereborSnowSpeed;

    // Rivendell
    private FeatObject _rivendellArmyInfluence;
    private FeatObject _rivendellHearthGrowth;
    private FeatObject _rivendellArmyInfluenceCost;
    private FeatObject _rivendellFoodConsumption;
    private FeatObject _rivendellLoyalty;
    private FeatObject _rivendellForestSpeed;

    // Mirkwood
    private FeatObject _mirkwoodForestSpeed;
    private FeatObject _mirkwoodMilitiaProduction;
    private FeatObject _mirkwoodHearthGrowth;
    private FeatObject _mirkwoodFoodConsumption;
    private FeatObject _mirkwoodMorale;

    // Lothlorien
    private FeatObject _lothlorienForestSpeed;
    private FeatObject _lothlorienGarrisonWage;
    private FeatObject _lothlorienConstructionSpeed;
    private FeatObject _lothlorienFoodConsumption;
    private FeatObject _lothlorienLoyalty;
    private FeatObject _lothlorienMorale;

    // Isengard
    private FeatObject _isengardCheaperRecruits;
    private FeatObject _isengardGarrisonWage;
    private FeatObject _isengardDecisionPenalty;
    private FeatObject _isengardPartySize;
    private FeatObject _isengardConstructionSpeed;
    private FeatObject _isengardSmithing;
    private FeatObject _isengardRaidDamage;
    private FeatObject _isengardPlainSpeed;
    private FeatObject _isengardSwampSpeed;
    private FeatObject _isengardNotableCountTown;
    private FeatObject _isengardNotableCountVillage;

    // Gundabad
    private FeatObject _gundabadArmyInfluenceCost;
    private FeatObject _gundabadGrainProduction;
    private FeatObject _gundabadWage;
    private FeatObject _gundabadPartySize;
    private FeatObject _gundabadRaidDamage;
    private FeatObject _gundabadSnowSpeed;
    private FeatObject _gundabadVolunteerRate;
    private FeatObject _gundabadNotableCountTown;
    private FeatObject _gundabadNotableCountVillage;

    // Umbar
    private FeatObject _umbarCheaperCaravans;
    private FeatObject _umbarRenown;
    private FeatObject _umbarWage;
    private FeatObject _umbarTariffIncome;
    private FeatObject _umbarDesertSpeed;

    // Dol Guldur
    private FeatObject _dolguldurArmyInfluenceCost;
    private FeatObject _dolguldurMilitiaProduction;
    private FeatObject _dolguldurConstructionSpeed;
    private FeatObject _dolguldurPartySize;
    private FeatObject _dolguldurFoodConsumption;
    private FeatObject _dolguldurVolunteerRate;
    private FeatObject _dolguldurNotableCountTown;
    private FeatObject _dolguldurNotableCountVillage;

    // Gondor
    private FeatObject _gondorGarrisonWage;
    private FeatObject _gondorArmyInfluence;
    private FeatObject _gondorHearthGrowth;
    private FeatObject _gondorPartySize;
    private FeatObject _gondorLoyalty;
    private FeatObject _gondorMorale;
    private FeatObject _gondorPlainSpeed;

    // Mordor
    private FeatObject _mordorArmyInfluenceCost;
    private FeatObject _mordorGrainProduction;
    private FeatObject _mordorWage;
    private FeatObject _mordorPartySize;
    private FeatObject _mordorRaidDamage;
    private FeatObject _mordorPlainSpeed;
    private FeatObject _mordorSwampSpeed;
    private FeatObject _mordorNightSpeed;
    private FeatObject _mordorVolunteerRate;
    private FeatObject _mordorNotableCountTown;
    private FeatObject _mordorNotableCountVillage;

    // Rohan (XSLT culture — custom C# feats)
    private FeatObject _rohanMountedCost;
    private FeatObject _rohanMountedWage;
    private FeatObject _rohanInfantrySpeed;
    private FeatObject _rohanLoyalty;
    private FeatObject _rohanMorale;
    private FeatObject _rohanPlainSpeed;

    // Dale (XSLT culture — custom C# feats)
    private FeatObject _dalePlainSpeed;

    // Khand (XSLT culture — custom C# feats)
    private FeatObject _khandSteppeSpeed;

    // Rhun (XSLT culture — custom C# feats)
    private FeatObject _rhunSteppeSpeed;
    private FeatObject _rhunPartySize;

    // Harad (XSLT culture — custom C# feats)
    private FeatObject _haradDesertSpeed;
    private FeatObject _haradPartySize;

    // Dunland (XSLT culture — custom C# feats)
    private FeatObject _dunlandPlainSpeed;
    private FeatObject _dunlandPartySize;
    private FeatObject _dunlandVolunteerRate;

    // Shaghana
    private FeatObject _shaghanaDesertSpeed;

    // Abanissa
    private FeatObject _abanissaDesertSpeed;

    // Erebor
    public static FeatObject EreborGarrisonWageFeat => Instance._ereborGarrisonWage;
    public static FeatObject EreborProductionFeat => Instance._ereborProduction;
    public static FeatObject EreborConstructionSpeedFeat => Instance._ereborConstructionSpeed;
    public static FeatObject EreborLoyaltyFeat => Instance._ereborLoyalty;
    public static FeatObject EreborMoraleFeat => Instance._ereborMorale;
    public static FeatObject EreborSmithingFeat => Instance._ereborSmithing;
    public static FeatObject EreborSnowSpeedFeat => Instance._ereborSnowSpeed;

    // Rivendell
    public static FeatObject RivendellArmyInfluenceFeat => Instance._rivendellArmyInfluence;
    public static FeatObject RivendellHearthGrowthFeat => Instance._rivendellHearthGrowth;
    public static FeatObject RivendellArmyInfluenceCostFeat => Instance._rivendellArmyInfluenceCost;
    public static FeatObject RivendellFoodConsumptionFeat => Instance._rivendellFoodConsumption;
    public static FeatObject RivendellLoyaltyFeat => Instance._rivendellLoyalty;
    public static FeatObject RivendellForestSpeedFeat => Instance._rivendellForestSpeed;

    // Mirkwood
    public static FeatObject MirkwoodForestSpeedFeat => Instance._mirkwoodForestSpeed;
    public static FeatObject MirkwoodMilitiaProductionFeat => Instance._mirkwoodMilitiaProduction;
    public static FeatObject MirkwoodHearthGrowthFeat => Instance._mirkwoodHearthGrowth;
    public static FeatObject MirkwoodFoodConsumptionFeat => Instance._mirkwoodFoodConsumption;
    public static FeatObject MirkwoodMoraleFeat => Instance._mirkwoodMorale;

    // Lothlorien
    public static FeatObject LothlorienForestSpeedFeat => Instance._lothlorienForestSpeed;
    public static FeatObject LothlorienGarrisonWageFeat => Instance._lothlorienGarrisonWage;
    public static FeatObject LothlorienConstructionSpeedFeat => Instance._lothlorienConstructionSpeed;
    public static FeatObject LothlorienFoodConsumptionFeat => Instance._lothlorienFoodConsumption;
    public static FeatObject LothlorienLoyaltyFeat => Instance._lothlorienLoyalty;
    public static FeatObject LothlorienMoraleFeat => Instance._lothlorienMorale;

    // Isengard
    public static FeatObject IsengardCheaperRecruitsFeat => Instance._isengardCheaperRecruits;
    public static FeatObject IsengardGarrisonWageFeat => Instance._isengardGarrisonWage;
    public static FeatObject IsengardDecisionPenaltyFeat => Instance._isengardDecisionPenalty;
    public static FeatObject IsengardPartySizeFeat => Instance._isengardPartySize;
    public static FeatObject IsengardConstructionSpeedFeat => Instance._isengardConstructionSpeed;
    public static FeatObject IsengardSmithingFeat => Instance._isengardSmithing;
    public static FeatObject IsengardRaidDamageFeat => Instance._isengardRaidDamage;
    public static FeatObject IsengardPlainSpeedFeat => Instance._isengardPlainSpeed;
    public static FeatObject IsengardSwampSpeedFeat => Instance._isengardSwampSpeed;
    public static FeatObject IsengardNotableCountTownFeat => Instance._isengardNotableCountTown;
    public static FeatObject IsengardNotableCountVillageFeat => Instance._isengardNotableCountVillage;

    // Gundabad
    public static FeatObject GundabadArmyInfluenceCostFeat => Instance._gundabadArmyInfluenceCost;
    public static FeatObject GundabadGrainProductionFeat => Instance._gundabadGrainProduction;
    public static FeatObject GundabadWageFeat => Instance._gundabadWage;
    public static FeatObject GundabadPartySizeFeat => Instance._gundabadPartySize;
    public static FeatObject GundabadRaidDamageFeat => Instance._gundabadRaidDamage;
    public static FeatObject GundabadSnowSpeedFeat => Instance._gundabadSnowSpeed;
    public static FeatObject GundabadVolunteerRateFeat => Instance._gundabadVolunteerRate;
    public static FeatObject GundabadNotableCountTownFeat => Instance._gundabadNotableCountTown;
    public static FeatObject GundabadNotableCountVillageFeat => Instance._gundabadNotableCountVillage;

    // Umbar
    public static FeatObject UmbarCheaperCaravansFeat => Instance._umbarCheaperCaravans;
    public static FeatObject UmbarRenownFeat => Instance._umbarRenown;
    public static FeatObject UmbarWageFeat => Instance._umbarWage;
    public static FeatObject UmbarTariffIncomeFeat => Instance._umbarTariffIncome;
    public static FeatObject UmbarDesertSpeedFeat => Instance._umbarDesertSpeed;

    // Dol Guldur
    public static FeatObject DolGuldurArmyInfluenceCostFeat => Instance._dolguldurArmyInfluenceCost;
    public static FeatObject DolGuldurMilitiaProductionFeat => Instance._dolguldurMilitiaProduction;
    public static FeatObject DolGuldurConstructionSpeedFeat => Instance._dolguldurConstructionSpeed;
    public static FeatObject DolGuldurPartySizeFeat => Instance._dolguldurPartySize;
    public static FeatObject DolGuldurFoodConsumptionFeat => Instance._dolguldurFoodConsumption;
    public static FeatObject DolGuldurVolunteerRateFeat => Instance._dolguldurVolunteerRate;
    public static FeatObject DolGuldurNotableCountTownFeat => Instance._dolguldurNotableCountTown;
    public static FeatObject DolGuldurNotableCountVillageFeat => Instance._dolguldurNotableCountVillage;

    // Gondor
    public static FeatObject GondorGarrisonWageFeat => Instance._gondorGarrisonWage;
    public static FeatObject GondorArmyInfluenceFeat => Instance._gondorArmyInfluence;
    public static FeatObject GondorHearthGrowthFeat => Instance._gondorHearthGrowth;
    public static FeatObject GondorPartySizeFeat => Instance._gondorPartySize;
    public static FeatObject GondorLoyaltyFeat => Instance._gondorLoyalty;
    public static FeatObject GondorMoraleFeat => Instance._gondorMorale;
    public static FeatObject GondorPlainSpeedFeat => Instance._gondorPlainSpeed;

    // Mordor
    public static FeatObject MordorArmyInfluenceCostFeat => Instance._mordorArmyInfluenceCost;
    public static FeatObject MordorGrainProductionFeat => Instance._mordorGrainProduction;
    public static FeatObject MordorWageFeat => Instance._mordorWage;
    public static FeatObject MordorPartySizeFeat => Instance._mordorPartySize;
    public static FeatObject MordorRaidDamageFeat => Instance._mordorRaidDamage;
    public static FeatObject MordorPlainSpeedFeat => Instance._mordorPlainSpeed;
    public static FeatObject MordorSwampSpeedFeat => Instance._mordorSwampSpeed;
    public static FeatObject MordorNightSpeedFeat => Instance._mordorNightSpeed;
    public static FeatObject MordorVolunteerRateFeat => Instance._mordorVolunteerRate;
    public static FeatObject MordorNotableCountTownFeat => Instance._mordorNotableCountTown;
    public static FeatObject MordorNotableCountVillageFeat => Instance._mordorNotableCountVillage;

    // Rohan
    public static FeatObject RohanMountedCostFeat => Instance._rohanMountedCost;
    public static FeatObject RohanMountedWageFeat => Instance._rohanMountedWage;
    public static FeatObject RohanInfantrySpeedFeat => Instance._rohanInfantrySpeed;
    public static FeatObject RohanLoyaltyFeat => Instance._rohanLoyalty;
    public static FeatObject RohanMoraleFeat => Instance._rohanMorale;
    public static FeatObject RohanPlainSpeedFeat => Instance._rohanPlainSpeed;

    // Dale
    public static FeatObject DalePlainSpeedFeat => Instance._dalePlainSpeed;

    // Khand
    public static FeatObject KhandSteppeSpeedFeat => Instance._khandSteppeSpeed;

    // Rhun
    public static FeatObject RhunSteppeSpeedFeat => Instance._rhunSteppeSpeed;
    public static FeatObject RhunPartySizeFeat => Instance._rhunPartySize;

    // Harad
    public static FeatObject HaradDesertSpeedFeat => Instance._haradDesertSpeed;
    public static FeatObject HaradPartySizeFeat => Instance._haradPartySize;

    // Dunland
    public static FeatObject DunlandPlainSpeedFeat => Instance._dunlandPlainSpeed;
    public static FeatObject DunlandPartySizeFeat => Instance._dunlandPartySize;
    public static FeatObject DunlandVolunteerRateFeat => Instance._dunlandVolunteerRate;

    // Shaghana
    public static FeatObject ShaghanaDesertSpeedFeat => Instance._shaghanaDesertSpeed;

    // Abanissa
    public static FeatObject AbanissaDesertSpeedFeat => Instance._abanissaDesertSpeed;

    public static void CreateAndRegister()
    {
        _instance = new TaomCulturalFeats();
        _instance.RegisterAll();
        _instance.InitializeAll();
    }

    internal static void Reset() => _instance = null;

    private void RegisterAll()
    {
        _ereborGarrisonWage = Register("taom_erebor_garrison_wage");
        _ereborProduction = Register("taom_erebor_production");
        _ereborConstructionSpeed = Register("taom_erebor_construction_speed");
        _ereborLoyalty = Register("taom_erebor_loyalty");
        _ereborMorale = Register("taom_erebor_morale");
        _ereborSmithing = Register("taom_erebor_smithing");
        _ereborSnowSpeed = Register("taom_erebor_snow_speed");

        _rivendellArmyInfluence = Register("taom_rivendell_army_influence");
        _rivendellHearthGrowth = Register("taom_rivendell_hearth_growth");
        _rivendellArmyInfluenceCost = Register("taom_rivendell_army_influence_cost");
        _rivendellFoodConsumption = Register("taom_rivendell_food_consumption");
        _rivendellLoyalty = Register("taom_rivendell_loyalty");
        _rivendellForestSpeed = Register("taom_rivendell_forest_speed");

        _mirkwoodForestSpeed = Register("taom_mirkwood_forest_speed");
        _mirkwoodMilitiaProduction = Register("taom_mirkwood_militia_production");
        _mirkwoodHearthGrowth = Register("taom_mirkwood_hearth_growth");
        _mirkwoodFoodConsumption = Register("taom_mirkwood_food_consumption");
        _mirkwoodMorale = Register("taom_mirkwood_morale");

        _lothlorienForestSpeed = Register("taom_lothlorien_forest_speed");
        _lothlorienGarrisonWage = Register("taom_lothlorien_garrison_wage");
        _lothlorienConstructionSpeed = Register("taom_lothlorien_construction_speed");
        _lothlorienFoodConsumption = Register("taom_lothlorien_food_consumption");
        _lothlorienLoyalty = Register("taom_lothlorien_loyalty");
        _lothlorienMorale = Register("taom_lothlorien_morale");

        _isengardCheaperRecruits = Register("taom_isengard_cheaper_recruits");
        _isengardGarrisonWage = Register("taom_isengard_garrison_wage");
        _isengardDecisionPenalty = Register("taom_isengard_decision_penalty");
        _isengardPartySize = Register("taom_isengard_party_size");
        _isengardConstructionSpeed = Register("taom_isengard_construction_speed");
        _isengardSmithing = Register("taom_isengard_smithing");
        _isengardRaidDamage = Register("taom_isengard_raid_damage");
        _isengardPlainSpeed = Register("taom_isengard_plain_speed");
        _isengardSwampSpeed = Register("taom_isengard_swamp_speed");
        _isengardNotableCountTown = Register("taom_isengard_notable_count_town");
        _isengardNotableCountVillage = Register("taom_isengard_notable_count_village");

        _gundabadArmyInfluenceCost = Register("taom_gundabad_army_influence_cost");
        _gundabadGrainProduction = Register("taom_gundabad_grain_production");
        _gundabadWage = Register("taom_gundabad_wage");
        _gundabadPartySize = Register("taom_gundabad_party_size");
        _gundabadRaidDamage = Register("taom_gundabad_raid_damage");
        _gundabadSnowSpeed = Register("taom_gundabad_snow_speed");
        _gundabadVolunteerRate = Register("taom_gundabad_volunteer_rate");
        _gundabadNotableCountTown = Register("taom_gundabad_notable_count_town");
        _gundabadNotableCountVillage = Register("taom_gundabad_notable_count_village");

        _umbarCheaperCaravans = Register("taom_umbar_cheaper_caravans");
        _umbarRenown = Register("taom_umbar_renown");
        _umbarWage = Register("taom_umbar_wage");
        _umbarTariffIncome = Register("taom_umbar_tariff_income");
        _umbarDesertSpeed = Register("taom_umbar_desert_speed");

        _dolguldurArmyInfluenceCost = Register("taom_dolguldur_army_influence_cost");
        _dolguldurMilitiaProduction = Register("taom_dolguldur_militia_production");
        _dolguldurConstructionSpeed = Register("taom_dolguldur_construction_speed");
        _dolguldurPartySize = Register("taom_dolguldur_party_size");
        _dolguldurFoodConsumption = Register("taom_dolguldur_food_consumption");
        _dolguldurVolunteerRate = Register("taom_dolguldur_volunteer_rate");
        _dolguldurNotableCountTown = Register("taom_dolguldur_notable_count_town");
        _dolguldurNotableCountVillage = Register("taom_dolguldur_notable_count_village");

        _gondorGarrisonWage = Register("taom_gondor_garrison_wage");
        _gondorArmyInfluence = Register("taom_gondor_army_influence");
        _gondorHearthGrowth = Register("taom_gondor_hearth_growth");
        _gondorPartySize = Register("taom_gondor_party_size");
        _gondorLoyalty = Register("taom_gondor_loyalty");
        _gondorMorale = Register("taom_gondor_morale");
        _gondorPlainSpeed = Register("taom_gondor_plain_speed");

        _mordorArmyInfluenceCost = Register("taom_mordor_army_influence_cost");
        _mordorGrainProduction = Register("taom_mordor_grain_production");
        _mordorWage = Register("taom_mordor_wage");
        _mordorPartySize = Register("taom_mordor_party_size");
        _mordorRaidDamage = Register("taom_mordor_raid_damage");
        _mordorPlainSpeed = Register("taom_mordor_plain_speed");
        _mordorSwampSpeed = Register("taom_mordor_swamp_speed");
        _mordorNightSpeed = Register("taom_mordor_night_speed");
        _mordorVolunteerRate = Register("taom_mordor_volunteer_rate");
        _mordorNotableCountTown = Register("taom_mordor_notable_count_town");
        _mordorNotableCountVillage = Register("taom_mordor_notable_count_village");

        _rohanMountedCost = Register("taom_rohan_mounted_cost");
        _rohanMountedWage = Register("taom_rohan_mounted_wage");
        _rohanInfantrySpeed = Register("taom_rohan_infantry_speed");
        _rohanLoyalty = Register("taom_rohan_loyalty");
        _rohanMorale = Register("taom_rohan_morale");
        _rohanPlainSpeed = Register("taom_rohan_plain_speed");

        _dalePlainSpeed = Register("taom_dale_plain_speed");
        _khandSteppeSpeed = Register("taom_khand_steppe_speed");
        _rhunSteppeSpeed = Register("taom_rhun_steppe_speed");
        _haradDesertSpeed = Register("taom_harad_desert_speed");
        _dunlandPlainSpeed = Register("taom_dunland_plain_speed");
        _rhunPartySize = Register("taom_rhun_party_size");
        _haradPartySize = Register("taom_harad_party_size");
        _dunlandPartySize = Register("taom_dunland_party_size");
        _dunlandVolunteerRate = Register("taom_dunland_volunteer_rate");
        _shaghanaDesertSpeed = Register("taom_shaghana_desert_speed");
        _abanissaDesertSpeed = Register("taom_abanissa_desert_speed");
    }

    private void InitializeAll()
    {
        // Erebor — Dwarves: cheap garrisons, strong production, slow construction
        _ereborGarrisonWage.Initialize(
            "{=taom_feat_erebor_gw}Dwarven Garrison",
            "{=taom_feat_erebor_gw_desc}Garrison wages reduced by 25%.",
            -0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _ereborProduction.Initialize(
            "{=taom_feat_erebor_p}Dwarven Industry",
            "{=taom_feat_erebor_p_desc}All village production increased by 10%.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _ereborConstructionSpeed.Initialize(
            "{=taom_feat_erebor_cs}Dwarven Perfectionism",
            "{=taom_feat_erebor_cs_desc}Construction speed reduced by 15%.",
            -0.15f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _ereborLoyalty.Initialize(
            "{=taom_feat_erebor_loy}Dwarven Honor",
            "{=taom_feat_erebor_loy_desc}Settlement loyalty increased by 1 per day.",
            1f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _ereborMorale.Initialize(
            "{=taom_feat_erebor_mor}Dwarven Stubbornness",
            "{=taom_feat_erebor_mor_desc}Party morale increased by 5.",
            5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _ereborSmithing.Initialize(
            "{=taom_feat_erebor_sm}Master Smiths",
            "{=taom_feat_erebor_sm_desc}Smithing energy cost reduced by 30%.",
            -0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _ereborSnowSpeed.Initialize(
            "{=taom_feat_erebor_ss}Mountain Folk",
            "{=taom_feat_erebor_ss_desc}Party movement speed increased by 10% in snow.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Rivendell — High Elves: strong armies, good growth, expensive to rally
        _rivendellArmyInfluence.Initialize(
            "{=taom_feat_riv_ai}Elven Wisdom",
            "{=taom_feat_riv_ai_desc}Army influence award increased by 35%.",
            0.35f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _rivendellHearthGrowth.Initialize(
            "{=taom_feat_riv_hg}The Last Homely House",
            "{=taom_feat_riv_hg_desc}Village hearth growth increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _rivendellArmyInfluenceCost.Initialize(
            "{=taom_feat_riv_aic}Elven Pride",
            "{=taom_feat_riv_aic_desc}Army recruitment costs 25% more influence.",
            0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _rivendellFoodConsumption.Initialize(
            "{=taom_feat_riv_fc}Elven Frugality",
            "{=taom_feat_riv_fc_desc}Party food consumption reduced by 15%.",
            -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _rivendellLoyalty.Initialize(
            "{=taom_feat_riv_loy}Elven Wisdom",
            "{=taom_feat_riv_loy_desc}Settlement loyalty increased by 0.5 per day.",
            0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _rivendellForestSpeed.Initialize(
            "{=taom_feat_riv_fs}Woodland Grace",
            "{=taom_feat_riv_fs_desc}Party movement speed increased by 10% in forests.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Mirkwood — Wood Elves: forest masters, good militia, isolated
        _mirkwoodForestSpeed.Initialize(
            "{=taom_feat_mrk_fs}Woodland Realm",
            "{=taom_feat_mrk_fs_desc}Party movement speed increased by 10% in forests.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mirkwoodMilitiaProduction.Initialize(
            "{=taom_feat_mrk_mp}Silvan Wardens",
            "{=taom_feat_mrk_mp_desc}25% increased chance of veteran militia.",
            0.25f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _mirkwoodHearthGrowth.Initialize(
            "{=taom_feat_mrk_hg}Forest Isolation",
            "{=taom_feat_mrk_hg_desc}Village hearth growth reduced by 20%.",
            -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _mirkwoodFoodConsumption.Initialize(
            "{=taom_feat_mrk_fc}Woodland Sustenance",
            "{=taom_feat_mrk_fc_desc}Party food consumption reduced by 15%.",
            -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mirkwoodMorale.Initialize(
            "{=taom_feat_mrk_mor}Woodland Bonds",
            "{=taom_feat_mrk_mor_desc}Party morale increased by 3.",
            3f, isPositiveEffect: true, FeatObject.AdditionType.Add);

        // Lothlorien — Golden Wood: forest speed, cheap garrisons, slow building
        _lothlorienForestSpeed.Initialize(
            "{=taom_feat_loth_fs}Golden Wood",
            "{=taom_feat_loth_fs_desc}Party movement speed increased by 10% in forests.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _lothlorienGarrisonWage.Initialize(
            "{=taom_feat_loth_gw}Wardens of Lorien",
            "{=taom_feat_loth_gw_desc}Garrison wages reduced by 20%.",
            -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _lothlorienConstructionSpeed.Initialize(
            "{=taom_feat_loth_cs}Timeless Craft",
            "{=taom_feat_loth_cs_desc}Construction speed reduced by 10%.",
            -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _lothlorienFoodConsumption.Initialize(
            "{=taom_feat_loth_fc}Lembas Bread",
            "{=taom_feat_loth_fc_desc}Party food consumption reduced by 15%.",
            -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _lothlorienLoyalty.Initialize(
            "{=taom_feat_loth_loy}Elven Grace",
            "{=taom_feat_loth_loy_desc}Settlement loyalty increased by 0.5 per day.",
            0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _lothlorienMorale.Initialize(
            "{=taom_feat_loth_mor}Elven Harmony",
            "{=taom_feat_loth_mor_desc}Party morale increased by 3.",
            3f, isPositiveEffect: true, FeatObject.AdditionType.Add);

        // Isengard — Saruman: cheap mounted recruits, cheap garrisons, decision penalty
        _isengardCheaperRecruits.Initialize(
            "{=taom_feat_isen_cr}War Machine",
            "{=taom_feat_isen_cr_desc}Mounted troop recruitment and upgrade costs reduced by 15%.",
            -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardGarrisonWage.Initialize(
            "{=taom_feat_isen_gw}Orthanc Garrison",
            "{=taom_feat_isen_gw_desc}Garrison wages reduced by 20%.",
            -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardDecisionPenalty.Initialize(
            "{=taom_feat_isen_dp}Saruman's Grip",
            "{=taom_feat_isen_dp_desc}Kingdom decision relationship penalties increased by 25%.",
            0.25f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _isengardPartySize.Initialize(
            "{=taom_feat_isen_ps}Uruk-hai Legions",
            "{=taom_feat_isen_ps_desc}Party size limit increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardConstructionSpeed.Initialize(
            "{=taom_feat_isen_cs}Industrial Might",
            "{=taom_feat_isen_cs_desc}Construction speed increased by 15%.",
            0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardSmithing.Initialize(
            "{=taom_feat_isen_sm}Industrial Forges",
            "{=taom_feat_isen_sm_desc}Smithing energy cost reduced by 20%.",
            -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardRaidDamage.Initialize(
            "{=taom_feat_isen_rd}War Machine Raids",
            "{=taom_feat_isen_rd_desc}Raid damage increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardPlainSpeed.Initialize(
            "{=taom_feat_isen_ps2}Forced March",
            "{=taom_feat_isen_ps2_desc}Party movement speed increased by 10% on plains.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardSwampSpeed.Initialize(
            "{=taom_feat_isen_sws}Fenland Drillmasters",
            "{=taom_feat_isen_sws_desc}Party movement speed increased by 10% in swamps.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardNotableCountTown.Initialize(
            "{=taom_feat_isen_nct}Orthanc Industry",
            "{=taom_feat_isen_nct_desc}Notable count in towns increased by 50%.",
            0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _isengardNotableCountVillage.Initialize(
            "{=taom_feat_isen_ncv}Iron Press",
            "{=taom_feat_isen_ncv_desc}Notable count in villages increased by 10%.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Gundabad — Orcs: cheap armies, grain production, expensive wages
        _gundabadArmyInfluenceCost.Initialize(
            "{=taom_feat_gun_aic}Orc Horde",
            "{=taom_feat_gun_aic_desc}Army recruitment costs 40% less influence.",
            -0.4f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gundabadGrainProduction.Initialize(
            "{=taom_feat_gun_gp}Plundered Stores",
            "{=taom_feat_gun_gp_desc}Grain production increased by 15%.",
            0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gundabadWage.Initialize(
            "{=taom_feat_gun_w}Plunder Demands",
            "{=taom_feat_gun_w_desc}Party wages increased by 10%.",
            0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _gundabadPartySize.Initialize(
            "{=taom_feat_gun_ps}Mountain Swarm",
            "{=taom_feat_gun_ps_desc}Party size limit increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gundabadRaidDamage.Initialize(
            "{=taom_feat_gun_rd}Orc Pillagers",
            "{=taom_feat_gun_rd_desc}Raid damage increased by 25%.",
            0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gundabadSnowSpeed.Initialize(
            "{=taom_feat_gun_ss}Mountain Marauders",
            "{=taom_feat_gun_ss_desc}Party movement speed increased by 10% in snow.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gundabadVolunteerRate.Initialize(
            "{=taom_feat_gun_vr}Mountain Levies",
            "{=taom_feat_gun_vr_desc}Village volunteer respawn rate increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gundabadNotableCountTown.Initialize(
            "{=taom_feat_gun_nct}Goblin Warrens",
            "{=taom_feat_gun_nct_desc}Notable count in towns increased by 10%.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gundabadNotableCountVillage.Initialize(
            "{=taom_feat_gun_ncv}Bone Camps",
            "{=taom_feat_gun_ncv_desc}Notable count in villages increased by 10%.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Umbar — Corsairs: cheap caravans, battle renown, expensive wages
        _umbarCheaperCaravans.Initialize(
            "{=taom_feat_umb_cc}Corsair Trade",
            "{=taom_feat_umb_cc_desc}Caravan formation cost reduced by 25%.",
            -0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _umbarRenown.Initialize(
            "{=taom_feat_umb_r}Corsair Glory",
            "{=taom_feat_umb_r_desc}Renown from battles increased by 8%.",
            0.08f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _umbarWage.Initialize(
            "{=taom_feat_umb_w}Corsair Greed",
            "{=taom_feat_umb_w_desc}Party wages increased by 8%.",
            0.08f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _umbarTariffIncome.Initialize(
            "{=taom_feat_umb_ti}Corsair Trade Networks",
            "{=taom_feat_umb_ti_desc}Tariff income increased by 15%.",
            0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _umbarDesertSpeed.Initialize(
            "{=taom_feat_umb_ds}Desert Corsairs",
            "{=taom_feat_umb_ds_desc}Party movement speed increased by 10% in deserts.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Dol Guldur — Shadow: cheap armies, veteran militia, slow construction
        _dolguldurArmyInfluenceCost.Initialize(
            "{=taom_feat_dg_aic}Shadow Command",
            "{=taom_feat_dg_aic_desc}Army recruitment costs 50% less influence.",
            -0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _dolguldurMilitiaProduction.Initialize(
            "{=taom_feat_dg_mp}Dark Conscription",
            "{=taom_feat_dg_mp_desc}20% increased chance of veteran militia.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _dolguldurConstructionSpeed.Initialize(
            "{=taom_feat_dg_cs}Ruinous Works",
            "{=taom_feat_dg_cs_desc}Construction speed reduced by 20%.",
            -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _dolguldurPartySize.Initialize(
            "{=taom_feat_dg_ps}Dark Legions",
            "{=taom_feat_dg_ps_desc}Party size limit increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _dolguldurFoodConsumption.Initialize(
            "{=taom_feat_dg_fc}Voracious Hordes",
            "{=taom_feat_dg_fc_desc}Party food consumption increased by 10%.",
            0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _dolguldurVolunteerRate.Initialize(
            "{=taom_feat_dg_vr}Dark Conscripts",
            "{=taom_feat_dg_vr_desc}Village volunteer respawn rate increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _dolguldurNotableCountTown.Initialize(
            "{=taom_feat_dg_nct}Shadow Brokers",
            "{=taom_feat_dg_nct_desc}Notable count in towns increased by 50%.",
            0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _dolguldurNotableCountVillage.Initialize(
            "{=taom_feat_dg_ncv}Hidden Hovels",
            "{=taom_feat_dg_ncv_desc}Notable count in villages increased by 10%.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Gondor — Men of the West: cheap garrisons, army influence, depleted hearths
        _gondorGarrisonWage.Initialize(
            "{=taom_feat_gon_gw}Tower Guard",
            "{=taom_feat_gon_gw_desc}Garrison wages reduced by 20%.",
            -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gondorArmyInfluence.Initialize(
            "{=taom_feat_gon_ai}Gondorian Discipline",
            "{=taom_feat_gon_ai_desc}Army influence award increased by 30%.",
            0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gondorHearthGrowth.Initialize(
            "{=taom_feat_gon_hg}War-Depleted Lands",
            "{=taom_feat_gon_hg_desc}Village hearth growth reduced by 15%.",
            -0.15f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _gondorPartySize.Initialize(
            "{=taom_feat_gon_ps}Standing Armies",
            "{=taom_feat_gon_ps_desc}Party size limit increased by 2.5%.",
            0.025f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _gondorLoyalty.Initialize(
            "{=taom_feat_gon_loy}Tower Guard Discipline",
            "{=taom_feat_gon_loy_desc}Settlement loyalty increased by 1 per day.",
            1f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _gondorMorale.Initialize(
            "{=taom_feat_gon_mor}Gondorian Resolve",
            "{=taom_feat_gon_mor_desc}Party morale increased by 5.",
            5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _gondorPlainSpeed.Initialize(
            "{=taom_feat_gon_ps2}Men of the Fields",
            "{=taom_feat_gon_ps2_desc}Party movement speed increased by 10% on plains.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Mordor — Dark Lord: very cheap armies, grain production, very expensive wages
        _mordorArmyInfluenceCost.Initialize(
            "{=taom_feat_mor_aic}The Dark Lord's Will",
            "{=taom_feat_mor_aic_desc}Army recruitment costs 60% less influence.",
            -0.6f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorGrainProduction.Initialize(
            "{=taom_feat_mor_gp}Nurn Farmlands",
            "{=taom_feat_mor_gp_desc}Grain production increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorWage.Initialize(
            "{=taom_feat_mor_w}Dark Tribute",
            "{=taom_feat_mor_w_desc}Party wages increased by 20%.",
            0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _mordorPartySize.Initialize(
            "{=taom_feat_mor_ps}Sauron's Hordes",
            "{=taom_feat_mor_ps_desc}Party size limit increased by 10%.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorRaidDamage.Initialize(
            "{=taom_feat_mor_rd}Sauron's Wrath",
            "{=taom_feat_mor_rd_desc}Raid damage increased by 25%.",
            0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorPlainSpeed.Initialize(
            "{=taom_feat_mor_pls}Shadow March",
            "{=taom_feat_mor_pls_desc}Party movement speed increased by 5% on plains.",
            0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorSwampSpeed.Initialize(
            "{=taom_feat_mor_sws}Dead Marshes",
            "{=taom_feat_mor_sws_desc}Party movement speed increased by 5% in swamps.",
            0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorNightSpeed.Initialize(
            "{=taom_feat_mor_ns}Creatures of the Dark",
            "{=taom_feat_mor_ns_desc}Party movement speed increased by 10% at night.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorVolunteerRate.Initialize(
            "{=taom_feat_mor_vr}Sauron's Levy",
            "{=taom_feat_mor_vr_desc}Village volunteer respawn rate increased by 20%.",
            0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorNotableCountTown.Initialize(
            "{=taom_feat_mor_nct}Black Speech Heralds",
            "{=taom_feat_mor_nct_desc}Notable count in towns increased by 5%.",
            0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _mordorNotableCountVillage.Initialize(
            "{=taom_feat_mor_ncv}Slave Drivers",
            "{=taom_feat_mor_ncv_desc}Notable count in villages increased by 5%.",
            0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Rohan — Horse-lords: cheap mounted troops, slow without cavalry
        _rohanMountedCost.Initialize(
            "{=taom_feat_roh_mc}Horse-lord Heritage",
            "{=taom_feat_roh_mc_desc}Mounted troop recruitment and upgrade costs reduced by 15%.",
            -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _rohanMountedWage.Initialize(
            "{=taom_feat_roh_mw}Riders of the Mark",
            "{=taom_feat_roh_mw_desc}Mounted troop wages reduced by 15%.",
            -0.15f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _rohanInfantrySpeed.Initialize(
            "{=taom_feat_roh_is}Cavalry Dependent",
            "{=taom_feat_roh_is_desc}Party speed reduced by 10% when majority infantry.",
            -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
        _rohanLoyalty.Initialize(
            "{=taom_feat_roh_loy}Horse-lord Fellowship",
            "{=taom_feat_roh_loy_desc}Settlement loyalty increased by 0.5 per day.",
            0.5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _rohanMorale.Initialize(
            "{=taom_feat_roh_mor}Riders' Spirit",
            "{=taom_feat_roh_mor_desc}Party morale increased by 5.",
            5f, isPositiveEffect: true, FeatObject.AdditionType.Add);
        _rohanPlainSpeed.Initialize(
            "{=taom_feat_roh_pls}Riders of the Plains",
            "{=taom_feat_roh_pls_desc}Party movement speed increased by 10% on plains.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Dale — Men of Dale: traders of the vale
        _dalePlainSpeed.Initialize(
            "{=taom_feat_dale_pls}Vale Traders",
            "{=taom_feat_dale_pls_desc}Party movement speed increased by 10% on plains.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Khand — Variags: steppe charioteers
        _khandSteppeSpeed.Initialize(
            "{=taom_feat_khand_sts}Steppe Charioteers",
            "{=taom_feat_khand_sts_desc}Party movement speed increased by 10% on steppes.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Rhun — Easterlings: mounted outriders
        _rhunSteppeSpeed.Initialize(
            "{=taom_feat_rhun_sts}Easterling Outriders",
            "{=taom_feat_rhun_sts_desc}Party movement speed increased by 10% on steppes.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _rhunPartySize.Initialize(
            "{=taom_feat_rhun_ps}Easterling Host",
            "{=taom_feat_rhun_ps_desc}Party size limit increased by 5%.",
            0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Harad — Haradrim: sons of the sun
        _haradDesertSpeed.Initialize(
            "{=taom_feat_har_ds}Sons of the Sun",
            "{=taom_feat_har_ds_desc}Party movement speed increased by 10% in deserts.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _haradPartySize.Initialize(
            "{=taom_feat_har_ps}Haradrim Warbands",
            "{=taom_feat_har_ps_desc}Party size limit increased by 5%.",
            0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Dunland — Hill-men: hill marchers
        _dunlandPlainSpeed.Initialize(
            "{=taom_feat_dun_pls}Hill Marchers",
            "{=taom_feat_dun_pls_desc}Party movement speed increased by 10% on plains.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _dunlandPartySize.Initialize(
            "{=taom_feat_dun_ps}Hill-Tribe Levy",
            "{=taom_feat_dun_ps_desc}Party size limit increased by 5%.",
            0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _dunlandVolunteerRate.Initialize(
            "{=taom_feat_dun_vr}Hill-Tribe Recruitment",
            "{=taom_feat_dun_vr_desc}Village volunteer respawn rate increased by 10%.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Shaghana — southern Haradrim: dune wanderers
        _shaghanaDesertSpeed.Initialize(
            "{=taom_feat_shg_ds}Dune Wanderers",
            "{=taom_feat_shg_ds_desc}Party movement speed increased by 10% in deserts.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Abanissa — deep-south Haradrim: deep desert nomads
        _abanissaDesertSpeed.Initialize(
            "{=taom_feat_aba_ds}Deep Desert Nomads",
            "{=taom_feat_aba_ds_desc}Party movement speed increased by 10% in deserts.",
            0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
    }

    private static FeatObject Register(string stringId)
        => Game.Current.ObjectManager.RegisterPresumedObject(new FeatObject(stringId));

    internal static IEnumerable<FeatObject> GetAllFeats()
    {
        if (_instance == null)
            yield break;

        yield return _instance._ereborGarrisonWage;
        yield return _instance._ereborProduction;
        yield return _instance._ereborConstructionSpeed;
        yield return _instance._ereborLoyalty;
        yield return _instance._ereborMorale;
        yield return _instance._ereborSmithing;
        yield return _instance._ereborSnowSpeed;
        yield return _instance._rivendellArmyInfluence;
        yield return _instance._rivendellHearthGrowth;
        yield return _instance._rivendellArmyInfluenceCost;
        yield return _instance._rivendellFoodConsumption;
        yield return _instance._rivendellLoyalty;
        yield return _instance._rivendellForestSpeed;
        yield return _instance._mirkwoodForestSpeed;
        yield return _instance._mirkwoodMilitiaProduction;
        yield return _instance._mirkwoodHearthGrowth;
        yield return _instance._mirkwoodFoodConsumption;
        yield return _instance._mirkwoodMorale;
        yield return _instance._lothlorienForestSpeed;
        yield return _instance._lothlorienGarrisonWage;
        yield return _instance._lothlorienConstructionSpeed;
        yield return _instance._lothlorienFoodConsumption;
        yield return _instance._lothlorienLoyalty;
        yield return _instance._lothlorienMorale;
        yield return _instance._isengardCheaperRecruits;
        yield return _instance._isengardGarrisonWage;
        yield return _instance._isengardDecisionPenalty;
        yield return _instance._isengardPartySize;
        yield return _instance._isengardConstructionSpeed;
        yield return _instance._isengardSmithing;
        yield return _instance._isengardRaidDamage;
        yield return _instance._isengardPlainSpeed;
        yield return _instance._isengardSwampSpeed;
        yield return _instance._isengardNotableCountTown;
        yield return _instance._isengardNotableCountVillage;
        yield return _instance._gundabadArmyInfluenceCost;
        yield return _instance._gundabadGrainProduction;
        yield return _instance._gundabadWage;
        yield return _instance._gundabadPartySize;
        yield return _instance._gundabadRaidDamage;
        yield return _instance._gundabadSnowSpeed;
        yield return _instance._gundabadVolunteerRate;
        yield return _instance._gundabadNotableCountTown;
        yield return _instance._gundabadNotableCountVillage;
        yield return _instance._umbarCheaperCaravans;
        yield return _instance._umbarRenown;
        yield return _instance._umbarWage;
        yield return _instance._umbarTariffIncome;
        yield return _instance._umbarDesertSpeed;
        yield return _instance._dolguldurArmyInfluenceCost;
        yield return _instance._dolguldurMilitiaProduction;
        yield return _instance._dolguldurConstructionSpeed;
        yield return _instance._dolguldurPartySize;
        yield return _instance._dolguldurFoodConsumption;
        yield return _instance._dolguldurVolunteerRate;
        yield return _instance._dolguldurNotableCountTown;
        yield return _instance._dolguldurNotableCountVillage;
        yield return _instance._gondorGarrisonWage;
        yield return _instance._gondorArmyInfluence;
        yield return _instance._gondorHearthGrowth;
        yield return _instance._gondorPartySize;
        yield return _instance._gondorLoyalty;
        yield return _instance._gondorMorale;
        yield return _instance._gondorPlainSpeed;
        yield return _instance._mordorArmyInfluenceCost;
        yield return _instance._mordorGrainProduction;
        yield return _instance._mordorWage;
        yield return _instance._mordorPartySize;
        yield return _instance._mordorRaidDamage;
        yield return _instance._mordorPlainSpeed;
        yield return _instance._mordorSwampSpeed;
        yield return _instance._mordorNightSpeed;
        yield return _instance._mordorVolunteerRate;
        yield return _instance._mordorNotableCountTown;
        yield return _instance._mordorNotableCountVillage;
        yield return _instance._rohanMountedCost;
        yield return _instance._rohanMountedWage;
        yield return _instance._rohanInfantrySpeed;
        yield return _instance._rohanLoyalty;
        yield return _instance._rohanMorale;
        yield return _instance._rohanPlainSpeed;
        yield return _instance._dalePlainSpeed;
        yield return _instance._khandSteppeSpeed;
        yield return _instance._rhunSteppeSpeed;
        yield return _instance._rhunPartySize;
        yield return _instance._haradDesertSpeed;
        yield return _instance._haradPartySize;
        yield return _instance._dunlandPlainSpeed;
        yield return _instance._dunlandPartySize;
        yield return _instance._dunlandVolunteerRate;
        yield return _instance._shaghanaDesertSpeed;
        yield return _instance._abanissaDesertSpeed;
    }
}
