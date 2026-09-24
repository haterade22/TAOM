using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.Animalia;

/// <summary>
/// Pins the Animalia elk and moose (#646, docs/features/animalia-elk-moose.md): horse-skeleton reskins that play
/// their OWN retargeted clips through action sets of their own. The test riders live in the repo; the Monsters,
/// action sets, items and clips live in the unversioned LOTRLOME_Armory, so those tests read the live install and
/// are Inconclusive where it is absent.
///
/// The clip-existence pin is the load-bearing one: an action bound to an animation name no package defines
/// compiles into a degenerate record that access-violates later in native code (creature-mount-authoring.md,
/// the an_spi_attack_back phantom, 2026-06-11), and nothing logs it at load.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class AnimaliaMountWiringTests
{
    private const string ElkSaddle = "Item.taom_elk_saddle_a";

    private static readonly (string Animal, string Item, string Mesh, string Monster, string ActionSet, string Rider)[] Mounts =
    {
        ("elk", "taom_animalia_elk_a", "animalia_elk_08", "taom_animalia_elk", "as_animalia_elk", "taom_test_animalia_elk_rider"),
        ("moose", "taom_animalia_moose_a", "animalia_moose_big", "taom_animalia_moose", "as_animalia_moose", "taom_test_animalia_moose_rider"),
    };

    private static string ModuleDataPath => CultureDataFixture.ModuleDataPath();

    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    private static string Armory()
    {
        string armory = Path.Combine(GameDir(), "Modules", "LOTRLOME_Armory");
        if (!Directory.Exists(armory))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live data cannot be checked here.");
        return armory;
    }

    private static string? Slot(XElement scope, string slot) =>
        scope.Elements()
            .Where(e => string.Equals(e.Name.LocalName, "equipment", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("slot"), slot, StringComparison.Ordinal))
            ?.Attribute("id")?.Value;

    private static XElement ActionSet(XDocument doc, string id)
    {
        var set = doc.Descendants("action_set").SingleOrDefault(s => (string?)s.Attribute("id") == id);
        Assert.IsNotNull(set, $"action set {id} is missing from the Armory's action_sets.xml");
        return set!;
    }

    [TestMethod]
    public void TestRiders_RideTheirAnimaliaMount_WithTheElkSaddle()
    {
        var doc = XDocument.Load(Path.Combine(ModuleDataPath, "troops", "troops_animalia_test.xml"));
        foreach (var m in Mounts)
        {
            var troop = doc.Descendants("NPCCharacter").SingleOrDefault(n => (string?)n.Attribute("id") == m.Rider);
            Assert.IsNotNull(troop, $"{m.Rider} is missing");
            var equipments = troop!.Element("Equipments");
            Assert.IsNotNull(equipments, $"{m.Rider} has no <Equipments>");
            Assert.AreEqual("Item." + m.Item, Slot(equipments!, "Horse"), $"{m.Rider} must ride the {m.Animal}");
            // Every Horse slot needs a harness beside it (MOUNT_WITHOUT_HARNESS): the Animalia meshes are bare animals.
            Assert.AreEqual(ElkSaddle, Slot(equipments!, "HorseHarness"), $"{m.Rider} must carry the elk saddle");
        }
    }

    [TestMethod]
    public void AnimaliaItems_NameTheirMonster_AndTheirReskinnedMesh()
    {
        var items = XDocument.Load(Path.Combine(Armory(), "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml")).Descendants("Item").ToList();
        foreach (var m in Mounts)
        {
            var item = items.SingleOrDefault(i => (string?)i.Attribute("id") == m.Item);
            Assert.IsNotNull(item, $"{m.Item} is missing from LOTRAOM_horses.xml");
            Assert.AreEqual(m.Mesh, (string?)item!.Attribute("mesh"), $"{m.Item} must use the reskinned mesh");
            var horse = item.Descendants("Horse").SingleOrDefault();
            Assert.IsNotNull(horse, $"{m.Item} has no Horse component");
            Assert.AreEqual("Monster." + m.Monster, (string?)horse!.Attribute("monster"), $"{m.Item} must name its own Monster");
        }
    }

    [TestMethod]
    public void AnimaliaMonsters_AreHorsesThatUseTheirOwnActionSet()
    {
        var path = Path.Combine(Armory(), "ModuleData", "Monsters", "LOTR", "lotr_monster_animalia.xml");
        Assert.IsTrue(File.Exists(path), $"{path} not found");
        var monsters = XDocument.Load(path).Descendants("Monster").ToList();
        foreach (var m in Mounts)
        {
            var monster = monsters.SingleOrDefault(x => (string?)x.Attribute("id") == m.Monster);
            Assert.IsNotNull(monster, $"Monster {m.Monster} is missing");
            Assert.AreEqual("horse", (string?)monster!.Attribute("base_monster"), $"{m.Monster} must be a horse reskin");
            Assert.AreEqual(m.ActionSet, (string?)monster.Attribute("action_set"), $"{m.Monster} must use {m.ActionSet}");
        }
        // The Monsters file loads only when the Armory's SubModule.xml registers it.
        string subModule = File.ReadAllText(Path.Combine(Armory(), "SubModule.xml"));
        StringAssert.Contains(subModule, "Monsters/LOTR/lotr_monster_animalia", "the Monsters file is not registered in the Armory's SubModule.xml");
    }

    [TestMethod]
    public void AnimaliaActionSets_ChildOfTheHorse_WithTheMapTwinTheEngineLooksUp()
    {
        var doc = XDocument.Load(Path.Combine(Armory(), "ModuleData", "action_sets.xml"));
        foreach (var m in Mounts)
        {
            var main = ActionSet(doc, m.ActionSet);
            Assert.AreEqual("as_horse", (string?)main.Attribute("base_set"), $"{m.ActionSet} must inherit the horse");
            Assert.AreEqual("horse_skeleton", (string?)main.Attribute("skeleton"), $"{m.ActionSet} must be on horse_skeleton");
            // MobilePartyVisual asks for ActionSetCode + "_map" and MBGlobals.GetActionSet throws on a miss.
            Assert.AreEqual("as_horse_map", (string?)ActionSet(doc, m.ActionSet + "_map").Attribute("base_set"));
            Assert.AreEqual("as_horse_town_and_village", (string?)ActionSet(doc, m.ActionSet + "_town_and_village").Attribute("base_set"));
            Assert.IsTrue(main.Elements("action").Any(), $"{m.ActionSet} binds no clip of its own");
        }
    }

    [TestMethod]
    public void AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist()
    {
        string armory = Armory();
        var clips = new HashSet<string>(
            Directory.GetFiles(Path.Combine(armory, "Assets", "creature", "elk", "animations"), "*_anm.tpac", SearchOption.AllDirectories)
                .Select(f => Path.GetFileName(f).Substring(0, Path.GetFileName(f).Length - "_anm.tpac".Length)),
            StringComparer.Ordinal);
        var horseActions = new HashSet<string>(
            XDocument.Load(Path.Combine(GameDir(), "Modules", "Native", "ModuleData", "action_sets.xml"))
                .Descendants("action_set").Single(s => (string?)s.Attribute("id") == "as_horse")
                .Elements("action").Select(a => (string)a.Attribute("type")!),
            StringComparer.Ordinal);
        var doc = XDocument.Load(Path.Combine(armory, "ModuleData", "action_sets.xml"));
        foreach (var m in Mounts)
        {
            foreach (var action in ActionSet(doc, m.ActionSet).Elements("action"))
            {
                string type = (string?)action.Attribute("type") ?? "";
                string anim = (string?)action.Attribute("animation") ?? "";
                Assert.IsTrue(horseActions.Contains(type), $"{m.ActionSet}: {type} is not an as_horse action, so nothing fires it");
                Assert.IsTrue(clips.Contains(anim), $"{m.ActionSet}: {type} names clip {anim}, which no _anm.tpac defines");
                StringAssert.StartsWith(anim, "anim_animalia_" + m.Animal + "_", $"{m.ActionSet}: {type} plays another animal's clip");
            }
        }
    }
}
