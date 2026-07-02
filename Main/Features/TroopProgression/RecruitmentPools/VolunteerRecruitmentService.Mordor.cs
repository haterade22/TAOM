using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Mordor (settlement + culture pools; no clan pools by explicit user choice) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Mordor Settlement Mappings ---
    // Two distinct pools by settlement type:
    //   - Towns (3): canonical Mordor pool with Morannon as the dominant elite-orc path (weight 5/15),
    //                Black Uruks as the rare elite (weight 3), orcs as the common baseline (weight 4),
    //                plus 3 specialist tier-2 entries.
    //   - Castles (8): town pool MINUS Black Uruks (per user spec: BU recruitable in towns only);
    //                  Morannon weight 4 (still > the orc_recruit common baseline tier-wise as elite line).
    //
    // Per user direction (2026-06-08): Morannon troops must be MORE plentiful than Black Uruks in Mordor.
    // Weight 5 for Morannon vs 3 for Black Uruk in towns satisfies that.
    //
    // town_ES2 (Pelgaur / Minas Morgul) has the existing AddSettlementConditional Ithil Guard rule
    // that fires BEFORE this SettlementMap lookup when Gondor owns the town. When Mordor owns
    // (default), the conditional predicate fails and resolution falls through to this town pool.

    private static void InitializeMordorSettlements()
    {
        (string, int)[] townPool =
        {
            ("mordor_uruk_grunt",   3),  // Black Uruk Grunt — line entry; rare elite
            ("mordor_orc_recruit",  4),  // Orc Recruit — common baseline
            ("mordor_orc_impaler",  1),  // mid-tier orc polearm specialist
            ("mordor_orc_hunter",   1),  // mid-tier orc ranged
            ("mordor_warg_tamer",   1),  // warg cavalry line entry
            ("morannon_recruit",    5),  // Morannon Recruit — dominant elite-orc (Black Gate garrison)
        };
        AddSettlement("town_ES1", townPool);  // Danustica
        AddSettlement("town_ES2", townPool);  // Pelgaur — falls through Ithil Guard conditional when Mordor-owned
        AddSettlement("town_ES3", townPool);  // Tharbilid

        (string, int)[] castlePool =
        {
            ("mordor_orc_recruit",  4),
            ("mordor_orc_impaler",  1),
            ("mordor_orc_hunter",   1),
            ("mordor_warg_tamer",   1),
            ("morannon_recruit",    4),  // Morannon Recruit — elite-orc presence in fortresses
        };
        AddSettlement("castle_ES1", castlePool);  // The Morannon
        AddSettlement("castle_ES2", castlePool);  // Carach Angren
        AddSettlement("castle_ES3", castlePool);  // Cirith Ungol
        AddSettlement("castle_ES4", castlePool);  // Mornaur
        AddSettlement("castle_ES5", castlePool);  // Barad Nûrn
        AddSettlement("castle_ES6", castlePool);  // Cirith Nargil
        AddSettlement("castle_ES7", castlePool);  // Barad Wath
        AddSettlement("castle_ES8", castlePool);  // Lûglurag
    }

    // --- Mordor Culture Fallback ---
    // Matches the town pool (includes Black Uruks + Morannon). Safety net for any Mordor settlement
    // not explicitly mapped above — none expected, but explicit > implicit per simplicity-criterion.md.
    private static void InitializeMordorCulture()
    {
        CultureMap["mordor"] = new List<VolunteerChance>
        {
            new VolunteerChance("mordor_uruk_grunt",   3),
            new VolunteerChance("mordor_orc_recruit",  4),
            new VolunteerChance("mordor_orc_impaler",  1),
            new VolunteerChance("mordor_orc_hunter",   1),
            new VolunteerChance("mordor_warg_tamer",   1),
            new VolunteerChance("morannon_recruit",    5),  // Morannon Recruit — dominant elite-orc
        };
    }
}
