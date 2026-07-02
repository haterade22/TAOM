using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Erebor / Iron Hills (settlement, clan, and culture pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    private static void InitializeEreborSettlements()
    {
        // Towns
        AddSettlement("town_E1", EreborMix);
        AddSettlement("town_E2", EreborMix);
        AddSettlement("town_E3", EreborMix);
        AddSettlement("town_E4", EreborMix);

        // Castles
        AddSettlement("castle_E1", EreborMix);
        AddSettlement("castle_E2", EreborMix);
        AddSettlement("castle_E3", EreborMix);
        AddSettlement("castle_E4", EreborMix);
        AddSettlement("castle_E5", EreborMix);
        AddSettlement("castle_E6", EreborMix);
        AddSettlement("castle_E7", EreborMix);
        AddSettlement("castle_E8", EreborMix);
        AddSettlement("castle_E9", EreborMix);
    }

    // --- Erebor Clan Mappings ---

    private static void InitializeEreborClans()
    {
        AddClan("clan_erebor_1", EreborMix);
        AddClan("clan_erebor_2", EreborMix);
        AddClan("clan_erebor_3", EreborMix);
        AddClan("clan_erebor_4", EreborMix);
        AddClan("clan_erebor_5", EreborMix);
        AddClan("clan_erebor_6", EreborMix);
        AddClan("clan_erebor_7", EreborMix);
    }

    // --- Erebor Culture Fallback ---

    private static void InitializeEreborCulture()
    {
        CultureMap["erebor"] = new List<VolunteerChance>
        {
            new VolunteerChance("erebor_reg_miner", 5),
            new VolunteerChance("erebor_noble", 3),
            new VolunteerChance("iron_hills_reg_recruit", 2),
            // T2 entry-point of the Iron Hills Noble line added in #212 KEYforce revamp.
            // Without this, the 13-troop noble line is fielded by AI but not recruitable in villages.
            new VolunteerChance("iron_hills_noble", 2),
            // Reachability fixes (mirror EreborMix): Iron Pass line + Oathsworn elite line.
            new VolunteerChance("ironpass_recruit", 2),  // Iron Pass line entry
            new VolunteerChance("erebor_oathsworn", 1)   // Oathsworn elite line entry
        };
    }
}
