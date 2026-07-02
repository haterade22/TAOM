using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Umbar (culture pool) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Umbar (Culture.umbar) Culture Fallback ---
    // Umbar (the Corsair city-states) likewise shipped with NO recruitment wiring, so its fiefs
    // recruited nothing. aux_basic (L6 auxiliary levy) is the common baseline — a TERMINAL recruit
    // with no upgrade_targets (recruitable but never promotes). umbar_elite (L11) is the entry of the
    // umbar_elite_root* corsair line (the culture basic_troop / bandit_raider) and ALONE connects the
    // whole corsair upgrade tree. Both are pooled so neither is an unreachable orphan.
    private static void InitializeUmbarCulture()
    {
        CultureMap["umbar"] = new List<VolunteerChance>
        {
            new VolunteerChance("aux_basic",   7),
            new VolunteerChance("umbar_elite", 3),
        };
    }
}
