using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats;

public class TaomCulturalFeats
{
    private static TaomCulturalFeats _instance;

    // Erebor
    private FeatObject _ereborGarrisonWage;
    private FeatObject _ereborProduction;
    private FeatObject _ereborConstructionSpeed;

    // Rivendell
    private FeatObject _rivendellArmyInfluence;
    private FeatObject _rivendellHearthGrowth;
    private FeatObject _rivendellArmyInfluenceCost;
    private FeatObject _rivendellFoodConsumption;

    // Mirkwood
    private FeatObject _mirkwoodForestSpeed;
    private FeatObject _mirkwoodMilitiaProduction;
    private FeatObject _mirkwoodHearthGrowth;
    private FeatObject _mirkwoodFoodConsumption;

    // Lothlorien
    private FeatObject _lothlorienForestSpeed;
    private FeatObject _lothlorienGarrisonWage;
    private FeatObject _lothlorienConstructionSpeed;
    private FeatObject _lothlorienFoodConsumption;

    // Isengard
    private FeatObject _isengardCheaperRecruits;
    private FeatObject _isengardGarrisonWage;
    private FeatObject _isengardDecisionPenalty;
    private FeatObject _isengardPartySize;
    private FeatObject _isengardConstructionSpeed;

    // Gundabad
    private FeatObject _gundabadArmyInfluenceCost;
    private FeatObject _gundabadGrainProduction;
    private FeatObject _gundabadWage;
    private FeatObject _gundabadPartySize;

    // Umbar
    private FeatObject _umbarCheaperCaravans;
    private FeatObject _umbarRenown;
    private FeatObject _umbarWage;

    // Dol Guldur
    private FeatObject _dolguldurArmyInfluenceCost;
    private FeatObject _dolguldurMilitiaProduction;
    private FeatObject _dolguldurConstructionSpeed;
    private FeatObject _dolguldurPartySize;
    private FeatObject _dolguldurFoodConsumption;

    // Gondor
    private FeatObject _gondorGarrisonWage;
    private FeatObject _gondorArmyInfluence;
    private FeatObject _gondorHearthGrowth;
    private FeatObject _gondorPartySize;

    // Mordor
    private FeatObject _mordorArmyInfluenceCost;
    private FeatObject _mordorGrainProduction;
    private FeatObject _mordorWage;
    private FeatObject _mordorPartySize;

    // Rohan (XSLT culture — custom C# feats)
    private FeatObject _rohanMountedCost;
    private FeatObject _rohanMountedWage;
    private FeatObject _rohanInfantrySpeed;

    // Erebor
    public static FeatObject EreborGarrisonWageFeat => _instance._ereborGarrisonWage;
    public static FeatObject EreborProductionFeat => _instance._ereborProduction;
    public static FeatObject EreborConstructionSpeedFeat => _instance._ereborConstructionSpeed;

    // Rivendell
    public static FeatObject RivendellArmyInfluenceFeat => _instance._rivendellArmyInfluence;
    public static FeatObject RivendellHearthGrowthFeat => _instance._rivendellHearthGrowth;
    public static FeatObject RivendellArmyInfluenceCostFeat => _instance._rivendellArmyInfluenceCost;
    public static FeatObject RivendellFoodConsumptionFeat => _instance._rivendellFoodConsumption;

    // Mirkwood
    public static FeatObject MirkwoodForestSpeedFeat => _instance._mirkwoodForestSpeed;
    public static FeatObject MirkwoodMilitiaProductionFeat => _instance._mirkwoodMilitiaProduction;
    public static FeatObject MirkwoodHearthGrowthFeat => _instance._mirkwoodHearthGrowth;
    public static FeatObject MirkwoodFoodConsumptionFeat => _instance._mirkwoodFoodConsumption;

    // Lothlorien
    public static FeatObject LothlorienForestSpeedFeat => _instance._lothlorienForestSpeed;
    public static FeatObject LothlorienGarrisonWageFeat => _instance._lothlorienGarrisonWage;
    public static FeatObject LothlorienConstructionSpeedFeat => _instance._lothlorienConstructionSpeed;
    public static FeatObject LothlorienFoodConsumptionFeat => _instance._lothlorienFoodConsumption;

    // Isengard
    public static FeatObject IsengardCheaperRecruitsFeat => _instance._isengardCheaperRecruits;
    public static FeatObject IsengardGarrisonWageFeat => _instance._isengardGarrisonWage;
    public static FeatObject IsengardDecisionPenaltyFeat => _instance._isengardDecisionPenalty;
    public static FeatObject IsengardPartySizeFeat => _instance._isengardPartySize;
    public static FeatObject IsengardConstructionSpeedFeat => _instance._isengardConstructionSpeed;

