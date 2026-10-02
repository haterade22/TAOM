using System.Collections.Generic;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>
/// Kysaro's art direction for the faction screen (#704), from his <c>FactionCatalog</c> tables: each
/// faction's painted portrait (with its drawn width and scale) and its named-hero cards. His alignment
/// table is not kept: a faction's side comes from <c>factions.json</c>, as on the faction map.
/// The heroes his module could not find are now TAOM characters: Saruman (<c>lord_I1_0</c>), Bolg
/// (<c>lord_MM6_5</c>), one of the Nine for his generic Nazgûl (<c>lord_1_155</c>), and picker-only
/// templates for the legends TAOM has no hero for (<c>taom_fui_*</c>, never spawned).
/// </summary>
public static class FactionScreenArt
{
    public const string DefaultBackground = "fs_bg_default";
    public const string DefaultEmblem = "fs_emblem_default";
    public const string CustomCharacterPortrait = "fs_portrait_custom";

    public static readonly IReadOnlyDictionary<string, string> PaintedPortraits = new Dictionary<string, string>
    {
        ["dominion_of_isengard"] = "fs_portrait_isengard",
        ["kingdom_of_rohan"] = "fs_portrait_rohan",
        ["dominion_of_mordor"] = "fs_portrait_mordor",
        ["stewardship_of_gondor"] = "fs_portrait_gondor",
        ["kingdom_of_lasgalen"] = "fs_portrait_lasgalen",
        ["high_kingdom_of_lindon"] = "fs_portrait_lindon",
        ["kingdom_of_imladris"] = "fs_portrait_imladris",
        ["overlordship_of_gundabad"] = "fs_portrait_gundabad",
        ["kingdom_of_lothlorien"] = "fs_portrait_lothlorien",
        ["kingdom_of_erebor"] = "fs_portrait_erebor",
        ["overlordship_of_dol_guldur"] = "fs_portrait_dolguldur",
        ["golden_realm_of_rhun"] = "fs_portrait_rhun",
        ["kingdom_of_dale"] = "fs_portrait_dale",
        ["taskralan_of_harwan"] = "fs_portrait_harad",
        ["khudorom_of_khand"] = "fs_portrait_khand",
        ["clans_of_dunland"] = "fs_portrait_dunland",
        ["kingdom_of_moria"] = "fs_portrait_moria",
        ["goblins_of_goblin_town"] = "fs_portrait_goblintown",
        ["goblins_of_blue_craig"] = "fs_portrait_bluecraig",
        ["havens_of_umbar"] = "fs_portrait_umbar",
    };

    public static readonly IReadOnlyDictionary<string, float> PaintedPortraitWidths = new Dictionary<string, float>
    {
        ["overlordship_of_gundabad"] = 450f,
        ["overlordship_of_dol_guldur"] = 499f,
        ["kingdom_of_imladris"] = 457f,
        ["kingdom_of_lothlorien"] = 419f,
        ["dominion_of_mordor"] = 660f,
        ["kingdom_of_lasgalen"] = 581f,
        ["stewardship_of_gondor"] = 441f,
    };

