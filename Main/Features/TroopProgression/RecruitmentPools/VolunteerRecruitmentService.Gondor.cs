using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Gondor (regional settlement pools, clan pools, culture fallback; the JSON loader in the core file overrides these at runtime) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
//
// SETTLEMENT POOLS BELOW MIRROR ModuleData/recruitment_pools/gondor.json. In-game the JSON loader
// overwrites every key here, so this layer is live only in degraded mode (JSON missing or
// unparseable) and in the unit tests. It used to diverge from the JSON — the whole Ithil Guard line
// was unreachable here, and three ids were pooled here that the JSON never offered — which meant the
// tests asserted behaviour the game never exhibited. The two are now kept in lockstep by
// GondorPools_HandWrittenFallback_MatchesProductionJson.
//
// Weights are the smallest integers holding the JSON's ratios (PickWeighted is cumulative, so only
// ratios matter). Towns: the regular line totals 60% and the household / settlement-specific line
// 40% (20% until 2026-09-13, raised on player feedback that the household lines were too rare), with
// Minas Tirith and Osgiliath at 50 / 40 / 10 for the Ithilien Ranger. Castles that have a household
// line offer only that line (Glanhir, Morlad, Bar-en-Siril, Caras Tolfalas, Linhir; Cair Andros keeps
// the ranger at 10%); the other castles keep their pure regional pool.
//
// Scope note: only the 27 towns/castles are mirrored, not the JSON's 70 villages. A village with no
// pool of its own resolves through the BoundSettlementId leg of ResolveStandardCascade and inherits
// its bound town's pool — close to, but not identical with, the JSON, which gives villages their own
// pure regional pool. That residual gap is acceptable for a degraded-mode safety net; duplicating 66
// more entries here is not.
public partial class VolunteerRecruitmentService
{
    // --- Gondor Settlement Mappings ---

    // Ithil Guard gates on the LIVE owner culture, not the settlement's authored culture — Minas Ithil
    // only raises Gondor's watchers once Gondor holds it. Mirrors the predicate the JSON loader builds
    // for the same group (GondorRecruitmentJsonLoader.ResolveCondition).
    private static bool GondorOwns(VolunteerContext context) => context.OwnerCultureId == "gondor";

