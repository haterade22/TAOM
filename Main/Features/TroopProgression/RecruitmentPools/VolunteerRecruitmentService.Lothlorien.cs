using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Lothlórien (settlement, clan, and culture pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
//
// Three entry troops (troops_lothlorien.xml): lorien_scout (the regular tree), lorien_mw_sentry (the
// Marchwardens, the border guard of Talas Duiren) and lorien_gl_warrior (the Galadhrim nobles).
public partial class VolunteerRecruitmentService
{
    // --- Lothlorien Settlement Mappings ---

    private static void InitializeLothlorienSettlements()
    {
        // Caras Galadhon and Cerin Amroth: the regular tree, with Galadhrim nobles at the heart of the wood.
        AddSettlement("town_L1",   ("lorien_scout", 6), ("lorien_gl_warrior", 2));
        AddSettlement("castle_L1", ("lorien_scout", 5), ("lorien_gl_warrior", 3));
        // Talas Duiren: the Marchwardens' seat.
        AddSettlement("castle_L2", ("lorien_mw_sentry", 5), ("lorien_scout", 3));
        AddSettlement("castle_L3", ("lorien_scout", 6), ("lorien_mw_sentry", 1), ("lorien_gl_warrior", 1));
    }

    // --- Lothlorien Clan Mappings ---

    private static void InitializeLothlorienClans()
    {
        AddClan("clan_lothlorien_1", ("lorien_scout", 5), ("lorien_gl_warrior", 3));
        // Wardens of the Naith: the Marchwardens' clan.
        AddClan("clan_lothlorien_2", ("lorien_mw_sentry", 5), ("lorien_scout", 3));
    }

    // --- Lothlorien Culture Fallback ---

    private static void InitializeLothlorienCulture()
    {
        CultureMap["lothlorien"] = new List<VolunteerChance>
        {
            new VolunteerChance("lorien_scout", 6),
            new VolunteerChance("lorien_gl_warrior", 2),
            new VolunteerChance("lorien_mw_sentry", 1)
        };
    }
}
