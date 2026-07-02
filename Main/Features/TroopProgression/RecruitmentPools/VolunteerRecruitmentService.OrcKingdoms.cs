using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for the hostile orc kingdoms — Gundabad, Goblins of the High Pass, Misty Mountain orcs (culture-only pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Gundabad Culture Fallback ---
    // Settlements/clans absent — Gundabad in TAOM is hostile faction; volunteer recruitment
    // falls back to culture pool. Per #212 KEYforce troop revamp (Pale Uruk T2–T8).
    private static void InitializeGundabadCulture()
    {
        CultureMap["gundabad"] = new List<VolunteerChance>
        {
            new VolunteerChance("gundabad_snaga", 7),
            new VolunteerChance("gundabad_grunt", 2),
            new VolunteerChance("gundabad_fighter", 1),
            // Reachability fix: the Gundabad archer line (gundabad_hunter -> lurker -> sentry ->
            // archer) and the horse-archer scout line (gundabad_scout -> despoiler) had no path
            // from the melee snaga root, so they never appeared in recruitment. Add their entry
            // troops directly. Hunter is the archer-line entry (2); scout the rarer mounted line (1).
            new VolunteerChance("gundabad_hunter", 2),  // archer line entry
            new VolunteerChance("gundabad_scout", 1)    // horse-archer line entry
        };
    }

    private static void InitializeGoblinCulture()
    {
        // Goblins of the High Pass — swarm faction (mirrors the Gundabad hostile-faction pattern).
        CultureMap["goblin"] = new List<VolunteerChance>
        {
            new VolunteerChance("goblin_snaga", 7),
            new VolunteerChance("goblin_grunt", 2),
            new VolunteerChance("goblin_fighter", 1),
            // Reachability fix (same shape as Gundabad): goblin archer line
            // (goblin_hunter -> lurker -> sentry -> archer) was orphaned from the melee root.
            new VolunteerChance("goblin_hunter", 2)  // archer line entry
        };
    }

    private static void InitializeMistyMountainOrcsCulture()
    {
        // Orc-host of the Misty Mountains — culture-only pool (hostile faction).
        CultureMap["mistymountainorcs"] = new List<VolunteerChance>
        {
            new VolunteerChance("mistymountainorcs_snaga", 7),
            new VolunteerChance("mistymountainorcs_grunt", 2),
            new VolunteerChance("mistymountainorcs_fighter", 1),
            // Reachability fix (same shape as Gundabad): MMO archer line
            // (mistymountainorcs_hunter -> lurker -> sentry -> archer) was orphaned.
            new VolunteerChance("mistymountainorcs_hunter", 2)  // archer line entry
        };
    }
}
