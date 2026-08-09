using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Pins the registration of the character-creation narrative strings, whose keys
/// <see cref="TAOM.Features.CharacterCreation.NarrativeMenuBuilder"/> composes at runtime:
/// <c>$"{{=taom_cc_{definition.StringId}_text}}{definition.Text}"</c>.
///
/// A composed key is invisible to a <c>{=key}</c> grep, so an unregistered one leaves no trace
/// anywhere a human would look. Worse, it does not misbehave in English:
/// <c>MBTextManager.GetLocalizedText</c> short-circuits on English and returns the inline default
/// (Localization/MBTextManager.cs:264-268), so the JSON text renders correctly and the key is never
/// consulted. The row only matters for the other eleven languages — which means the failure mode is
/// "invisible to the author, invisible to the test suite, visible only to a player in a language
/// the author does not read."
///
/// That is exactly how <c>goblin</c> and <c>mistymountainorcs</c> shipped with all 96 of their
/// narrative rows missing while the other sixteen cultures were complete (2026-08-09). It is the
/// same defect class the enlistment duty-result toasts hit two days earlier, which is what prompted
/// the sweep that found this one.
/// </summary>
[TestClass]
public class NarrativeStringRegistrationTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    /// <summary>
    /// The menus <c>NarrativeMenuBuilder</c> renders. Childhood is deliberately absent: it is
    /// culture-agnostic and its options are authored as literal keys, not composed ones.
    /// </summary>
    private static readonly string[] NarrativeMenus =
        { "parents_menu.json", "youth_menu.json", "education_menu.json", "adulthood_menu.json" };

    private static readonly Regex KeyPrefix = new Regex(@"^\{=[^}]*\}", RegexOptions.Compiled);

    private static Dictionary<string, string> LoadRegisteredDefaults()
    {
        var path = Path.Combine(ModuleDataPath, "taom_cc_strings.xml");
        Assert.IsTrue(File.Exists(path), $"taom_cc_strings.xml not found at {path}");

        var registered = new Dictionary<string, string>();
        foreach (var node in XDocument.Load(path).Root!.Elements("string"))
        {
            var id = (string?)node.Attribute("id");
            if (string.IsNullOrEmpty(id))
                continue;

            // The registered text carries its own {=key} prefix by TAOM convention; the default
            // string is what follows it.
            registered[id!] = KeyPrefix.Replace((string?)node.Attribute("text") ?? string.Empty, string.Empty);
        }
        return registered;
    }

    private static IEnumerable<(string Menu, string StringId, string Text, string Description)> LoadNarrativeOptions()
    {
        foreach (var menu in NarrativeMenus)
        {
            var path = Path.Combine(ModuleDataPath, "charactercreation", menu);
            Assert.IsTrue(File.Exists(path), $"{menu} not found at {path}");

            foreach (var option in JArray.Parse(File.ReadAllText(path)))
            {
                var stringId = option.Value<string>("string_id");
                if (string.IsNullOrEmpty(stringId))
                    continue;

                yield return (menu, stringId!,
                    option.Value<string>("text") ?? string.Empty,
                    option.Value<string>("description") ?? string.Empty);
            }
        }
    }

    [TestMethod]
    public void EveryNarrativeOption_HasRegisteredTextAndDescriptionKeys()
    {
        var registered = LoadRegisteredDefaults();

        var missing = LoadNarrativeOptions()
            .SelectMany(o => new[] { $"taom_cc_{o.StringId}_text", $"taom_cc_{o.StringId}_desc" }
                .Where(k => !registered.ContainsKey(k))
                .Select(k => $"{o.Menu}: {k}"))
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "Narrative option keys are composed at runtime, so an unregistered one renders fine in "
            + "English and is untranslatable in every other language. Add these to "
            + "taom_cc_strings.xml, then run tools/translate_with_claude.py:\n  "
            + string.Join("\n  ", missing.Take(20))
            + (missing.Count > 20 ? $"\n  ... and {missing.Count - 20} more" : string.Empty));
    }

    [TestMethod]
    public void EveryRegisteredNarrativeKey_MatchesItsJsonSource()
    {
        var registered = LoadRegisteredDefaults();

        // A registered row whose default has drifted from the JSON is worse than a missing one:
        // English renders the JSON (short-circuit) while the translations were made from the stale
        // row, so the two halves of the same option say different things in different languages.
        var drifted = new List<string>();
        foreach (var option in LoadNarrativeOptions())
        {
            foreach (var (suffix, source) in new[] { ("_text", option.Text), ("_desc", option.Description) })
            {
                var key = $"taom_cc_{option.StringId}{suffix}";
                if (registered.TryGetValue(key, out var value) && value.Trim() != source.Trim())
                    drifted.Add($"{key}\n     xml : {value}\n     json: {source}");
            }
        }

        Assert.AreEqual(0, drifted.Count,
            "Registered default has drifted from the JSON it mirrors — the translations were made "
            + "from the stale text:\n  " + string.Join("\n  ", drifted.Take(10)));
    }
}