    // Gundabad
    public static FeatObject GundabadArmyInfluenceCostFeat => _instance._gundabadArmyInfluenceCost;
    public static FeatObject GundabadGrainProductionFeat => _instance._gundabadGrainProduction;
    public static FeatObject GundabadWageFeat => _instance._gundabadWage;
    public static FeatObject GundabadPartySizeFeat => _instance._gundabadPartySize;

    // Umbar
    public static FeatObject UmbarCheaperCaravansFeat => _instance._umbarCheaperCaravans;
    public static FeatObject UmbarRenownFeat => _instance._umbarRenown;
    public static FeatObject UmbarWageFeat => _instance._umbarWage;

    // Dol Guldur
    public static FeatObject DolGuldurArmyInfluenceCostFeat => _instance._dolguldurArmyInfluenceCost;
    public static FeatObject DolGuldurMilitiaProductionFeat => _instance._dolguldurMilitiaProduction;
    public static FeatObject DolGuldurConstructionSpeedFeat => _instance._dolguldurConstructionSpeed;
    public static FeatObject DolGuldurPartySizeFeat => _instance._dolguldurPartySize;
    public static FeatObject DolGuldurFoodConsumptionFeat => _instance._dolguldurFoodConsumption;

    // Gondor
    public static FeatObject GondorGarrisonWageFeat => _instance._gondorGarrisonWage;
    public static FeatObject GondorArmyInfluenceFeat => _instance._gondorArmyInfluence;
    public static FeatObject GondorHearthGrowthFeat => _instance._gondorHearthGrowth;
    public static FeatObject GondorPartySizeFeat => _instance._gondorPartySize;

    // Mordor
    public static FeatObject MordorArmyInfluenceCostFeat => _instance._mordorArmyInfluenceCost;
    public static FeatObject MordorGrainProductionFeat => _instance._mordorGrainProduction;
    public static FeatObject MordorWageFeat => _instance._mordorWage;
    public static FeatObject MordorPartySizeFeat => _instance._mordorPartySize;

    // Rohan
    public static FeatObject RohanMountedCostFeat => _instance._rohanMountedCost;
    public static FeatObject RohanMountedWageFeat => _instance._rohanMountedWage;
    public static FeatObject RohanInfantrySpeedFeat => _instance._rohanInfantrySpeed;

