using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The howdah crew's own equipment set (#627, Mike 2026-09-19): a howdah archer carries a bow and quivers and nothing
/// else. The first crew test put harad_archer in the seats, whose roster carries a sword, and archers were seen with
/// swords drawn on the deck. They cannot use a melee weapon up there (the seat holds them on their frame and the
/// nearest enemy is three metres below), so the crew get a dedicated troop instead of the line archer.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class HowdahCrewLoadoutTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    // Slots the engine reads as weapons; anything else on the roster is armour.
    private static readonly string[] WeaponSlots = { "Item0", "Item1", "Item2", "Item3", "Item4" };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static XElement CrewTroop()
    {
        string troops = Path.Combine(RepoRoot(), "Main", "_Module", "ModuleData", "troops", "troops_harad.xml");
        var found = XDocument.Load(troops).Descendants("NPCCharacter")
            .Where(c => (string?)c.Attribute("id") == ElephantConfig.HowdahCrewCharacterId).ToList();
        Assert.AreEqual(1, found.Count,
            $"troops_harad.xml must define {ElephantConfig.HowdahCrewCharacterId} exactly once (HowdahCrewSpawner spawns it)");
        return found[0];
    }

    [TestMethod]
    public void TheCrewTroop_CarriesABowAndQuiversAndNothingElse()
    {
        var rosters = CrewTroop().Descendants("EquipmentRoster")
            .Where(r => (string?)r.Attribute("id") == null).ToList();
        Assert.IsTrue(rosters.Count > 0, "the crew troop has no battle EquipmentRoster");

        foreach (XElement roster in rosters)
        {
            var weapons = roster.Elements("equipment")
                .Where(e => WeaponSlots.Contains((string?)e.Attribute("slot")))
                .Select(e => (string?)e.Attribute("id") ?? "")
                .ToList();

            Assert.IsTrue(weapons.Count >= 2, "a howdah archer needs a bow and at least one quiver");
            Assert.IsTrue(weapons.Any(id => id.Contains("bow")), $"no bow in [{string.Join(", ", weapons)}]");
            Assert.IsTrue(weapons.Any(id => id.Contains("arrow")), $"no quiver in [{string.Join(", ", weapons)}]");
            foreach (string id in weapons)
                Assert.IsTrue(id.Contains("bow") || id.Contains("arrow"),
                    $"'{id}' is neither a bow nor a quiver: the crew shoot from the deck and never reach a melee target");
        }
    }

    [TestMethod]
    public void TheCrewTroop_HasAFaceTemplate()
    {
        // An NPCCharacter with no <face> deserialises age 0 and renders the toddler skin (docs/modding/body-properties.md).
        Assert.IsTrue(CrewTroop().Descendants("face_key_template").Any(),
            "the crew troop needs a face_key_template or the engine renders children on the howdah");
    }

    [TestMethod]
    public void TheCrewTroops_WeaponIds_ResolveToBowsAndArrows()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string modules = Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env, "Modules");
        // The crew's arrows are a SandBoxCore item, not an Armory one, so scanning only the Armory made every run
        // stop at the first Inconclusive and never check the bow (#627, data-flow review F-12).
        var itemDirs = new[]
        {
            Path.Combine(modules, "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items"),
            Path.Combine(modules, "SandBoxCore", "ModuleData"),
            Path.Combine(modules, "Native", "ModuleData"),
        }.Where(Directory.Exists).ToList();
        if (itemDirs.Count == 0)
            Assert.Inconclusive("No module item folders on this machine; item types cannot be resolved here.");

        var types = itemDirs.SelectMany(d => Directory.EnumerateFiles(d, "*.xml", SearchOption.AllDirectories))
            .SelectMany(f =>
            {
                try { return XDocument.Load(f).Descendants("Item"); }
                catch (Exception) { return Enumerable.Empty<XElement>(); }
            })
            .Where(i => (string?)i.Attribute("id") != null)
            .GroupBy(i => (string)i.Attribute("id")!)
            .ToDictionary(g => g.Key, g => (string?)g.First().Attribute("Type") ?? "");

        foreach (XElement roster in CrewTroop().Descendants("EquipmentRoster").Where(r => (string?)r.Attribute("id") == null))
            foreach (string id in roster.Elements("equipment")
                         .Where(e => WeaponSlots.Contains((string?)e.Attribute("slot")))
                         .Select(e => ((string?)e.Attribute("id") ?? "").Replace("Item.", "")))
            {
                // A FAILURE, not Inconclusive: the item roots exist (guarded above), so a missing id is the real
                // defect this gate is for. The crew's bow is a generated ladder_* item in the unversioned Armory, and
                // a reinstall drops those and spawns archers bowless with no error (#627, XML review F6).
                if (!types.TryGetValue(id, out string? type))
                    Assert.Fail($"'{id}' resolves in no installed module: a reinstall drops generated Armory items");
                Assert.IsTrue(type == "Bow" || type == "Arrows",
                    $"'{id}' is a {type}; a howdah archer carries only Bow and Arrows");
            }
    }
}
