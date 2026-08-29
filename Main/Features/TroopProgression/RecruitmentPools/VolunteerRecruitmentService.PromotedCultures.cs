using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for the two cultures promoted out of a borrowed one — Blue Craig
// (carved off Culture.goblin) and Lindon (carved off Culture.rivendell) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
//
// Both kingdoms previously ran on their host culture's pool, so recruitment in Blue Craig drew
// Goblin-town troops and recruitment in Mithlond drew Imladris ones. Once each got its own culture
// the host pool no longer answered for it, and every troop in the new trees became unreachable —
// caught immediately by AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot (42 orphans)
// and by the tavern-mercenary test, both of which derive their expectations from the culture list.
public partial class VolunteerRecruitmentService
{
    private static void InitializeBlueCraigCulture()
    {
        // Western goblins of the Ered Luin. The clone of the Goblin-town tree it used to field was
        // retired, so this is now the Goblin-town pool itself: a cheap melee root plus the
        // archer-line entry, which does not sit on the melee upgrade path and so has to be
        // reachable directly or the whole bow branch is AI-only. Blue Craig's one bespoke troop,
        // bluecraig_bolgs_ironfang, is a T7 capstone reached by upgrade rather than recruited.
        CultureMap["bluecraig"] = new List<VolunteerChance>
        {
            new VolunteerChance("goblin_snaga", 7),
            new VolunteerChance("goblin_grunt", 2),
            new VolunteerChance("goblin_fighter", 1),
            new VolunteerChance("goblin_hunter", 2)  // archer line entry
        };
    }

    private static void InitializeLindonCulture()
    {
        // Círdan's Falathrim. Mirrors the Rivendell pool's structure because the tree is cloned
        // from it: the common Mithlond line at the top, then one entry per elite branch that the
        // common line cannot upgrade into.
        CultureMap["lindon"] = new List<VolunteerChance>
        {
            new VolunteerChance("lindon_imladris_recruit", 5),
            new VolunteerChance("lindon_imladris_infantry", 3),
            new VolunteerChance("lindon_imladris_bowman", 2),
            new VolunteerChance("lindon_noble", 1),                 // noble cavalry line entry
            new VolunteerChance("lindon_knight_golden_flower", 1)   // Golden-Flower foot elite line entry
        };
    }
}