    public static void CreateAndRegister()
    {
        if (_instance != null)
            return;

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

        _rivendellArmyInfluence = Register("taom_rivendell_army_influence");
        _rivendellHearthGrowth = Register("taom_rivendell_hearth_growth");
        _rivendellArmyInfluenceCost = Register("taom_rivendell_army_influence_cost");
        _rivendellFoodConsumption = Register("taom_rivendell_food_consumption");

        _mirkwoodForestSpeed = Register("taom_mirkwood_forest_speed");
        _mirkwoodMilitiaProduction = Register("taom_mirkwood_militia_production");
        _mirkwoodHearthGrowth = Register("taom_mirkwood_hearth_growth");
        _mirkwoodFoodConsumption = Register("taom_mirkwood_food_consumption");

        _lothlorienForestSpeed = Register("taom_lothlorien_forest_speed");
        _lothlorienGarrisonWage = Register("taom_lothlorien_garrison_wage");
        _lothlorienConstructionSpeed = Register("taom_lothlorien_construction_speed");
        _lothlorienFoodConsumption = Register("taom_lothlorien_food_consumption");

        _isengardCheaperRecruits = Register("taom_isengard_cheaper_recruits");
        _isengardGarrisonWage = Register("taom_isengard_garrison_wage");
        _isengardDecisionPenalty = Register("taom_isengard_decision_penalty");
        _isengardPartySize = Register("taom_isengard_party_size");
        _isengardConstructionSpeed = Register("taom_isengard_construction_speed");

        _gundabadArmyInfluenceCost = Register("taom_gundabad_army_influence_cost");
        _gundabadGrainProduction = Register("taom_gundabad_grain_production");
        _gundabadWage = Register("taom_gundabad_wage");
        _gundabadPartySize = Register("taom_gundabad_party_size");

        _umbarCheaperCaravans = Register("taom_umbar_cheaper_caravans");
        _umbarRenown = Register("taom_umbar_renown");
        _umbarWage = Register("taom_umbar_wage");

        _dolguldurArmyInfluenceCost = Register("taom_dolguldur_army_influence_cost");
        _dolguldurMilitiaProduction = Register("taom_dolguldur_militia_production");
        _dolguldurConstructionSpeed = Register("taom_dolguldur_construction_speed");
        _dolguldurPartySize = Register("taom_dolguldur_party_size");
        _dolguldurFoodConsumption = Register("taom_dolguldur_food_consumption");

        _gondorGarrisonWage = Register("taom_gondor_garrison_wage");
        _gondorArmyInfluence = Register("taom_gondor_army_influence");
        _gondorHearthGrowth = Register("taom_gondor_hearth_growth");
        _gondorPartySize = Register("taom_gondor_party_size");

        _mordorArmyInfluenceCost = Register("taom_mordor_army_influence_cost");
        _mordorGrainProduction = Register("taom_mordor_grain_production");
        _mordorWage = Register("taom_mordor_wage");
        _mordorPartySize = Register("taom_mordor_party_size");

        _rohanMountedCost = Register("taom_rohan_mounted_cost");
        _rohanMountedWage = Register("taom_rohan_mounted_wage");
        _rohanInfantrySpeed = Register("taom_rohan_infantry_speed");
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

        // Mirkwood — Wood Elves: forest masters, good militia, isolated
        _mirkwoodForestSpeed.Initialize(
            "{=taom_feat_mrk_fs}Woodland Realm",
            "{=taom_feat_mrk_fs_desc}Forest speed penalty reduced by 60%.",
            0.6f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
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

        // Lothlorien — Golden Wood: forest speed, cheap garrisons, slow building
        _lothlorienForestSpeed.Initialize(
            "{=taom_feat_loth_fs}Golden Wood",
            "{=taom_feat_loth_fs_desc}Forest speed penalty reduced by 50%.",
            0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
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
            "{=taom_feat_gun_ps_desc}Party size limit increased by 30%.",
            0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

        // Umbar — Corsairs: cheap caravans, battle renown, expensive wages
        _umbarCheaperCaravans.Initialize(
            "{=taom_feat_umb_cc}Corsair Trade",
            "{=taom_feat_umb_cc_desc}Caravan formation cost reduced by 25%.",
            0.75f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _umbarRenown.Initialize(
            "{=taom_feat_umb_r}Corsair Glory",
            "{=taom_feat_umb_r_desc}Renown from battles increased by 8%.",
            0.08f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _umbarWage.Initialize(
            "{=taom_feat_umb_w}Corsair Greed",
            "{=taom_feat_umb_w_desc}Party wages increased by 8%.",
            0.08f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);

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
            "{=taom_feat_dg_ps_desc}Party size limit increased by 25%.",
            0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
        _dolguldurFoodConsumption.Initialize(
            "{=taom_feat_dg_fc}Voracious Hordes",
            "{=taom_feat_dg_fc_desc}Party food consumption increased by 10%.",
            0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);

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
            "{=taom_feat_gon_ps_desc}Party size limit increased by 10%.",
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
            "{=taom_feat_mor_ps_desc}Party size limit increased by 30%.",
            0.3f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);

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
        yield return _instance._rivendellArmyInfluence;
        yield return _instance._rivendellHearthGrowth;
        yield return _instance._rivendellArmyInfluenceCost;
        yield return _instance._rivendellFoodConsumption;
        yield return _instance._mirkwoodForestSpeed;
        yield return _instance._mirkwoodMilitiaProduction;
        yield return _instance._mirkwoodHearthGrowth;
        yield return _instance._mirkwoodFoodConsumption;
        yield return _instance._lothlorienForestSpeed;
        yield return _instance._lothlorienGarrisonWage;
        yield return _instance._lothlorienConstructionSpeed;
        yield return _instance._lothlorienFoodConsumption;
        yield return _instance._isengardCheaperRecruits;
        yield return _instance._isengardGarrisonWage;
        yield return _instance._isengardDecisionPenalty;
        yield return _instance._isengardPartySize;
        yield return _instance._isengardConstructionSpeed;
        yield return _instance._gundabadArmyInfluenceCost;
        yield return _instance._gundabadGrainProduction;
        yield return _instance._gundabadWage;
        yield return _instance._gundabadPartySize;
        yield return _instance._umbarCheaperCaravans;
        yield return _instance._umbarRenown;
        yield return _instance._umbarWage;
        yield return _instance._dolguldurArmyInfluenceCost;
        yield return _instance._dolguldurMilitiaProduction;
        yield return _instance._dolguldurConstructionSpeed;
        yield return _instance._dolguldurPartySize;
        yield return _instance._dolguldurFoodConsumption;
        yield return _instance._gondorGarrisonWage;
        yield return _instance._gondorArmyInfluence;
        yield return _instance._gondorHearthGrowth;
        yield return _instance._gondorPartySize;
        yield return _instance._mordorArmyInfluenceCost;
        yield return _instance._mordorGrainProduction;
        yield return _instance._mordorWage;
        yield return _instance._mordorPartySize;
        yield return _instance._rohanMountedCost;
        yield return _instance._rohanMountedWage;
        yield return _instance._rohanInfantrySpeed;
    }
}
