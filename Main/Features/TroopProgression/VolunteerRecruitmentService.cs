using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

public class VolunteerRecruitmentService : IVolunteerRecruitmentService
{
    private readonly IRandomProvider _random;

    private static readonly Dictionary<string, List<VolunteerChance>> SettlementMap = new();
    private static readonly Dictionary<string, List<VolunteerChance>> ClanMap = new();
    private static readonly Dictionary<string, List<VolunteerChance>> CultureMap = new();

    static VolunteerRecruitmentService()
    {
        InitializeGondorSettlements();
        InitializeGondorClans();
        InitializeGondorCulture();
        InitializeDolGuldurSettlements();
        InitializeDolGuldurClans();
        InitializeDolGuldurCulture();
        InitializeEreborSettlements();
        InitializeEreborClans();
        InitializeEreborCulture();
        InitializeShaghanaClans();
        InitializeShaghânaCulture();
        InitializeAbanissaClans();
        InitializeAbanissaCulture();
    }

    public VolunteerRecruitmentService(IRandomProvider random)
    {
        _random = random;
    }

    public string GetVolunteerTroopId(VolunteerContext context)
    {
        var pool = ResolvePool(context.SettlementId, SettlementMap)
                ?? ResolvePool(context.BoundSettlementId, SettlementMap)
                ?? ResolvePool(context.OwnerClanId, ClanMap)
                ?? ResolvePool(context.CultureId, CultureMap);

        if (pool == null || pool.Count == 0)
            return null;

        return PickWeighted(pool);
    }

    private static List<VolunteerChance> ResolvePool(string key, Dictionary<string, List<VolunteerChance>> map)
    {
        if (key != null && map.TryGetValue(key, out var pool))
            return pool;
        return null;
    }

    private string PickWeighted(List<VolunteerChance> pool)
    {
        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++)
            totalWeight += pool[i].Weight;

        int roll = _random.Next(totalWeight);

