using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Lothlórien (settlement, clan, and culture pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Lothlorien Settlement Mappings (temporary: borrows Rivendell troops until troops_lothlorien.xml is built) ---

    private static void InitializeLothlorienSettlements()
    {
        AddSettlement("town_L1",   ("imladris_recruit", 5), ("imladris_infantry", 3));
        AddSettlement("castle_L1", ("imladris_recruit", 5), ("imladris_infantry", 3));
        AddSettlement("castle_L2", ("imladris_recruit", 5), ("imladris_infantry", 3));
        AddSettlement("castle_L3", ("imladris_recruit", 5), ("imladris_infantry", 3));
    }

    // --- Lothlorien Clan Mappings ---

    private static void InitializeLothlorienClans()
    {
        AddClan("clan_lothlorien_1", ("imladris_recruit", 5), ("imladris_infantry", 3));
    }

    // --- Lothlorien Culture Fallback ---

    private static void InitializeLothlorienCulture()
    {
        CultureMap["lothlorien"] = new List<VolunteerChance>
        {
            new VolunteerChance("imladris_recruit", 5),
            new VolunteerChance("imladris_infantry", 3),
            new VolunteerChance("imladris_bowman", 2)
        };
    }
}
