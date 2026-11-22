using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// A character of a non-human race may not take its civilian clothes from a vanilla template (#1433).
///
/// <para>
/// Every vanilla <c>*_troop_civilian_template_tN</c> roster is a human culture's. A goblin, orc, uruk or
/// elf that names one gets that civilian set from the engine loader, so Field Commission copies a human
/// kit onto the promoted hero. Such a character writes an inline <c>EquipmentRoster civilian="true"</c>
/// from its own race's items. <see cref="DwarfCivilianKitTests"/> holds the same rule for dwarves; this
/// one covers every other <c>race</c> attribute except <c>human</c>. Human troops are a design call
/// that #1433 keeps open, so the rule does not touch them.
/// </para>
///
/// <para>
/// A race passes only when its roster id is defined by a TAOM file. A character is exempt when its race
/// is <c>human</c> or absent. Reads the repo's own ModuleData, so it needs no game install.
/// </para>
/// </summary>
[TestClass]
public class NonHumanCivilianKitTests
{
    private static readonly string LanguagesSegment =
        $"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}";

    internal static bool IsNonHuman(XElement character)
    {
        var race = (string?)character.Attribute("race");
        return !string.IsNullOrEmpty(race) && !string.Equals(race, "human", StringComparison.OrdinalIgnoreCase);
    }

    internal static IEnumerable<string> BorrowedRosterReferences(
        XDocument doc, IReadOnlyDictionary<string, string?> defined)
    {
        foreach (var character in doc.Descendants("NPCCharacter").Where(IsNonHuman))
        {
            var characterId = (string?)character.Attribute("id") ?? "(no id)";
            foreach (var set in character.Descendants("EquipmentSet"))
            {
                var rosterId = (string?)set.Attribute("id");
                if (string.IsNullOrEmpty(rosterId) || defined.ContainsKey(rosterId!)) continue;
                yield return $"{characterId} -> {rosterId}";
            }
        }
    }

    [TestMethod]
    public void NoNonHumanCharacter_BorrowsAVanillaHumanRoster()
    {
        var documents = Directory.GetFiles(CultureDataFixture.ModuleDataPath(), "*.xml", SearchOption.AllDirectories)
            .Where(f => f.IndexOf(LanguagesSegment, StringComparison.OrdinalIgnoreCase) < 0)
            .Select(f => (File: Path.GetFileName(f), Doc: XDocument.Load(f)))
            .ToList();
        var defined = DwarfCivilianKitTests.DefinedRosters(documents.Select(d => d.Doc));
        var offenders = new List<string>();
        var seen = 0;
        foreach (var (file, doc) in documents)
        {
            seen += doc.Descendants("NPCCharacter").Count(IsNonHuman);
            offenders.AddRange(BorrowedRosterReferences(doc, defined).Select(r => $"{file}: {r}"));
        }

        Assert.IsTrue(seen > 0, "The walk found no non-human character, so the rule checked nothing.");
        Assert.AreEqual(0, offenders.Count,
            $"{offenders.Count} non-human character(s) borrow a vanilla roster. Write an inline "
            + "EquipmentRoster civilian=\"true\" from the race's own items (#1433): "
            + string.Join("; ", offenders.Take(20)));
    }

    [DataTestMethod]
    [DataRow("<NPCCharacter id=\"o\" race=\"orc\"><Equipments><EquipmentSet id=\"aserai_troop_civilian_template_t3\" equipmentType=\"Civilian\" /></Equipments></NPCCharacter>", 1)]
    [DataRow("<NPCCharacter id=\"e\" race=\"elf\"><Equipments><EquipmentSet id=\"battania_troop_civilian_template_t2\" /></Equipments></NPCCharacter>", 1)]
    [DataRow("<NPCCharacter id=\"o\" race=\"orc\"><Equipments><EquipmentSet id=\"taom_kit\" /></Equipments></NPCCharacter>", 0)]
    [DataRow("<NPCCharacter id=\"o\" race=\"orc\"><Equipments><EquipmentRoster civilian=\"true\"><equipment slot=\"Body\" id=\"Item.x\" /></EquipmentRoster></Equipments></NPCCharacter>", 0)]
    [DataRow("<NPCCharacter id=\"h\" race=\"human\"><Equipments><EquipmentSet id=\"battania_troop_civilian_template_t1\" /></Equipments></NPCCharacter>", 0)]
    [DataRow("<NPCCharacter id=\"h\"><Equipments><EquipmentSet id=\"battania_troop_civilian_template_t1\" /></Equipments></NPCCharacter>", 0)]
    public void BorrowedRosterReferences_ClassifiesEachShape(string xml, int expected)
    {
        var doc = XDocument.Parse($"<NPCCharacters>{xml}<EquipmentRoster id=\"taom_kit\"><EquipmentSet /></EquipmentRoster></NPCCharacters>");
        var defined = DwarfCivilianKitTests.DefinedRosters(new[] { doc });
        Assert.AreEqual(expected, BorrowedRosterReferences(doc, defined).Count());
    }
}