    public static readonly IReadOnlyDictionary<string, float> PaintedPortraitScales = new Dictionary<string, float>
    {
        ["stewardship_of_gondor"] = 1.13f,
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<SpecialCharacter>> SpecialCharacters =
        new Dictionary<string, IReadOnlyList<SpecialCharacter>>
        {
            ["kingdom_of_rohan"] = new[]
            {
                new SpecialCharacter("Théoden", "lord_4_1", "fs_portrait_theoden", "fs_reveal_theoden", 609f),
                new SpecialCharacter("Éowyn", "lord_4_3_2", "fs_portrait_eowyn", "fs_reveal_eowyn", 584f, 0.92f),
                new SpecialCharacter("Éomer", "lord_4_3_1", "fs_portrait_eomer", "fs_reveal_eomer", 428f),
                new SpecialCharacter("Théodred", "lord_4_7", "fs_portrait_theodred", "fs_reveal_theodred", 393f),
            },
            ["stewardship_of_gondor"] = new[]
            {
                new SpecialCharacter("Gandalf", "taom_fui_gandalf", "fs_portrait_gandalf", "fs_reveal_gandalf", 551f),
                new SpecialCharacter("Aragorn", "named_companion_aragorn", "fs_portrait_aragorn", "fs_reveal_aragorn", 589f),
                new SpecialCharacter("Boromir", "lord_1_75", "fs_portrait_boromir", "fs_reveal_boromir", 552f),
                new SpecialCharacter("Faramir", "lord_1_34", "fs_portrait_faramir", "fs_reveal_faramir", 484f),
                new SpecialCharacter("Denethor II", "lord_1_7", "fs_portrait_denethor", "fs_reveal_denethor", 520f),
                new SpecialCharacter("Isildur", "taom_fui_isildur", "fs_portrait_isildur", "fs_reveal_isildur", 401f),
            },
            ["kingdom_of_lasgalen"] = new[]
            {
                new SpecialCharacter("Thranduil", "lord_M1_1", "fs_portrait_thranduil", "fs_reveal_thranduil", 456f),
                new SpecialCharacter("Legolas", "lord_M1_11", "fs_portrait_legolas", "fs_reveal_legolas", 492f),
                new SpecialCharacter("Tauriel", "taom_fui_tauriel", "fs_portrait_tauriel", "fs_reveal_tauriel", 417f),
            },
            ["kingdom_of_imladris"] = new[]
            {
                new SpecialCharacter("Elrond", "lord_R1_1", "fs_portrait_elrond", "fs_reveal_elrond", 582f),
                new SpecialCharacter("Arwen", "lord_R1_5", "fs_portrait_arwen", "fs_reveal_arwen", 393f),
                new SpecialCharacter("Glorfindel", "lord_R2_1", "fs_portrait_glorfindel", "fs_reveal_glorfindel", 696f),
            },
            ["kingdom_of_lothlorien"] = new[]
            {
                new SpecialCharacter("Galadriel", "lord_L1_1", "fs_portrait_galadriel", "fs_reveal_galadriel", 423f),
                new SpecialCharacter("Celeborn", "lord_L1_2", "fs_portrait_celeborn", "fs_reveal_celeborn", 504f),
                new SpecialCharacter("Haldir", "lord_M3_3", "fs_portrait_haldir", "fs_reveal_haldir", 470f),
            },
            ["kingdom_of_erebor"] = new[]
            {
                new SpecialCharacter("Gimli", "named_companion_gimli", "fs_portrait_gimli", "fs_reveal_gimli", 630f, 0.95f),
                new SpecialCharacter("Dáin Ironfoot", "lord_E1_1", "fs_portrait_dain", "fs_reveal_dain", 678f),
                new SpecialCharacter("Thorin Oakenshield", "taom_fui_thorin", "fs_portrait_thorin", "fs_reveal_thorin", 472f),
            },
            ["dominion_of_mordor"] = new[]
            {
                new SpecialCharacter("Sauron", "lord_1_17", "fs_portrait_sauron", "fs_reveal_sauron", 567f),
                new SpecialCharacter("Witch-king of Angmar", "lord_1_15", "fs_portrait_witchking", "fs_reveal_witchking", 672f),
                new SpecialCharacter("Mouth of Sauron", "lord_1_14", "fs_portrait_mouth", "fs_reveal_mouth", 708f),
                new SpecialCharacter("Nazgûl", "lord_1_155", "fs_portrait_nazgul", "fs_reveal_nazgul", 470f, 1.2f),
            },
            ["dominion_of_isengard"] = new[]
            {
                new SpecialCharacter("Saruman", "lord_I1_0", "fs_portrait_saruman", "fs_reveal_saruman", 370f),
                new SpecialCharacter("Lurtz", "lord_I4_1", "fs_portrait_lurtz", "fs_reveal_lurtz", 588f),
            },
            ["kingdom_of_dale"] = new[]
            {
                new SpecialCharacter("Bard the Bowman", "taom_fui_bard", "fs_portrait_bard", "fs_reveal_bard", 477f),
                new SpecialCharacter("Brand", "taom_fui_brand", "fs_portrait_brand", "fs_reveal_brand", 415f),
            },
            ["overlordship_of_dol_guldur"] = new[]
            {
                new SpecialCharacter("Khamûl", "lord_1_48", "fs_portrait_khamul", "fs_reveal_khamul", 529f),
            },
            ["overlordship_of_gundabad"] = new[]
            {
                new SpecialCharacter("Azog the Defiler", "taom_fui_azog", "fs_portrait_azog", "fs_reveal_azog", 583f),
                new SpecialCharacter("Bolg", "lord_MM6_5", "fs_portrait_bolg", "fs_reveal_bolg", 605f),
            },
            ["high_kingdom_of_lindon"] = new[]
            {
                new SpecialCharacter("Gil-galad", "taom_fui_gilgalad", "fs_portrait_gilgalad", "fs_reveal_gilgalad", 447f, 1.08f),
            },
        };
}
