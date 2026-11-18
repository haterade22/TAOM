using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// A dwarf has its own skeleton, so armour rigged for the human skeleton deforms on it. Only the
/// Armory's <c>sk_dwarf_*</c> pieces fit (lessons/data-content-cultures.md, "Custom-skeleton-race
/// NPCs need race-rigged armor"). Weapons and shields are static meshes, so the rule reads the five
/// armour slots only.
///
/// <para>
/// The child-education screen previews a kit chosen by the child's culture
/// (<c>EducationCampaignBehavior</c>, v1.5.7). Erebor's 98 kits were vanilla's Sturgian clothes
/// (reported by Silas He, #1439), and Eredluin's, cloned from them, were the same. A culture whose
/// troops are dwarves counts as a dwarf culture, so a new one is covered. Reads the repo's own
/// ModuleData, so it needs no game install.
/// </para>
/// </summary>
[TestClass]
public class DwarfEducationKitTests
{
    private const string DwarfPrefix = "Item.sk_dwarf_";
    private const string EducationPrefix = "child_education_equipments_";

    private static readonly HashSet<string> ArmourSlots =
        new(StringComparer.Ordinal) { "Head", "Body", "Leg", "Gloves", "Cape" };

    private static readonly string LanguagesSegment =
        $"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}";

    private static IEnumerable<XDocument> Documents() =>
        Directory.GetFiles(CultureDataFixture.ModuleDataPath(), "*.xml", SearchOption.AllDirectories)
            .Where(f => f.IndexOf(LanguagesSegment, StringComparison.OrdinalIgnoreCase) < 0)
            .Select(XDocument.Load);

    [TestMethod]
    public void EveryEducationKitOfADwarfCulture_HoldsOnlyDwarfArmour()
    {
        var documents = Documents().ToList();

        var dwarfCultures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in documents.SelectMany(d => d.Descendants("NPCCharacter")))
            if ((string?)character.Attribute("race") == "dwarf"
                && (string?)character.Attribute("culture") is { } c && c.StartsWith("Culture.", StringComparison.Ordinal))
                dwarfCultures.Add(c.Substring("Culture.".Length));
        CollectionAssert.IsSubsetOf(new[] { "erebor", "eredluin" }, dwarfCultures.ToList(),
            "the dwarf cultures are no longer derived from the troops");

        var faults = new List<string>();
        var kits = 0;
        foreach (var roster in documents.SelectMany(d => d.Descendants("EquipmentRoster")))
        {
            var id = (string?)roster.Attribute("id") ?? "";
            if (!id.StartsWith(EducationPrefix, StringComparison.Ordinal)) continue;
            if (!dwarfCultures.Contains(id.Substring(id.LastIndexOf('_') + 1))) continue;
            kits++;
            var human = roster.Descendants("Equipment")
                .Where(e => ArmourSlots.Contains((string?)e.Attribute("slot") ?? ""))
                .Select(e => (string?)e.Attribute("id") ?? "")
                .Where(item => !item.StartsWith(DwarfPrefix, StringComparison.Ordinal))
                .Distinct()
                .ToList();
            if (human.Count > 0)
                faults.Add($"  {id}: {string.Join(", ", human)}");
        }

        Assert.IsTrue(kits >= 2 * 98, $"found only {kits} dwarf education kits; the file shapes changed");
        Assert.AreEqual(0, faults.Count,
            "A dwarf child wears only sk_dwarf_* armour:\n" + string.Join("\n", faults.Take(20)) +
            (faults.Count > 20 ? $"\n  ... and {faults.Count - 20} more" : ""));
    }
}
