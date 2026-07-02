using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Gondor (regional settlement pools, clan pools, culture fallback; the JSON loader in the core file overrides these at runtime) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Gondor Settlement Mappings ---

    private static void InitializeGondorSettlements()
    {
        // Towns
        AddSettlement("town_EW1",
            ("gondor_ano_peasant",       6),
            ("gondor_ithilien_ranger",   1),
            ("gondor_mt_fountain_guard", 1),
            ("gondor_mt_trainee",        2));
        AddSettlement("town_EW2",  ("gondor_osg_veteran", 6),     ("gondor_ano_peasant", 4));
        AddSettlement("town_EW3",  ("gondor_osg_veteran", 6),     ("gondor_ano_peasant", 4));
        AddSettlement("town_EW4",  ("gondor_pel_skirmisher", 7),  ("gondor_leb_militia", 3));
        AddSettlement("town_EW5",  ("gondor_da_noble", 7),        ("gondor_bel_recruit", 3));
        AddSettlement("town_EW6",  ("gondor_anf_levy", 7),        ("gondor_anf_guardsman", 3));
        AddSettlement("town_EW7",  ("gondor_leb_militia", 7),     ("gondor_lg_noble", 3));
        AddSettlement("town_EW8",  ("gondor_pg_volunteer", 7),    ("gondor_arn_noble", 3));
        AddSettlement("town_EW9",  ("gondor_cal_noble", 7),       ("gondor_lam_clansman", 3));
        AddSettlement("town_EW10", ("gondor_ser_noble", 7),       ("gondor_anf_levy", 3));
        AddSettlement("town_EW11", ("gondor_met_noble", 7),       ("gondor_ano_peasant", 3));

        // Castles
        AddSettlement("castle_EW1",  ("gondor_ano_peasant", 7),     ("gondor_mt_trainee", 3));
        AddSettlement("castle_EW2",  ("gondor_lam_clansman", 7),    ("gondor_ring_peasant", 3));
        AddSettlement("castle_EW3",  ("gondor_bel_recruit", 7),     ("gondor_da_noble", 3));
        AddSettlement("castle_EW4",  ("gondor_ca_noble", 7),        ("gondor_ano_peasant", 3));
        AddSettlement("castle_EW5",  ("gondor_ano_peasant", 8),     ("gondor_ano_peasant", 2));
        AddSettlement("castle_EW6",  ("gondor_har_conscript", 8),   ("gondor_har_conscript", 2));
        AddSettlement("castle_EW7",  ("gondor_anf_levy", 7),        ("gondor_ser_noble", 3));
        AddSettlement("castle_EW8",  ("gondor_pg_volunteer", 7),    ("gondor_arn_noble", 3));
        AddSettlement("castle_EW9",  ("gondor_tol_arbalest", 7),    ("gondor_bel_recruit", 3));
        AddSettlement("castle_EW10", ("gondor_har_conscript", 7),   ("gondor_met_noble", 3));
        AddSettlement("castle_EW11", ("gondor_bel_recruit", 7),     ("gondor_da_noble", 3));
        AddSettlement("castle_EW12", ("gondor_lin_noble", 7),       ("gondor_leb_militia", 3));
        AddSettlement("castle_EW13", ("gondor_anf_levy", 8),        ("gondor_anf_levy", 2));
        AddSettlement("castle_EW14", ("gondor_leb_militia", 7),     ("gondor_lin_noble", 3));
        AddSettlement("castle_EW15", ("gondor_har_conscript", 7),   ("gondor_met_noble", 3));
        AddSettlement("castle_EW16", ("gondor_har_conscript", 7),   ("gondor_met_noble", 3));
    }

    // --- Gondor Clan Mappings ---

    private static void InitializeGondorClans()
    {
        AddClan("clan_empire_west_1",
            ("gondor_ano_peasant",       6),
            ("gondor_ithilien_ranger",   1),
            ("gondor_mt_fountain_guard", 1),
            ("gondor_mt_trainee",        2));
        AddClan("clan_empire_west_2", ("gondor_bel_recruit", 7),    ("gondor_da_noble", 3));
        AddClan("clan_empire_west_3", ("gondor_leb_militia", 7),    ("gondor_pel_skirmisher", 3));
        AddClan("clan_empire_west_4", ("gondor_lam_clansman", 7),   ("gondor_cal_noble", 3));
        AddClan("clan_empire_west_5", ("gondor_loss_lumberman", 6), ("gondor_loss_axebearer", 2), ("gondor_loss_noble", 2));
        AddClan("clan_empire_west_6", ("gondor_pg_volunteer", 8),   ("gondor_pg_volunteer", 2));
        AddClan("clan_empire_west_7", ("gondor_lam_clansman", 7),   ("gondor_cal_noble", 3));
        AddClan("clan_empire_west_8",
            ("gondor_anf_levy",      5),
            ("gondor_ser_pikeman",   2),
            ("gondor_ser_noble",     2),
            ("gondor_anf_guardsman", 1));
        AddClan("clan_empire_west_9",  ("gondor_brv_bowman", 7),  ("gondor_ano_peasant", 3));
        AddClan("clan_empire_west_10", ("gondor_har_conscript", 7), ("gondor_met_noble", 3));
        AddClan("clan_empire_west_11", ("gondor_ca_noble", 9),    ("gondor_ithilien_ranger", 1));
        AddClan("clan_empire_west_12", ("gondor_lin_noble", 7),   ("gondor_ano_peasant", 3));
        AddClan("clan_empire_west_13", ("gondor_tol_arbalest", 7), ("gondor_bel_recruit", 3));
        AddClan("clan_empire_west_14", ("gondor_anf_levy", 7),    ("gondor_anf_guardsman", 3));
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
}
