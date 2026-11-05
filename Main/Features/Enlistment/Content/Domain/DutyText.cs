using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>
/// Duty text lives in <c>enlistment_duties.json</c> as <c>{=taom_enlist_duty_&lt;id&gt;_&lt;suffix&gt;}English</c>
/// strings. The key is fixed by the row id and the suffix (title, body, opta, optb, success,
/// failure), so this class is the one place the key formula lives; <c>taom_enlistment_strings.xml</c>
/// stays the translation registry, kept in step by <c>tools/harvest_duty_strings.py</c>.
///
/// The English has to come from the row itself. <c>MBTextManager.GetLocalizedText</c> returns the inline
/// text after <c>{=key}</c> for an English game and never consults the registered row, so a key
/// composed with a generic inline text would show that generic text to every English player.
/// </summary>
public static class DutyText
{
    public const string TitleSuffix = "title";
    public const string BodySuffix = "body";
    public const string OptionASuffix = "opta";
    public const string OptionBSuffix = "optb";
    public const string SuccessSuffix = "success";
    public const string FailureSuffix = "failure";

    /// <summary>The two result sentences every interactive duty and incident shares; each has its own registered row per id.</summary>
    public const string SharedSuccess = "It went well.";
    public const string SharedFailure = "It didn't go as planned.";

    public static string KeyFor(string id, string suffix) => "taom_enlist_duty_" + id + "_" + suffix;

    /// <summary>
    /// Null when <paramref name="text"/> is exactly <c>{=taom_enlist_duty_{id}_{suffix}}English</c> with a
    /// non-blank English part; otherwise a short description of what is wrong, for the load warning.
    /// <paramref name="field"/> names the JSON field in the message (<c>optionA.label</c>, <c>success</c>);
    /// it defaults to the suffix.
    /// </summary>
    public static string Problem(string text, string id, string suffix, string field = null)
    {
        field ??= suffix;
        var prefix = "{=" + KeyFor(id, suffix) + "}";
        if (string.IsNullOrEmpty(text))
            return $"{field} text is missing (expected '{prefix}English')";
        if (!text.StartsWith(prefix, System.StringComparison.Ordinal))
            return $"{field} text must start with '{prefix}'";
        if (string.IsNullOrWhiteSpace(text.Substring(prefix.Length)))
            return $"{field} text has no English after '{prefix}'";
        return null;
    }

    /// <summary>Every problem across the given (text, suffix, field) triples, joined for one warning; null when all are well formed.</summary>
    public static string Problems(string id, params (string Text, string Suffix, string Field)[] texts)
    {
        var found = new List<string>();
        foreach (var (text, suffix, field) in texts)
        {
            var problem = Problem(text, id, suffix, field);
            if (problem != null)
                found.Add(problem);
        }
        return found.Count == 0 ? null : string.Join("; ", found);
    }

    /// <summary>The English part of a valid row text, or <paramref name="generic"/> when the row has none (never for a loaded row).</summary>
    public static string English(string text, string id, string suffix, string generic)
        => Problem(text, id, suffix) == null ? text.Substring(KeyFor(id, suffix).Length + 3) : generic;

    /// <summary>
    /// The <c>{=key}English</c> string to hand to a <c>TextObject</c>: the row's own text when it is
    /// well formed, otherwise the composed key with <paramref name="generic"/> English.
    /// </summary>
    public static string Source(string text, string id, string suffix, string generic)
        => Problem(text, id, suffix) == null ? text : "{=" + KeyFor(id, suffix) + "}" + generic;
}
