using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Dunland (culture pool + totem-clan pools) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Dunland (Culture.empire) Culture Fallback ---
    // "Culture will be them all" — every Dunland is_basic_troop=true troop at equal weight 1:
    // the generic Peasant plus the three totem noble sons (Wolf/Blaidd, Boar/Turch, Raven/Cigfran).
    // SettlementMap is intentionally left empty for Dunland (per user spec).
    private static void InitializeDunlandCulture()
    {
        CultureMap["empire"] = new List<VolunteerChance>
        {
            new VolunteerChance("dunland_peasant",          1),
            new VolunteerChance("dunland_noble_son",        1),  // Blaidd-luth (Wolf)
            new VolunteerChance("dunland_boar_noble_son",   1),  // Turch-luth (Boar)
            new VolunteerChance("dunland_raven_noble_son",  1),  // Cigfran-luth (Raven)
        };
    }

    // --- Dunland Clan Pools ---
    // "Clan will be specific troops by name." The three totem clans with a matching noble son
    // recruit the Peasant + their own noble son. The six totem clans WITHOUT a named noble son
    // (Uch/Ox, Arth/Bear, Hebog/Hawk, Draig/Dragon, Caru/Stag, Avanc) get the full roster
    // (Peasant + all three noble sons) so they still field a complete clan army.
    private static void InitializeDunlandClans()
    {
        // Totem clans with a signature noble son:
        AddClan("clan_empire_north_1",  // Blaidd-luth (Wolf)
            ("dunland_peasant", 1), ("dunland_noble_son", 1));
        AddClan("clan_empire_north_2",  // Turch-luth (Boar)
            ("dunland_peasant", 1), ("dunland_boar_noble_son", 1));
        AddClan("clan_empire_north_5",  // Cigfran-luth (Raven)
            ("dunland_peasant", 1), ("dunland_raven_noble_son", 1));

        // Totem clans without a signature noble son — full roster:
        (string, int)[] allDunland =
        {
            ("dunland_peasant",         1),
            ("dunland_noble_son",       1),
            ("dunland_boar_noble_son",  1),
            ("dunland_raven_noble_son", 1),
        };
        AddClan("clan_empire_north_3", allDunland);  // Uch-luth (Ox)
        AddClan("clan_empire_north_4", allDunland);  // Arth-luth (Bear)
        AddClan("clan_empire_north_6", allDunland);  // Hebog-luth (Hawk)
        AddClan("clan_empire_north_7", allDunland);  // Draig-luth (Dragon)
        AddClan("clan_empire_north_8", allDunland);  // Caru-luth (Stag)
        AddClan("clan_empire_north_9", allDunland);  // Avanc-luth
    }
}