        int cumulative = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].Weight;
            if (roll < cumulative)
                return pool[i].CharacterId;
        }

        return pool[pool.Count - 1].CharacterId;
    }

    // --- Gondor Settlement Mappings ---

    private static void InitializeGondorSettlements()
    {
        // Towns
        AddSettlement("town_EW1", "gondor_ano_peasant", 7, "gondor_mt_trainee", 3);
        AddSettlement("town_EW2", "gondor_ano_peasant", 7, "gondor_osg_veteran", 3);
        AddSettlement("town_EW3", "gondor_ano_peasant", 7, "gondor_osg_veteran", 3);
        AddSettlement("town_EW4", "gondor_leb_militia", 7, "gondor_pel_skirmisher", 3);
        AddSettlement("town_EW5", "gondor_bel_recruit", 7, "gondor_da_noble", 3);
        AddSettlement("town_EW6", "gondor_bel_recruit", 7, "gondor_da_noble", 3);
        AddSettlement("town_EW7", "gondor_leb_militia", 7, "gondor_lg_noble", 3);
        AddSettlement("town_EW8", "gondor_pg_volunteer", 7, "gondor_arn_noble", 3);
        AddSettlement("town_EW9", "gondor_lam_clansman", 7, "gondor_cal_noble", 3);
        AddSettlement("town_EW10", "gondor_anf_levy", 7, "gondor_ser_noble", 3);
        AddSettlement("town_EW11", "gondor_ano_peasant", 7, "gondor_met_noble", 3);

        // Castles
        AddSettlement("castle_EW1", "gondor_ano_peasant", 7, "gondor_mt_trainee", 3);
        AddSettlement("castle_EW2", "gondor_lam_clansman", 7, "gondor_ring_peasant", 3);
        AddSettlement("castle_EW3", "gondor_lam_clansman", 7, "gondor_cal_noble", 3);
        AddSettlement("castle_EW4", "gondor_ano_peasant", 7, "gondor_ca_noble", 3);
        AddSettlement("castle_EW5", "gondor_ano_peasant", 8, "gondor_ano_peasant", 2);
        AddSettlement("castle_EW6", "gondor_har_conscript", 8, "gondor_har_conscript", 2);
        AddSettlement("castle_EW7", "gondor_anf_levy", 7, "gondor_ser_noble", 3);
        AddSettlement("castle_EW8", "gondor_loss_lumberman", 7, "gondor_loss_noble", 3);
        AddSettlement("castle_EW9", "gondor_bel_recruit", 7, "gondor_tol_arbalest", 3);
        AddSettlement("castle_EW10", "gondor_anf_levy", 7, "gondor_lg_noble", 3);
        AddSettlement("castle_EW11", "gondor_ano_peasant", 7, "gondor_met_noble", 3);
        AddSettlement("castle_EW12", "gondor_loss_lumberman", 7, "gondor_loss_noble", 3);
        AddSettlement("castle_EW13", "gondor_anf_levy", 8, "gondor_anf_levy", 2);
        AddSettlement("castle_EW14", "gondor_leb_militia", 7, "gondor_lin_noble", 3);
        AddSettlement("castle_EW15", "gondor_ano_peasant", 7, "gondor_ith_watcher", 3);
        AddSettlement("castle_EW16", "gondor_ano_peasant", 7, "gondor_ith_watcher", 3);
    }

    // --- Gondor Clan Mappings ---

    private static void InitializeGondorClans()
    {
        AddClan("clan_empire_west_1", "gondor_ano_peasant", 7, "gondor_mt_trainee", 3);
        AddClan("clan_empire_west_2", "gondor_bel_recruit", 7, "gondor_da_noble", 3);
        AddClan("clan_empire_west_3", "gondor_leb_militia", 7, "gondor_pel_skirmisher", 3);
        AddClan("clan_empire_west_4", "gondor_lam_clansman", 7, "gondor_cal_noble", 3);
        AddClan("clan_empire_west_5", "gondor_loss_lumberman", 7, "gondor_loss_noble", 3);
        AddClan("clan_empire_west_6", "gondor_pg_volunteer", 8, "gondor_pg_volunteer", 2);
        AddClan("clan_empire_west_7", "gondor_lam_clansman", 7, "gondor_cal_noble", 3);
        AddClan("clan_empire_west_8", "gondor_har_conscript", 8, "gondor_har_conscript", 2);
        AddClan("clan_empire_west_9", "gondor_anf_levy", 7, "gondor_brv_bowman", 3);
    }

    // --- Gondor Culture Fallback ---

    private static void InitializeGondorCulture()
    {
        CultureMap["gondor"] = new List<VolunteerChance>
        {
            new VolunteerChance("gondor_ano_peasant", 7),
            new VolunteerChance("gondor_bel_recruit", 1),
            new VolunteerChance("gondor_lam_clansman", 1),
            new VolunteerChance("gondor_loss_lumberman", 1)
        };
    }

    // --- Dol Guldur Settlement Mappings ---

    private static void InitializeDolGuldurSettlements()
    {
        AddSettlement("town_DG1", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddSettlement("castle_DG1", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddSettlement("castle_DG2", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddSettlement("castle_DG3", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
    }

    // --- Dol Guldur Clan Mappings ---

    private static void InitializeDolGuldurClans()
    {
        AddClan("clan_dolguldur_1", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddClan("clan_dolguldur_2", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddClan("clan_dolguldur_3", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddClan("clan_dolguldur_4", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddClan("clan_dolguldur_5", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
        AddClan("clan_dolguldur_6", "dg_goblin_slave", 7, "dg_khamul_shadow_initiate", 3);
    }

    // --- Dol Guldur Culture Fallback ---

    private static void InitializeDolGuldurCulture()
    {
        CultureMap["dolguldur"] = new List<VolunteerChance>
        {
            new VolunteerChance("dg_goblin_slave", 5),
            new VolunteerChance("dg_uruk_warrior", 3),
            new VolunteerChance("dg_khamul_shadow_initiate", 2)
        };
    }

    // --- Erebor Settlement Mappings ---

    private static void InitializeEreborSettlements()
    {
        // Towns
        AddSettlement("town_E1", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("town_E2", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("town_E3", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("town_E4", "erebor_reg_miner", 5, "erebor_noble", 3);

        // Castles
        AddSettlement("castle_E1", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E2", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E3", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E4", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E5", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E6", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E7", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E8", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddSettlement("castle_E9", "erebor_reg_miner", 5, "erebor_noble", 3);
    }

    // --- Erebor Clan Mappings ---

    private static void InitializeEreborClans()
    {
        AddClan("clan_erebor_1", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddClan("clan_erebor_2", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddClan("clan_erebor_3", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddClan("clan_erebor_4", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddClan("clan_erebor_5", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddClan("clan_erebor_6", "erebor_reg_miner", 5, "erebor_noble", 3);
        AddClan("clan_erebor_7", "erebor_reg_miner", 5, "erebor_noble", 3);
    }

    // --- Erebor Culture Fallback ---

    private static void InitializeEreborCulture()
    {
        CultureMap["erebor"] = new List<VolunteerChance>
        {
            new VolunteerChance("erebor_reg_miner", 5),
            new VolunteerChance("erebor_noble", 3),
            new VolunteerChance("iron_hills_reg_recruit", 2)
        };
    }

    // --- Shaghâna Clan Mappings ---

    private static void InitializeShaghanaClans()
    {
        AddClan("clan_shaghana_1", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_2", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_3", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_4", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_5", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_6", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_7", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_8", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_shaghana_9", "harad_levy", 7, "harad_noble", 3);
    }

    // --- Shaghâna Culture Fallback ---

    private static void InitializeShaghânaCulture()
    {
        CultureMap["shaghana"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy", 7),
            new VolunteerChance("harad_noble", 3)
        };
    }

    // --- Âbanissa Clan Mappings ---

    private static void InitializeAbanissaClans()
    {
        AddClan("clan_abanissa_1", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_abanissa_2", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_abanissa_3", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_abanissa_4", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_abanissa_5", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_abanissa_6", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_abanissa_7", "harad_levy", 7, "harad_noble", 3);
        AddClan("clan_abanissa_8", "harad_levy", 7, "harad_noble", 3);
    }

    // --- Âbanissa Culture Fallback ---

    private static void InitializeAbanissaCulture()
    {
        CultureMap["abanissa"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy", 7),
            new VolunteerChance("harad_noble", 3)
        };
    }

    // --- Helpers ---

    private static void AddSettlement(string settlementId, string regularId, int regularWeight, string nobleId, int nobleWeight)
    {
        SettlementMap[settlementId] = new List<VolunteerChance>
        {
            new VolunteerChance(regularId, regularWeight),
            new VolunteerChance(nobleId, nobleWeight)
        };
    }

    private static void AddClan(string clanId, string regularId, int regularWeight, string nobleId, int nobleWeight)
    {
        ClanMap[clanId] = new List<VolunteerChance>
        {
            new VolunteerChance(regularId, regularWeight),
            new VolunteerChance(nobleId, nobleWeight)
        };
    }
}
