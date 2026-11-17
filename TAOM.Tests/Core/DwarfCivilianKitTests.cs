using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// A dwarf character may not take its civilian clothes from a vanilla template (#1431).
///
/// <para>
/// <c>&lt;EquipmentSet id="battania_troop_civilian_template_t1" equipmentType="Civilian" /&gt;</c> pulls a
/// human culture's civilian roster into a dwarf's <c>Equipments</c>. The engine then reports a civilian
/// set for that troop, so Field Commission's battle-kit fallback never runs and a promoted dwarf wears
/// the Highland Tartan (<c>battania_light_armor_b</c>). A dwarf wears a dwarf tunic from the Armory: an
/// inline <c>EquipmentRoster civilian="true"</c> in its own file, as the Erebor militia already does
/// (<see cref="DwarfDressWearerTests"/> owns who may wear a dress).
/// </para>
///
/// <para>
/// The rule fails closed: an <c>EquipmentSet</c> that names a roster by <c>id</c> inside a dwarf
/// character passes only when a TAOM file defines that roster for a dwarf culture (or for none), because
/// every vanilla template is a human's. A character is a dwarf when it says <c>race="dwarf"</c> or its
/// culture is Erebor or Ered Luin.
/// Reads the repo's own ModuleData, so it needs no game install.
/// </para>
/// </summary>
[TestClass]
public class DwarfCivilianKitTests
{
    private static readonly string LanguagesSegment =
        $"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}";

    private static readonly string[] DwarfCultures = { "Culture.erebor", "Culture.eredluin" };

    private static IEnumerable<(string File, XDocument Doc)> ModuleDataDocuments()
    {
        foreach (var file in Directory.GetFiles(CultureDataFixture.ModuleDataPath(), "*.xml",
                                                SearchOption.AllDirectories))
        {
            if (file.IndexOf(LanguagesSegment, StringComparison.OrdinalIgnoreCase) >= 0) continue;
            yield return (Path.GetFileName(file), XDocument.Load(file));
        }
    }

    internal static bool IsDwarf(XElement character) =>
        string.Equals((string?)character.Attribute("race"), "dwarf", StringComparison.OrdinalIgnoreCase)
        || DwarfCultures.Contains((string?)character.Attribute("culture") ?? string.Empty,
                                  StringComparer.OrdinalIgnoreCase);

    /// <summary>Every roster id this repo defines, mapped to the culture it declares (null when none).</summary>
    internal static Dictionary<string, string?> DefinedRosters(IEnumerable<XDocument> docs)
    {
        var defined = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var doc in docs)
        foreach (var roster in doc.Descendants("EquipmentRoster"))
        {
            var id = (string?)roster.Attribute("id");
            if (!string.IsNullOrEmpty(id))
                defined[id!] = (string?)roster.Attribute("culture");
        }
        return defined;
    }

    /// <summary>
    /// Every roster a dwarf character names by id that is not a dwarf's own, as "character -> set id":
    /// an id no TAOM file defines (so it is a vanilla template, and every one of those is a human's) or
    /// an id TAOM defines under a non-dwarf culture. TAOM's own dwarf templates, such as the Erebor
    /// wanderer kit, pass.
    /// </summary>
    internal static IEnumerable<string> BorrowedRosterReferences(
        XDocument doc, IReadOnlyDictionary<string, string?> defined)
    {
        foreach (var character in doc.Descendants("NPCCharacter").Where(IsDwarf))
        {
            var characterId = (string?)character.Attribute("id") ?? "(no id)";
            foreach (var set in character.Descendants("EquipmentSet"))
            {
                var rosterId = (string?)set.Attribute("id");
                if (string.IsNullOrEmpty(rosterId)) continue;
                var owned = defined.TryGetValue(rosterId!, out var culture)
                            && (string.IsNullOrEmpty(culture)
                                || DwarfCultures.Contains(culture!, StringComparer.OrdinalIgnoreCase));
                if (!owned)
                    yield return $"{characterId} -> {rosterId}";
            }
        }
    }

    [TestMethod]
    public void NoDwarfCharacter_BorrowsAHumanRoster()
    {
        var documents = ModuleDataDocuments().ToList();
        var defined = DefinedRosters(documents.Select(d => d.Doc));
        var offenders = new List<string>();
        var dwarvesSeen = 0;
        foreach (var (file, doc) in documents)
        {
            dwarvesSeen += doc.Descendants("NPCCharacter").Count(IsDwarf);
            offenders.AddRange(BorrowedRosterReferences(doc, defined).Select(r => $"{file}: {r}"));
        }

        Assert.IsTrue(dwarvesSeen > 0, "The walk found no dwarf character, so the rule checked nothing.");
        Assert.AreEqual(0, offenders.Count,
            $"{offenders.Count} dwarf character(s) borrow a roster that is not a dwarf's. A dwarf wears an "
            + "inline dwarf civilian roster (Item.sk_dwarf_tunic_normal_*), never a human culture's "
            + "template (#1431): " + string.Join("; ", offenders.Take(20)));
    }

    private const string OwnDwarfKit = "<EquipmentRoster id=\"dwarf_kit\" culture=\"Culture.erebor\"><EquipmentSet /></EquipmentRoster>";
    private const string HumanKit = "<EquipmentRoster id=\"human_kit\" culture=\"Culture.gondor\"><EquipmentSet /></EquipmentRoster>";

    [DataTestMethod]
    [DataRow("<NPCCharacter id=\"d\" race=\"dwarf\"><Equipments><EquipmentSet id=\"battania_troop_civilian_template_t1\" equipmentType=\"Civilian\" /></Equipments></NPCCharacter>", 1)]
    [DataRow("<NPCCharacter id=\"d\" culture=\"Culture.eredluin\"><Equipments><EquipmentSet id=\"battania_troop_civilian_template_t2\" equipmentType=\"Civilian\" /></Equipments></NPCCharacter>", 1)]
    [DataRow("<NPCCharacter id=\"d\" culture=\"Culture.erebor\"><Equipments><EquipmentSet id=\"human_kit\" /></Equipments></NPCCharacter>", 1)]
    [DataRow("<NPCCharacter id=\"d\" culture=\"Culture.erebor\"><Equipments><EquipmentSet id=\"dwarf_kit\" /></Equipments></NPCCharacter>", 0)]
    [DataRow("<NPCCharacter id=\"d\" race=\"dwarf\"><Equipments><EquipmentRoster civilian=\"true\"><equipment slot=\"Body\" id=\"Item.sk_dwarf_tunic_normal_a\" /></EquipmentRoster></Equipments></NPCCharacter>", 0)]
    [DataRow("<NPCCharacter id=\"h\"><Equipments><EquipmentSet id=\"battania_troop_civilian_template_t1\" equipmentType=\"Civilian\" /></Equipments></NPCCharacter>", 0)]
    public void BorrowedRosterReferences_ClassifiesEachShape(string xml, int expected)
    {
        var doc = XDocument.Parse($"<NPCCharacters>{xml}{OwnDwarfKit}{HumanKit}</NPCCharacters>");
        var defined = DefinedRosters(new[] { doc });
        Assert.AreEqual(expected, BorrowedRosterReferences(doc, defined).Count());
    }
}
