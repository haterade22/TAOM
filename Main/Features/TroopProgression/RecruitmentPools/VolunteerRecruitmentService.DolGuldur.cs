using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Dol Guldur (settlement, clan, and culture pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // Settlement pool (total 18): goblins common, orcs the mid-line meat, a uruk entry, khamul shadow
    // elite, a rare ranged-orc entry, and the spider RIDER at the rare tail (a goblin cavalry troop
    // whose Horse slot carries the Giant Spider mount — vanilla cavalry spawn, no patch).
    private static readonly (string, int)[] DolGuldurSettlementPool =
    {
        ("dg_goblin_slave",           7),
        ("dg_orc_recruit",            4),  // orc line + warg line entry
        ("dg_uruk_foul",              2),  // uruk line entry
        ("dg_khamul_shadow_initiate", 3),
        ("dg_orc_scout",              1),  // ranged-orc line entry (dg_orc_scout -> dg_orc_archer)
        ("taom_spider_creature",      1),  // Giant Spider rider — rare settlement-path tail (keeps pool total 18).
    };

    private static readonly (string, int)[] DolGuldurClanPool =
    {
        ("dg_goblin_slave",           7),
        ("dg_orc_recruit",            4),  // orc line + warg line entry
        ("dg_uruk_foul",              2),  // uruk line entry
        ("dg_khamul_shadow_initiate", 3),
        ("dg_orc_scout",              1),  // ranged-orc line entry
    };

    // --- Dol Guldur Settlement Mappings ---

    private static void InitializeDolGuldurSettlements()
    {
        // taom_spider_creature: the giant spider, recruitable at Dol Guldur fiefs only. It rides in the
        // party roster as a humanoid anchor (race dg_uruk) and spawns + fights as the spider Monster via
        // Patch45_SpiderTroopSpawn. Settlement pools feed BOTH player and AI lord recruitment. Deliberately
        // absent from the clan-path pool (InitializeDolGuldurClans) to keep that source clean.
        // Orc + Uruk line entries (dg_orc_recruit, dg_uruk_foul, dg_orc_scout) are added DIRECTLY to
        // the settlement (and clan) pools — not just the culture fallback — because culture is the
        // lowest-priority pool and every Dol Guldur fief has a settlement/clan pool that shadows it.
        // Without these, the orc line (dg_orc_recruit -> gnasher/warrior/reaver + the warg line that
        // hangs off it) and the uruk line (dg_uruk_foul -> warrior -> ...) were unrecruitable at every
        // DG settlement even though lords fielded them.
        AddSettlement("town_DG1",   DolGuldurSettlementPool);
        AddSettlement("castle_DG1", DolGuldurSettlementPool);
        AddSettlement("castle_DG2", DolGuldurSettlementPool);
        AddSettlement("castle_DG3", DolGuldurSettlementPool);
    }

    // --- Dol Guldur Clan Mappings ---

    private static void InitializeDolGuldurClans()
    {
        // Clan pool (total 17): identical to the settlement pool MINUS the spider rider, which stays
        // exclusive to the settlement-path pool (the spider rider is a per-fief recruit, not a
        // clan-army recruit — see InitializeDolGuldurSettlements).
        for (int i = 1; i <= 6; i++)
            AddClan($"clan_dolguldur_{i}", DolGuldurClanPool);
    }

    // --- Dol Guldur Culture Fallback ---

    private static void InitializeDolGuldurCulture()
    {
        CultureMap["dolguldur"] = new List<VolunteerChance>
        {
            new VolunteerChance("dg_goblin_slave", 5),
            // Orc + uruk line entries — also added here (not only to settlement/clan) so a CONVERTED
            // fief that recruits via the culture pool still gets the full Dol Guldur roster.
            new VolunteerChance("dg_orc_recruit", 3),   // orc line + warg line entry
            new VolunteerChance("dg_uruk_foul", 2),     // uruk line entry (T2; dg_uruk_warrior below is the T3 mid)
            new VolunteerChance("dg_uruk_warrior", 3),
            new VolunteerChance("dg_khamul_shadow_initiate", 2),
            new VolunteerChance("dg_orc_scout", 1),     // ranged-orc line entry
            // Giant spider — culture-fallback recruit for any Dol Guldur fief not in the per-settlement
            // map above. Spawns + fights as the spider Monster via the vanilla cavalry spawn (ridden mount).
            new VolunteerChance("taom_spider_creature", 1)  // Giant Spider — rare culture-fallback recruit (keeps pool total 17).
        };
    }
}
