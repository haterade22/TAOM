using System;
using System.Collections.Generic;

namespace TAOM.Features.EncounterLines;

/// <summary>
/// The notable introductions Encounter Lines rewrites through its text seam (docs/features/encounter-lines.md "Notable
/// introductions"): vanilla's three sentences in <c>LordConversationsCampaignBehavior</c> that a notable speaks on the first
/// talk (artisan, headman, rural notable), each with the TAOM game text whose rows hold the lines. A row's variation is
/// <c>&lt;family&gt;_&lt;n&gt;</c> with a family in <see cref="Families"/> and n from 1 to <see cref="Variants"/>.
/// </summary>
internal static class NotableIntroLines
{
    internal const string Artisan = "str_taom_notable_artisan";
    internal const string Headman = "str_taom_notable_headman";
    internal const string Rural = "str_taom_notable_rural";

    /// <summary>How many lines each family has for each sentence; the notable's id picks one.</summary>
    internal const int Variants = 3;

    /// <summary>The race families whose notables introduce themselves with their own rows; Men keep vanilla's lines.</summary>
    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        EncounterGroupMap.Dwarf, EncounterGroupMap.Elf, EncounterGroupMap.Orc,
    };

    /// <summary>Vanilla's sentence id to the TAOM game text.</summary>
    internal static readonly IReadOnlyDictionary<string, string> TextOfSentence = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["artisan_introduction"] = Artisan,
        ["headman_introduction"] = Headman,
        ["rural_notable_introduction"] = Rural,
    };
}
