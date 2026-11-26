using System.Collections.Generic;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Features.IncidentVoices;

/// <summary>
/// Names of the incident texts. A text is the game-text row <c>str_taom_inc.&lt;variation&gt;</c> whose text is
/// <c>{=taom_inc_&lt;variation&gt;}English</c>, so the translation key is <c>taom_inc_&lt;variation&gt;</c>. A variation is
/// <c>&lt;incident id without incident_&gt;_&lt;all|free|shadow&gt;_&lt;title|desc|optN&gt;</c>; a vocabulary word is
/// <c>voc_&lt;scope&gt;_&lt;word&gt;</c>, scope being the voice or a people.
/// </summary>
internal static class IncidentTextKeys
{
    public const string GameTextId = "str_taom_inc";
    public const string KeyPrefix = "taom_inc_";
    public const string All = "all";
    public const string Free = "free";
    public const string Shadow = "shadow";
    public const string Title = "title";
    public const string Description = "desc";
    public const string OptionPrefix = "opt";
    public const string IsengardCulture = "isengard";

    public static string VersionToken(IncidentVersion version) => version == IncidentVersion.Shadow ? Shadow : Free;

    public static string OptionSlot(int oneBased) => OptionPrefix + oneBased;

    public static string Variation(string shortId, string versionToken, string slot) => shortId + "_" + versionToken + "_" + slot;

    public static string Key(string variation) => KeyPrefix + variation;

    public static string VocabVariation(IncidentVersion version, string word) => VocabVariation(VersionToken(version), word);

    /// <summary>A vocabulary row of any scope: <c>free</c>, <c>shadow</c>, a people (<c>dwarves</c>, <c>isengard</c>) or a people on one voice (<c>southrons_shadow</c>).</summary>
    public static string VocabVariation(string scope, string word) => "voc_" + scope + "_" + word.ToLowerInvariant();

    /// <summary>
    /// The vocabulary scopes a player reads, most specific first: the Isengard realm culture, the people on the player's voice,
    /// the people, then the voice. The last one is the side row that every people falls back to.
    /// </summary>
    public static IEnumerable<string> VocabScopes(string? cultureId, string? group, IncidentVersion version)
    {
        var token = VersionToken(version);
        if (cultureId == IsengardCulture)
            yield return IsengardCulture;
        if (!string.IsNullOrEmpty(group))
        {
            yield return group + "_" + token;
            yield return group!;
        }
        yield return token;
    }

    /// <summary>The slots a rule writes, in order: title, description, then each option up to the smaller count.</summary>
    public static IEnumerable<string> Slots(int optionCount)
    {
        yield return Title;
        yield return Description;
        for (var i = 1; i <= optionCount; i++)
            yield return OptionSlot(i);
    }

    /// <summary>The 1-based option number of an <c>optN</c> slot, or 0 for any other slot.</summary>
    public static int OptionNumber(string slot) =>
        slot.StartsWith(OptionPrefix, System.StringComparison.Ordinal) && int.TryParse(slot.Substring(OptionPrefix.Length), out var n) && n >= 1 ? n : 0;
}
