using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Rhûn (settlement + culture pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Rhun Settlement Mappings ---
    // Six regional pools themed by sub-faction (Dragon-Wrath / Balcoth / Far-Rhun / Wain / mixed / Kharaghul).
    // Castle entries cover all bound villages via VolunteerContextAdapter's BoundSettlementId fallback.

    private static void InitializeRhunSettlements()
    {
        // Dragon-Wrath pool: Khûndol + Mârdûn + Tarlat Arlan + Khûsar
        (string, int)[] dragonWrathPool =
        {
            ("dragon_wrath_acolyte",  3),
            ("dragon_wrath_archer",   1),
            ("dragon_wrath_infantry", 1),
            ("dragon_wrath_lancer",   1),
            ("darkhun_recruit",       2),
            ("black_sun_trainee",     1),
            ("loke_rim_initiate",     1),
        };
        AddSettlement("town_RU7",   dragonWrathPool);
        AddSettlement("castle_RU1", dragonWrathPool);
        AddSettlement("castle_RU2", dragonWrathPool);
        AddSettlement("castle_RU3", dragonWrathPool);

        // Balcoth pool: Ûrushban + Nîrakh + Vorgavuld + Castle RU9
        (string, int)[] balcothPool =
        {
            ("balcoth_volunteer", 5),
            ("balcoth_archer",    2),
            ("balcoth_axeman",    1),
            ("loke_rim_initiate", 2),
        };
        AddSettlement("town_RU4",    balcothPool);
        AddSettlement("castle_RU10", balcothPool);
        AddSettlement("town_RU3",    balcothPool);
        AddSettlement("castle_RU9",  balcothPool);

        // Far-Rhun pool: Sârt + Ulbarath + Chêya
        // far_rhun_horse_master appended (reachability fix): its cavalry line was an AI-only orphan.
        (string, int)[] farRhunPool =
        {
            ("far_rhun_levy",        4),
            ("far_rhun_footman",     2),
            ("far_rhun_horseman",    3),
            ("loke_rim_initiate",    1),
            ("far_rhun_horse_master", 1),  // cavalry line entry (orphaned before)
        };
        AddSettlement("town_RU5",    farRhunPool);
        AddSettlement("castle_RU11", farRhunPool);
        AddSettlement("castle_RU12", farRhunPool);

        // Wain pool: Tôrcâin + Kârashûn + Kelepar + Rûartar
        (string, int)[] wainPool =
        {
            ("loke_rim_initiate",  1),
            ("wain_youngblood",    5),
            ("wain_glaiveman",     2),
            ("wainrider_cavalry",  2),
        };
        AddSettlement("castle_RU7", wainPool);
        AddSettlement("castle_RU8", wainPool);
        AddSettlement("town_RU6",   wainPool);
        AddSettlement("castle_RU6", wainPool);

        // Mixed pool: Mistrand + Lest + Samârnûl (generic Rhun pool).
        // easterling_recruit appended (reachability fix): the easterling_*_new tree (footman ->
        // swordsman/halberdier -> veterans + bowman/skirmisher/archer + cavalry) had no recruitable
        // root anywhere. The mixed pool is the generic Rhun home, so easterlings live here + in culture.
        (string, int)[] mixedPool =
        {
            ("balcoth_volunteer",    1),
            ("black_sun_trainee",    1),
            ("darkhun_recruit",      1),
            ("dragon_wrath_acolyte", 1),
            ("far_rhun_levy",        1),
            ("kharaghul_youth",      1),
            ("loke_rim_initiate",    1),
            ("sagarun_deckhand",     1),
            ("wain_youngblood",      2),
            ("easterling_recruit",   2),  // easterling line entry (orphaned before)
        };
        AddSettlement("town_RU1",   mixedPool);
        AddSettlement("town_RU2",   mixedPool);
        AddSettlement("castle_RU4", mixedPool);

        // Kharaghul pool: Iôrig + Ulathar
        // kharaghul_horse_master appended (reachability fix): its cavalry line was an AI-only orphan.
        (string, int)[] kharaghulPool =
        {
            ("loke_rim_initiate",      1),
            ("kharaghul_youth",        5),
            ("kharaghul_raider",       2),
            ("kharaghul_horse_scout",  2),
            ("kharaghul_horse_master", 1),  // cavalry line entry (orphaned before)
        };
        AddSettlement("town_RU8",   kharaghulPool);
        AddSettlement("castle_RU5", kharaghulPool);
    }

    // --- Rhun Culture Fallback ---
    // Engine culture id is "khuzait" (per .claude/rules/xml-data.md — XSLT cultures use vanilla engine IDs).

    private static void InitializeRhunCulture()
    {
        CultureMap["khuzait"] = new List<VolunteerChance>
        {
            new VolunteerChance("balcoth_volunteer",    1),
            new VolunteerChance("black_sun_trainee",    1),
            new VolunteerChance("darkhun_recruit",      1),
            new VolunteerChance("dragon_wrath_acolyte", 1),
            new VolunteerChance("far_rhun_levy",        1),
            new VolunteerChance("kharaghul_youth",      1),
            new VolunteerChance("loke_rim_initiate",    1),
            new VolunteerChance("sagarun_deckhand",     1),
            new VolunteerChance("wain_youngblood",      2),
            // Reachability fix (mirror the mixed pool): easterling line entry for converted fiefs.
            new VolunteerChance("easterling_recruit",   2),
        };

        // Variag (battania) shares the Rhun pool. The Khand cluster recruited exactly this until
        // 2026-08-04, when the 26 K-series settlements were retagged from khuzait to battania so
        // the Variag culture would stop being landless (crash 099f650c — vanilla SpawnLordParty
        // calls Settlement.All.First(x => x.Culture == hero.Culture) with no guard). Without an
        // entry here the retag would have silently dropped Khand's volunteers: the cascade ends at
        // ResolvePool(CultureId, CultureMap) and the K-series settlements have no per-settlement
        // pools. It also makes battania a valid CultureConversion target — HasCulturePool gates
        // that, so a fief taken by a Variag clan could never convert before.
        // Khand has no roster of its own; re-theme here if one is ever authored.
        CultureMap["battania"] = new List<VolunteerChance>(CultureMap["khuzait"]);
    }
}
