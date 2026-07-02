using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Isengard (culture pool) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Isengard Culture Fallback ---
    // Isengard had no recruitment pool (hostile-faction only; GetVolunteerTroopId returned null).
    // Culture-level pool makes the player able to recruit Uruk-Hai. Weighted toward the L6 Recruit
    // root (4), with the crossbow-line entry (Skirmisher) + Warg-Rider cavalry at 2 each, and the
    // melee mid (Warrior) + bow-line entry (Scout) at 1 each. Total weight 10.
    private static void InitializeIsengardCulture()
    {
        CultureMap["isengard"] = new List<VolunteerChance>
        {
            new VolunteerChance("urukhai_recruit",    4),
            new VolunteerChance("urukhai_skirmisher", 2),  // crossbow-line entry
            new VolunteerChance("orc_warg_scout",     2),  // Warg-Rider cavalry
            new VolunteerChance("urukhai_warrior",    1),  // melee mid
            new VolunteerChance("urukhai_scout",      1),  // bow-line entry
            // Reachability fix: the isengard_orc_* line (rooted at isengard_orc_grunt) and the
            // Orthanc Guard line (rooted at orthanc_chosen) were fielded by AI lords but orphaned
            // from every pool, so players could never recruit/upgrade into them. Grunt is the
            // common Mordor-style orc baseline (3); the L26 Orthanc elite is a rare line-entry (1).
            new VolunteerChance("isengard_orc_grunt", 3),  // orc line + warg_v2 line entry
            new VolunteerChance("orthanc_chosen",     1),  // Orthanc Guard elite line entry
        };
    }
}
