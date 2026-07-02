using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Dale / Barding (Lake-Town settlement override + culture pool) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Dale Settlement-Specific Pools ---
    // Only town_S1 (Lake-Town) gets a settlement-specific override per user spec. All
    // other Sturgia settlements fall through to the culture pool in InitializeDaleCulture.
    //
    // Lake-Town pool: heavy weight on the Lake-Town Peasant (9) — it's a fishing-folk
    // settlement, so common recruits are smallfolk. Dalian Levy (1) preserves a rare
    // route into the royal lines for players who specifically want to recruit there.
    private static void InitializeDaleSettlements()
    {
        AddSettlement("town_S1",
            ("dale_recruit", 9),  // Lake-Town Peasant
            ("dale_squire",  1)); // Dalian Levy
    }

    // --- Dale Culture Fallback ---
    // Dale (vanilla Culture.sturgia renamed to "Barding" via spcultures.xslt) recruits from
    // a single culture-level pool — no per-settlement / per-clan flavor variants in this
    // session. Sturgia kingdom settlements (Lake-town, Dale proper) all draw from this.
    //
    // Pool reflects the user-designed Dale tree: Dalian Levy (weight 4) is the most common
    // recruit and serves as the royal-line root. The 6 other slots (weight 1 each) cover
    // one representative entry troop for each branch + the Lake-Town levy entry. Total weight 10.
    //
    // Branch entry points:
    //   - Dalian Levy        (dale_squire)       → 4 royal branches (riverman/militia/yeoman/crossbow/merchant)
    //   - Dalian Riverman    (dale_riverman)     → Riverman spear+shield line
    //   - Dalian Militia     (dale_man_at_arms)  → Great Infantry line (NOT dale_militia / Lake-Town Militia)
    //   - Dalian Yeoman      (dale_bowman)       → Excellent Archers (bow) line
    //   - Dalian Crossbowman (dale_crossbowman)  → Royal Crossbow line
    //   - Dalian Merchant Guard (dale_outrider)  → Cavalry split (Light + Heavy)
    //   - Lake-Town Peasant  (dale_recruit)      → Lake-Town levy + Watch + Pikeman lines
    private static void InitializeDaleCulture()
    {
        CultureMap["sturgia"] = new List<VolunteerChance>
        {
            new VolunteerChance("dale_squire",       4),
            new VolunteerChance("dale_riverman",     1),
            new VolunteerChance("dale_man_at_arms",  1),
            new VolunteerChance("dale_bowman",       1),
            new VolunteerChance("dale_crossbowman",  1),
            new VolunteerChance("dale_outrider",     1),
            new VolunteerChance("dale_recruit",      1),
        };
    }
}
