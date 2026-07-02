using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Mirkwood / the Woodland Realm (culture pool) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Mirkwood (Culture.mirkwood) Culture Fallback ---
    // Mirkwood (the Woodland Realm) shipped with NO recruitment wiring at all — no "mirkwood"
    // CultureMap key and no settlement/clan pools — so the player could recruit nothing at a
    // Mirkwood fief. The Mirkwood roster is intentionally high-tier (every non-militia troop is
    // L36+ elite Silvan elves); mirkwood_recruit (L36, the culture basic_troop) is the sole line
    // root, and the whole infantry / ranged (sentinels) / cavalry (rochenlas) tree upgrades from it.
    private static void InitializeMirkwoodCulture()
    {
        CultureMap["mirkwood"] = new List<VolunteerChance>
        {
            new VolunteerChance("mirkwood_recruit", 1),
        };
    }
}
