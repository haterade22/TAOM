using System.Collections.Generic;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>
/// Kysaro's art direction for the faction screen (#704), from his <c>FactionCatalog</c> tables: each
/// faction's named-hero cards. Picking a card shows its character in 3D (2026-11-24). His alignment
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

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<SpecialCharacter>> SpecialCharacters =
        new Dictionary<string, IReadOnlyList<SpecialCharacter>>
        {
            ["kingdom_of_rohan"] = new[]
            {
                new SpecialCharacter("Théoden", "lord_4_1", "fs_portrait_theoden"),
                new SpecialCharacter("Éowyn", "lord_4_3_2", "fs_portrait_eowyn"),
                new SpecialCharacter("Éomer", "lord_4_3_1", "fs_portrait_eomer"),
                new SpecialCharacter("Théodred", "lord_4_7", "fs_portrait_theodred"),
            },
            ["stewardship_of_gondor"] = new[]
            {
                new SpecialCharacter("Gandalf", "taom_fui_gandalf", "fs_portrait_gandalf"),
                new SpecialCharacter("Aragorn", "named_companion_aragorn", "fs_portrait_aragorn"),
                new SpecialCharacter("Boromir", "lord_1_75", "fs_portrait_boromir"),
                new SpecialCharacter("Faramir", "lord_1_34", "fs_portrait_faramir"),
                new SpecialCharacter("Denethor II", "lord_1_7", "fs_portrait_denethor"),
                new SpecialCharacter("Isildur", "taom_fui_isildur", "fs_portrait_isildur"),
            },
            ["kingdom_of_lasgalen"] = new[]
            {
                new SpecialCharacter("Thranduil", "lord_M1_1", "fs_portrait_thranduil"),
                new SpecialCharacter("Legolas", "lord_M1_11", "fs_portrait_legolas"),
                new SpecialCharacter("Tauriel", "taom_fui_tauriel", "fs_portrait_tauriel"),
            },
            ["kingdom_of_imladris"] = new[]
            {
                new SpecialCharacter("Elrond", "lord_R1_1", "fs_portrait_elrond"),
                new SpecialCharacter("Arwen", "lord_R1_5", "fs_portrait_arwen"),
                new SpecialCharacter("Glorfindel", "lord_R2_1", "fs_portrait_glorfindel"),
            },
            ["kingdom_of_lothlorien"] = new[]
            {
                new SpecialCharacter("Galadriel", "lord_L1_1", "fs_portrait_galadriel"),
                new SpecialCharacter("Celeborn", "lord_L1_2", "fs_portrait_celeborn"),
                new SpecialCharacter("Haldir", "lord_M3_3", "fs_portrait_haldir"),
            },
            ["kingdom_of_erebor"] = new[]
            {
                new SpecialCharacter("Gimli", "named_companion_gimli", "fs_portrait_gimli"),
                new SpecialCharacter("Dáin Ironfoot", "lord_E1_1", "fs_portrait_dain"),
                new SpecialCharacter("Thorin Oakenshield", "taom_fui_thorin", "fs_portrait_thorin"),
            },
            ["dominion_of_mordor"] = new[]
            {
                new SpecialCharacter("Sauron", "lord_1_17", "fs_portrait_sauron"),
                new SpecialCharacter("Witch-king of Angmar", "lord_1_15", "fs_portrait_witchking"),
                new SpecialCharacter("Mouth of Sauron", "lord_1_14", "fs_portrait_mouth"),
                new SpecialCharacter("Nazgûl", "lord_1_155", "fs_portrait_nazgul"),
            },
            ["dominion_of_isengard"] = new[]
            {
                new SpecialCharacter("Saruman", "lord_I1_0", "fs_portrait_saruman"),
                new SpecialCharacter("Lurtz", "lord_I4_1", "fs_portrait_lurtz"),
            },
            ["kingdom_of_dale"] = new[]
            {
                new SpecialCharacter("Bard the Bowman", "taom_fui_bard", "fs_portrait_bard"),
                new SpecialCharacter("Brand", "taom_fui_brand", "fs_portrait_brand"),
            },
            ["overlordship_of_dol_guldur"] = new[]
            {
                new SpecialCharacter("Khamûl", "lord_1_48", "fs_portrait_khamul"),
            },
            ["overlordship_of_gundabad"] = new[]
            {
                new SpecialCharacter("Azog the Defiler", "taom_fui_azog", "fs_portrait_azog"),
                new SpecialCharacter("Bolg", "lord_MM6_5", "fs_portrait_bolg"),
            },
            ["high_kingdom_of_lindon"] = new[]
            {
                new SpecialCharacter("Gil-galad", "taom_fui_gilgalad", "fs_portrait_gilgalad"),
            },
        };
}
