using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Rivendell / Lindon (culture pool) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    private static void InitializeRivendellCulture()
    {
        // Noldor pool shared by the Rivendell kingdom and the new Lindon kingdom (both
        // Culture.rivendell). CultureMap["rivendell"] was previously absent, so rivendell-culture
        // settlements with no settlement/clan pool returned null — this fills that gap.
        CultureMap["rivendell"] = new List<VolunteerChance>
        {
            new VolunteerChance("imladris_recruit", 5),
            new VolunteerChance("imladris_infantry", 3),
            new VolunteerChance("imladris_bowman", 2),
            // Reachability fix: the named Rivendell elite lines — the noble cavalry line
            // (rivendell_noble -> royal_guard -> royal_knight -> high_captain/glorfindel_guard)
            // and the Gondolin / Golden-Flower foot elites (rivendell_knight_golden_flower ->
            // warden_gondolin / gondolin_battlemaster) — were AI-only orphans. Add their line
            // entries at rare weight 1 each (they are L36+ high elites; the imladris line stays
            // the common recruit).
            new VolunteerChance("rivendell_noble", 1),                 // noble cavalry line entry
            new VolunteerChance("rivendell_knight_golden_flower", 1)   // Gondolin/Golden-Flower elite line entry
        };
    }
}
