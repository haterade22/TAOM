using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every wanderer in taom_wanderers.xml needs its seven backstory GameText rows in
/// taom_wanderer_strings.xml, keyed "&lt;kind&gt;.&lt;wanderer id&gt;". The companion dialogue looks each one
/// up by that variation, and a missing row renders as "ERROR: Text with id backstory_c doesn't exist!
/// Variation: spc_wanderer_goblin_8" in the conversation box. Nothing warns at load.
///
/// Why this exists: the goblin, mistymountainorcs, bluecraig, lindon and arthedain wanderers were
/// cloned from donor cultures without their string rows, 53 characters in all; #257 had fixed the same
/// gap for two cultures by hand, with no test. Data-only check.
/// </summary>
[TestClass]
public class WandererBackstoryCoverageTests
{
    // Read with GameTexts.FindText, so a missing row renders as the ERROR line.
    private static readonly string[] RequiredKinds =
    {
        "prebackstory", "backstory_a", "backstory_b", "backstory_c", "backstory_d",
        "response_1", "response_2",
    };

    // generic_backstory is optional: LordConversationsCampaignBehavior reads it with TryGetText and
    // falls back to a vanilla line (v1.5.4 conversation_wanderer_preintroduction_on_condition).
    private static readonly string[] Kinds = RequiredKinds.Concat(new[] { "generic_backstory" }).ToArray();

    private static string ModuleData => CultureDataFixture.ModuleDataPath();

    private static List<string> WandererIds() =>
        XDocument.Load(Path.Combine(ModuleData, "taom_wanderers.xml"))
            .Descendants("NPCCharacter")
            .Where(c => (string)c.Attribute("occupation") == "Wanderer")
            .Select(c => (string)c.Attribute("id"))
            .ToList();

    private static List<XElement> StringRows() =>
        XDocument.Load(Path.Combine(ModuleData, "taom_wanderer_strings.xml"))
            .Descendants("string")
            .ToList();

    private static IEnumerable<string> RowIds(IEnumerable<string> wanderers, IEnumerable<string> kinds) =>
        wanderers.SelectMany(id => kinds.Select(kind => $"{kind}.{id}"));

    [TestMethod]
    public void WandererStrings_EveryWanderer_HasEveryRequiredBackstoryRow()
    {
        var wanderers = WandererIds();
        Assert.IsTrue(wanderers.Count > 0, "no wanderers found: the scan checked nothing");

        var present = new HashSet<string>(StringRows().Select(s => (string)s.Attribute("id")), StringComparer.Ordinal);
        var missing = RowIds(wanderers, RequiredKinds)
            .Where(rowId => !present.Contains(rowId))
            .OrderBy(rowId => rowId, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, missing.Count,
            $"{missing.Count} backstory rows missing from taom_wanderer_strings.xml:\n" +
            string.Join("\n", missing.Take(80)));
    }

    [TestMethod]
    public void WandererStrings_EveryRow_NamesAKnownWandererAndKind()
    {
        var allowed = new HashSet<string>(RowIds(WandererIds(), Kinds), StringComparer.Ordinal);
        var orphans = StringRows()
            .Select(s => (string)s.Attribute("id"))
            .Where(id => id == null || !allowed.Contains(id))
            .ToList();

        Assert.AreEqual(0, orphans.Count, "rows naming no wanderer or kind:\n" + string.Join("\n", orphans));
    }

    [TestMethod]
    public void WandererStrings_EveryRow_UsesItsOwnLocalizationKeyAndHasText()
    {
        var wrong = StringRows()
            .Where(s =>
            {
                var key = $"{{=aom_{(string)s.Attribute("id")}_text}}";
                var text = (string)s.Attribute("text") ?? string.Empty;
                return !text.StartsWith(key, StringComparison.Ordinal) || text.Length == key.Length;
            })
            .Select(s => (string)s.Attribute("id"))
            .ToList();

        Assert.AreEqual(0, wrong.Count,
            "rows whose key is not {=aom_<id>_text}, or with no text after it:\n" + string.Join("\n", wrong));
    }

    [TestMethod]
    public void WandererStrings_NoRowId_IsDeclaredTwice()
    {
        // The engine keeps the first copy, so an edit to a second one never shows.
        var duplicates = StringRows()
            .GroupBy(s => (string)s.Attribute("id"), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.AreEqual(0, duplicates.Count, "row ids declared twice:\n" + string.Join("\n", duplicates));
    }
}