    private static void InitializeGondorSettlements()
    {
        // Towns
        // Minas Tirith: Anorien 50 / Citadel Guard 40 / Ithilien Ranger 10
        AddSettlement("town_EW1",
            ("gondor_ano_peasant",        1),
            ("gondor_ano_archer_militia", 1),
            ("gondor_ano_militia",        1),
            ("gondor_ano_footman",        1),
            ("gondor_ano_skirmisher",     1),
            ("gondor_mt_trainee",         2),
            ("gondor_mt_veteran",         2),
            ("gondor_ithilien_ranger",    1));
        // West Osgiliath: Anorien 50 / Osgiliath 40 / Ithilien Ranger 10
        AddSettlement("town_EW2",
            ("gondor_ano_peasant",        1),
            ("gondor_ano_archer_militia", 1),
            ("gondor_ano_militia",        1),
            ("gondor_ano_footman",        1),
            ("gondor_ano_skirmisher",     1),
            ("gondor_osg_veteran",        2),
            ("gondor_osg_skirmisher",     2),
            ("gondor_ithilien_ranger",    1));
        // East Osgiliath: Anorien 50 / Osgiliath 40 / Ithilien Ranger 10
        AddSettlement("town_EW3",
            ("gondor_ano_peasant",        1),
            ("gondor_ano_archer_militia", 1),
            ("gondor_ano_militia",        1),
            ("gondor_ano_footman",        1),
            ("gondor_ano_skirmisher",     1),
            ("gondor_osg_veteran",        2),
            ("gondor_osg_skirmisher",     2),
            ("gondor_ithilien_ranger",    1));
        // Pelargir: Lebennin 60 / Pelargir 40
        AddSettlement("town_EW4",
            ("gondor_leb_militia",    3),
            ("gondor_leb_skirmisher", 3),
            ("gondor_leb_archer",     3),
            ("gondor_leb_infantry",   3),
            ("gondor_pel_skirmisher", 4),
            ("gondor_pel_veteran",    4));
        // Dol Amroth: Belfalas 60 / Dol Amroth Swan Knights 40
        AddSettlement("town_EW5",
            ("gondor_bel_recruit", 3),
            ("gondor_bel_footman", 3),
            ("gondor_bel_hunter",  3),
            ("gondor_bel_bowman",  3),
            ("gondor_bel_soldier", 3),
            ("gondor_da_noble",    5),
            ("gondor_da_footman",  5));
        // Lond Cirion: Anfalas 60 / Lond-Galen 40
        AddSettlement("town_EW6",
            ("gondor_anf_levy",       1),
            ("gondor_anf_militia",    1),
            ("gondor_anf_footman",    1),
            ("gondor_lg_noble",       1),
            ("gondor_lg_crossbowman", 1));
        // Bar Melui: Lossarnach Regular 60 / Lossarnach Noble 40
        AddSettlement("town_EW7",
            ("gondor_loss_lumberman",  3),
            ("gondor_loss_woodsman",   3),
            ("gondor_loss_axebearer",  3),
            ("gondor_loss_skirmisher", 3),
            ("gondor_loss_noble",      8));
        // Ost Arndir: Pinnath Gelin 60 / Arndir 40
        AddSettlement("town_EW8",
            ("gondor_pg_volunteer", 1),
            ("gondor_pg_militia",   1),
            ("gondor_pg_footman",   1),
            ("gondor_arn_noble",    1),
            ("gondor_arn_noble_t4", 1));
        // Calembel: Lamedon 60 / Calembel 40
        AddSettlement("town_EW9",
            ("gondor_lam_clansman",  1),
            ("gondor_lam_footman",   1),
            ("gondor_lam_swordman",  1),
            ("gondor_cal_noble",     1),
            ("gondor_cal_swordsman", 1));
        // Serelond: Anfalas 60 / Serelond 40
        AddSettlement("town_EW10",
            ("gondor_anf_levy",    1),
            ("gondor_anf_militia", 1),
            ("gondor_anf_footman", 1),
            ("gondor_ser_noble",   1),
            ("gondor_ser_veteran", 1));
        // Methir: Harondor 60 / Methir 40
        AddSettlement("town_EW11",
            ("gondor_har_conscript",  9),
            ("gondor_har_militia",    9),
            ("gondor_har_footman",    9),
            ("gondor_har_skirmisher", 9),
            ("gondor_met_noble",      8),
            ("gondor_met_archer",     8),
            ("gondor_met_glaiveman",  8));

        // Castles
        // Malandilionath / Barahirionath: Lamedon only
        AddSettlement("castle_EW1",
            ("gondor_lam_clansman", 1),
            ("gondor_lam_footman",  1),
            ("gondor_lam_swordman", 1));
        // Glanhir: Ringlo Vale only (its villages Upper Ringló and Vale Village too, in the JSON)
        AddSettlement("castle_EW2",
            ("gondor_ring_peasant", 1),
            ("gondor_ring_militia", 1));
        // Imrazorionath / Garvirionath / Hirilionath: Belfalas only
        AddSettlement("castle_EW3",
            ("gondor_bel_recruit", 1),
            ("gondor_bel_footman", 1),
            ("gondor_bel_hunter",  1),
            ("gondor_bel_bowman",  1),
            ("gondor_bel_soldier", 1));
        // Cair Andros: Cair Andros 90 / Ithilien Ranger 10
        AddSettlement("castle_EW4",
            ("gondor_ca_noble",        9),
            ("gondor_ca_veteran",      9),
            ("gondor_ithilien_ranger", 2));
        // Hurinionath / Caladionath: Anorien only
        AddSettlement("castle_EW5",
            ("gondor_ano_peasant",        1),
            ("gondor_ano_archer_militia", 1),
            ("gondor_ano_militia",        1),
            ("gondor_ano_footman",        1),
            ("gondor_ano_skirmisher",     1));
        // Morlad: Blackroot Vale only (its four villages and Blackroot Village Haven too, in the JSON)
        AddSettlement("castle_EW6",
            ("gondor_brv_bowman", 1),
            ("gondor_brv_scout",  1));
        // Bar-en-Siril: Serelond only (Olindurionath's castle beside Serelond; its villages stay Anfalas)
        AddSettlement("castle_EW7",
            ("gondor_ser_noble",   1),
            ("gondor_ser_veteran", 1));
        // Halboronionath / Danuhirionath: Pinnath Gelin only
        AddSettlement("castle_EW8",
            ("gondor_pg_volunteer", 1),
            ("gondor_pg_militia",   1),
            ("gondor_pg_footman",   1));
        // Caras Tolfalas: Tolfalas only
        AddSettlement("castle_EW9",
            ("gondor_tol_arbalest",    1),
            ("gondor_tol_crossbowman", 1));
        // Imrazorionath / Garvirionath / Hirilionath: Belfalas only
        AddSettlement("castle_EW10",
            ("gondor_bel_recruit", 1),
            ("gondor_bel_footman", 1),
            ("gondor_bel_hunter",  1),
            ("gondor_bel_bowman",  1),
            ("gondor_bel_soldier", 1));
        // Imrazorionath / Garvirionath / Hirilionath: Belfalas only
        AddSettlement("castle_EW11",
            ("gondor_bel_recruit", 1),
            ("gondor_bel_footman", 1),
            ("gondor_bel_hunter",  1),
            ("gondor_bel_bowman",  1),
            ("gondor_bel_soldier", 1));
        // Linhir: Linhir only
        AddSettlement("castle_EW12",
            ("gondor_lin_noble",   1),
            ("gondor_lin_footman", 1));
        // Halboronionath / Danuhirionath: Pinnath Gelin only
        AddSettlement("castle_EW13",
            ("gondor_pg_volunteer", 1),
            ("gondor_pg_militia",   1),
            ("gondor_pg_footman",   1));
        // Earnurionath: Lebennin only
        AddSettlement("castle_EW14",
            ("gondor_leb_militia",    1),
            ("gondor_leb_skirmisher", 1),
            ("gondor_leb_archer",     1),
            ("gondor_leb_infantry",   1));
        // Hyarthulionath: Harondor only
        AddSettlement("castle_EW15",
            ("gondor_har_conscript",  1),
            ("gondor_har_militia",    1),
            ("gondor_har_footman",    1),
            ("gondor_har_skirmisher", 1));
        // Hyarthulionath: Harondor only
        AddSettlement("castle_EW16",
            ("gondor_har_conscript",  1),
            ("gondor_har_militia",    1),
            ("gondor_har_footman",    1),
            ("gondor_har_skirmisher", 1));

        // Conditional — Minas Ithil / Minas Morgul: Ithil Guard, only while Gondor holds it.
        AddSettlementConditional("town_ES2",
            GondorOwns,
            ("gondor_ith_watcher", 1),
            ("gondor_ith_veteran", 1));
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
