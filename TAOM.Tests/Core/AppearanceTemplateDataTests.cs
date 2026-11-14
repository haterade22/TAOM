using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Core;

/// <summary>
/// Face template ranges in TAOM_bodyproperties.xml that troops, notables, wanderers and template-born heroes
/// are rolled from, held to the look of the peoples in the books and films. Fields (value / 63): skin is bits
/// 48-53 and hair colour bits 30-35 of the key's first 64-bit part. Gondor and Arnor men are fair (skin at
/// most 9), the Rohirrim mostly blond (hair at most 22), elves never look old (face age at most 30), and the
/// Galadhrim are golden or fair haired, so Lothlorien characters use fighter_lothlorien (hair at most 22)
/// instead of the dark-haired Noldor range fighter_rivendell. No Umbar character uses fighter_gondor: Umbar's
/// notables, wanderers and education templates use the dark fighter_umbar range, as its lords and troops do, so
/// narrowing fighter_gondor to fair skin cannot lighten them.
///
/// Two limits this class does not cover. (1) Lothlorien's culture block in taom_spcultures.xml borrows 35
/// Rivendell townsfolk, guard, caravan and tavern roles (NPCCharacter.*_rivendell) that stay on fighter_rivendell,
/// so those townsfolk keep dark hair; the test below checks only characters whose own culture is Lothlorien.
/// (2) The face age cap reaches troops and townsfolk only: notables and wanderers take their face age from their
/// campaign age, so the cap on the elf templates does not change them.
/// </summary>
[TestClass]
public class AppearanceTemplateDataTests
{
    private static string ModuleData => RepoPath("Main", "_Module", "ModuleData");

    private static int Field(string key, int part, int shift)
    {
        var value = ulong.Parse(key.Substring(part * 16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (int)((value >> shift) & 63);
    }

    private static int Skin(string key) => Field(key, 0, 48);
    private static int Hair(string key) => Field(key, 0, 30);

    private static XElement Template(string id)
    {
        var doc = XDocument.Load(Path.Combine(ModuleData, "TAOM_bodyproperties.xml"));
        var t = doc.Descendants("BodyProperty").FirstOrDefault(e => (string)e.Attribute("id") == id);
        Assert.IsNotNull(t, $"BodyProperty {id} is missing from TAOM_bodyproperties.xml.");
        return t;
    }

    private static IEnumerable<XElement> Ends(XElement t) =>
        new[] { t.Element("BodyPropertiesMin"), t.Element("BodyPropertiesMax") }.Where(e => e != null);

    private static string Key(XElement e) => (string)e.Attribute("key");

    [TestMethod]
    public void GondorTemplate_SkinStaysFair()
    {
        foreach (var e in Ends(Template("fighter_gondor")))
            Assert.IsTrue(Skin(Key(e)) <= 9, $"fighter_gondor {e.Name.LocalName} skin {Skin(Key(e))}/63 is above 9.");
    }

    [TestMethod]
    public void RohanTemplate_HairStaysFair()
    {
        foreach (var e in Ends(Template("fighter_rohan")))
            Assert.IsTrue(Hair(Key(e)) <= 22, $"fighter_rohan {e.Name.LocalName} hair {Hair(Key(e))}/63 is above 22.");
    }

    [TestMethod]
    public void LothlorienTemplate_HairIsGoldenOrFair()
    {
        foreach (var e in Ends(Template("fighter_lothlorien")))
            Assert.IsTrue(Hair(Key(e)) <= 22, $"fighter_lothlorien {e.Name.LocalName} hair {Hair(Key(e))}/63 is above 22.");
    }

    [DataTestMethod]
    [DataRow("fighter_rivendell")]
    [DataRow("fighter_mirkwood")]
    [DataRow("fighter_lothlorien")]
    public void ElfTemplates_FaceAgeStaysYoung(string id)
    {
        foreach (var e in Ends(Template(id)))
        {
            var age = float.Parse((string)e.Attribute("age"), CultureInfo.InvariantCulture);
            Assert.IsTrue(age <= 30f, $"{id} {e.Name.LocalName} face age {age} is above 30.");
        }
    }

    [TestMethod]
    public void LothlorienCharacters_UseTheLothlorienTemplate()
    {
        var wrong = new List<string>();
        var moved = 0;
        foreach (var file in Directory.EnumerateFiles(ModuleData, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)) continue;
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }
            foreach (var npc in doc.Descendants("NPCCharacter").Where(n => (string)n.Attribute("culture") == "Culture.lothlorien"))
            {
                var template = (string)npc.Element("face")?.Element("face_key_template")?.Attribute("value");
                if (template == "BodyProperty.fighter_lothlorien") moved++;
                if (template == "BodyProperty.fighter_rivendell") wrong.Add($"{npc.Attribute("id")?.Value} ({Path.GetFileName(file)})");
            }
        }

        Assert.AreEqual(0, wrong.Count, "Lothlorien characters still on fighter_rivendell: " + string.Join(", ", wrong));
        Assert.IsTrue(moved > 50, $"Expected the Lothlorien troops on fighter_lothlorien, found {moved}.");
    }

    [TestMethod]
    public void UmbarCharacters_AreNotOnTheGondorTemplate()
    {
        var wrong = new List<string>();
        var onUmbar = 0;
        foreach (var file in Directory.EnumerateFiles(ModuleData, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)) continue;
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }
            foreach (var npc in doc.Descendants("NPCCharacter").Where(n => (string)n.Attribute("culture") == "Culture.umbar"))
            {
                var template = (string)npc.Element("face")?.Element("face_key_template")?.Attribute("value");
                if (template == "BodyProperty.fighter_umbar") onUmbar++;
                if (template == "BodyProperty.fighter_gondor") wrong.Add($"{npc.Attribute("id")?.Value} ({Path.GetFileName(file)})");
            }
        }

        Assert.AreEqual(0, wrong.Count, "Umbar characters on fighter_gondor: " + string.Join(", ", wrong));
        Assert.IsTrue(onUmbar >= 44, $"Expected Umbar's notables, wanderers and education templates on fighter_umbar, found {onUmbar}.");
    }
}
