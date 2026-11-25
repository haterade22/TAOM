using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every culture's town guard, prison guard and gang bodyguard (the NPCs a player fights when caught
/// sneaking, in a prison break or a gang fight) carries a level and skills.
///
/// <para>
/// <c>BasicCharacterObject.Deserialize</c> reads a missing <c>level</c> as 1, and an NPC with no
/// <c>skills</c> rows and no <c>skill_template</c> has every skill at 0. Rohan's and South Rhovanion's
/// three were written that way, so their guards fought as level 1 recruits while every other culture's
/// stand at level 16 (guard, bodyguard) and 4 (prison guard).
/// </para>
/// </summary>
[TestClass]
public class TownGuardSkillDataTests
{
    private static readonly string[] Prefixes = { "guard_", "prison_guard_", "gangleader_bodyguard_" };

    [TestMethod]
    public void TownGuards_EveryCulture_HaveALevelAndSkills()
    {
        var folder = Path.Combine(CultureDataFixture.ModuleDataPath(), "characters");
        var seen = 0;
        var bad = new List<string>();
        foreach (var file in Directory.GetFiles(folder, "npcs_*.xml"))
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string)npc.Attribute("id") ?? string.Empty;
                if (!Prefixes.Any(id.StartsWith)) continue;
                seen++;
                var hasSkills = npc.Element("skills")?.Elements("skill").Any() == true
                                || !string.IsNullOrEmpty((string)npc.Attribute("skill_template"));
                if (npc.Attribute("level") == null || !hasSkills)
                    bad.Add($"{Path.GetFileName(file)}: {id}");
            }
        }

        Assert.IsTrue(seen > 60, $"only {seen} guard NPCs read from {folder}");
        Assert.AreEqual(0, bad.Count, "no level or no skills (the engine fields them at level 1, skills 0): " + string.Join(", ", bad));
    }

    [TestMethod]
    public void GangBodyguards_EveryCulture_AreTownsfolk()
    {
        // Vanilla's gang bodyguards are Townsfolk, and so is every TAOM culture's but one, which said Soldier.
        var folder = Path.Combine(CultureDataFixture.ModuleDataPath(), "characters");
        var bodyguards = Directory.GetFiles(folder, "npcs_*.xml")
            .SelectMany(f => XDocument.Load(f).Descendants("NPCCharacter"))
            .Where(n => ((string)n.Attribute("id") ?? string.Empty).StartsWith("gangleader_bodyguard_"))
            .ToList();

        Assert.IsTrue(bodyguards.Count > 20, $"only {bodyguards.Count} gang bodyguards read");
        var other = bodyguards.Where(n => (string)n.Attribute("occupation") != "Townsfolk")
                              .Select(n => (string)n.Attribute("id")).ToList();
        Assert.AreEqual(0, other.Count, "not Townsfolk: " + string.Join(", ", other));
    }
}
