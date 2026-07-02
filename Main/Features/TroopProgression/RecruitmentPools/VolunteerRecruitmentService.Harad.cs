using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for the Harad sphere — Shaghâna + Âbanissa sub-factions and the aserai Harad kingdom (clan + culture pools, incl. the elephant/mumakil-rider clan gate) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
    // --- Shaghâna Clan Mappings ---

    private static void InitializeShaghanaClans()
    {
        AddClan("clan_shaghana_1", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_2", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_3", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_4", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_5", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_6", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_7", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_8", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_9", ("harad_levy", 7), ("harad_noble", 3));
    }

    // --- Shaghâna Culture Fallback ---

    private static void InitializeShaghânaCulture()
    {
        CultureMap["shaghana"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy", 7),
            new VolunteerChance("harad_noble", 3)
        };
    }

    // --- Âbanissa Clan Mappings ---

    private static void InitializeAbanissaClans()
    {
        AddClan("clan_abanissa_1", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_2", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_3", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_4", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_5", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_6", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_7", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_8", ("harad_levy", 7), ("harad_noble", 3));
    }

    // --- Âbanissa Culture Fallback ---

    private static void InitializeAbanissaCulture()
    {
        CultureMap["abanissa"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy", 7),
            new VolunteerChance("harad_noble", 3)
        };
    }

    // --- Harad Culture Fallback (Culture.aserai — the Harad kingdom) ---
    // The Harad kingdom uses engine culture id "aserai" (cheatsheet). It had no CultureMap pool — only the
    // Shaghâna/Âbanissa sub-factions did — so HasCulturePool("aserai") was false and Harad conquests never
    // converted (Codex review, 2026-06-02). Reuse the same harad_levy/harad_noble pool as those sub-factions.
    private static void InitializeHaradCulture()
    {
        CultureMap["aserai"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy",  7),
            new VolunteerChance("harad_noble", 3),
        };
    }

    // --- Harad Clans (Culture.aserai) ---
    // The war-elephant rider (level-51 elite) is recruitable ONLY by clan_aserai_1 (Ayerikkä). A clan pool
    // SHADOWS the aserai culture fallback (troops.md priority: clan > culture), so it copies the levy/noble pool
    // and ADDS harad_elephant_rider at a low weight (1 of 11 ~= 9% of Ayerikkä's volunteer rolls). No other
    // clan or the culture fallback contains the rider, so it cannot be recruited anywhere else. Weight is the
    // rarity tuning knob.
    private static void InitializeHaradClans()
    {
        AddClan("clan_aserai_1",
            ("harad_levy",            7),
            ("harad_noble",           3),
            ("harad_elephant_rider",  1),
            ("harad_mumakil_rider",   1));
    }
}
