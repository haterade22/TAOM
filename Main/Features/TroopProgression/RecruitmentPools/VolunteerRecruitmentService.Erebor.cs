using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Erebor / Iron Hills (settlement, clan, and culture pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Erebor Settlement Mappings ---

    // Erebor recruitment is a mix of Erebor + Iron Hills troops (same blend as the culture pool):
    // Erebor-leaning 8:4 weight (miner 5 / noble 3 / Iron Hills recruit 2 / Iron Hills noble 2).
    // Applied to BOTH settlements and clans — settlement pools are checked first, so the mix must
    // live there too or it would never surface in the mapped Erebor towns/castles.
    // Total 18. Every entry after the first four is appended, never inserted, so the cumulative
    // ranges of the ones above it never move and the Next(N) stubs in the test file keep their
    // expected values when N changes.
    // Entries 5 and 6 are reachability fixes: the Iron Pass line (ironpass_recruit -> warrior ->
    // infantry -> axeman -> ... + the ironpass_arbalest ranged sub-line) and the Erebor Oathsworn
    // elite line (erebor_oathsworn -> legionary -> royal_legionary) were fielded by AI lords but
    // orphaned from every pool. Iron Pass recruit at modest weight (2); the L36 Oathsworn elite as
    // a rare line-entry (1).
    // Entry 7 is the ram-cavalry branch root — see the block below.
    private static readonly (string, int)[] EreborMix =
    {
        ("erebor_reg_miner",       5),
        ("erebor_noble",           3),
        ("iron_hills_reg_recruit", 2),
        ("iron_hills_noble",       2),
        ("ironpass_recruit",       2),  // Iron Pass line entry
        ("erebor_oathsworn",       1),  // Oathsworn elite line entry
        ("ironpass_ram_herder",    3),  // war-ram branch entry
    };

    // #515 (Ironpass ram cavalry): the four ARMOURED ram troops are still deliberately NOT pooled.
    // What changed is that the branch now has a root of its own. ironpass_ram_herder (16) is an
    // is_basic_troop root at the same depth as ironpass_warrior (16) and upgrades into ironpass_ram_rider (21),
    // so a player who wants rams starts at the bottom of a branch rather than being handed an
    // armoured level-21 rider by a village notable. That was #515's actual objection, and it is
    // answered rather than reversed: the ironpass_warrior -> ironpass_ram_rider edge is untouched,
    // so both routes still reach the line.
    //
    // Why the branch needed a root at all: before this, rams appeared at no notable anywhere, and
    // players reported never seeing them. Precedent for the shape is Rohan, a cavalry culture that
    // pools only its seven is_basic_troop recruits and reaches every horseman by upgrade.
    //
    // Worth knowing when tuning this: vanilla RecruitmentCampaignBehavior only promotes a notable's
    // slot while Tier < MaxVolunteerTier, and TAOM's MaxVolunteerTier is 6 = level 31-35. So a slot
    // seeded with the herder climbs to ironpass_ram_breaker (31) and stops. The vanguard (36) and
    // the marshal (41) are unreachable through a notable by design, which is why the Gems gate in
    // special_resources/troop_resource_costs.xml starts exactly at 31.
    //
    // Pinned by EreborRamCavalry_IsNotOfferedByAnyVolunteerPool (the four armoured ids, plus the
    // marshal) and EreborRamCavalry_IsReachableFromAPooledRoot in the test file.

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
            new VolunteerChance("erebor_oathsworn", 1),  // Oathsworn elite line entry
            // War-ram branch root (mirror EreborMix). Appended last: keeps every cumulative range
            // above it unchanged.
            new VolunteerChance("ironpass_ram_herder", 3)
        };
    }
}
