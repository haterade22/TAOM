using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every NPCCharacter must declare a &lt;face&gt; block. A character without one renders as a
/// toddler.
///
/// Why this exists: players reported the arena's "Practice Fighter" and "Gear Dummy" spawning as
/// child-sized agents. The cause is entirely in the data. `BasicCharacterObject.Deserialize`
/// (v1.4.8) initialises two local `BodyProperties` to `default` and, if no &lt;face&gt; node was
/// read, registers the character's `MBBodyProperty` from those defaults — an all-zero struct whose
/// `DynamicBodyProperties.Age` is 0. The engine then picks the body mesh by age, and `skins.xml`
/// maps age 0 to `mesh_maturity_type="toddler"` (min_scale 0.52 against the adult 1.07).
///
/// Nothing warns. The character's own `Age` property is separately clamped to `max(20, ...)` at
/// deserialisation, so `Mission.SpawnAgent`'s two age guards (age == 0 becomes 29; a sub-teenager
/// in a battle-like mission becomes 27) both read an adult value and never fire. The visual age and
/// the campaign age are different numbers and only the visual one is wrong.
///
/// Vanilla's equivalents all carry `&lt;face_key_template value="BodyProperty.guard"/&gt;`, and so
/// do the nine TAOM cultures that were authored later. Ten cultures shipped without it. This test
/// is the gate so an eleventh cannot.
///
/// Data-only check, so it needs no game and runs in milliseconds.
/// </summary>
[TestClass]
public class CharacterFaceCoverageTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    [TestMethod]
    public void EveryNpcCharacterDeclaresAFace()
    {
        var root = FindRepoRoot();
        var moduleData = Path.Combine(root, "Main", "_Module", "ModuleData");
        Assert.IsTrue(Directory.Exists(moduleData), $"ModuleData not found at {moduleData}");

        var scanned = 0;
        var faceless = new List<string>();

        foreach (var file in Directory.EnumerateFiles(moduleData, "*.xml", SearchOption.AllDirectories))
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch (Exception)
            {
                // Well-formedness is another gate's job; a parse failure here must not be reported
                // as a missing face.
                continue;
            }

            foreach (var character in doc.Descendants("NPCCharacter"))
            {
                scanned++;
                if (character.Element("face") != null)
                    continue;

                var id = (string)character.Attribute("id") ?? "<no id>";
                faceless.Add($"{id}  ({Path.GetFileName(file)})");
            }
        }

        Assert.IsTrue(scanned > 0, "scanned no NPCCharacter entries; the ModuleData layout changed");

        Assert.AreEqual(0, faceless.Count,
            "These NPCCharacters declare no <face>, so the engine gives them BodyProperties with " +
            "Age 0 and renders them as toddlers wherever they spawn. Add a " +
            "<face><face_key_template value=\"BodyProperty.fighter_<culture>\" /></face> block, " +
            "matching the sibling characters in the same file.\n  " +
            string.Join("\n  ", faceless.OrderBy(x => x, StringComparer.Ordinal)));
    }
}
