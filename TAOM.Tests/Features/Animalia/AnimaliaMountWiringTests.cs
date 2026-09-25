using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Animalia;
using TAOM.Features.MonsterSize;
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
        // Monster and set ids from AnimaliaConfig, so the Armory checks below also prove the C# attach key and drift
        // guard name what the Armory declares (AnimaliaConfigTests pins the constants as literals).
        ("elk", "taom_animalia_elk_a", "animalia_elk_08", AnimaliaConfig.ElkMonsterId, AnimaliaConfig.ElkActionSetId, "taom_test_animalia_elk_rider"),
        ("moose", "taom_animalia_moose_a", "animalia_moose_big", AnimaliaConfig.MooseMonsterId, AnimaliaConfig.MooseActionSetId, "taom_test_animalia_moose_rider"),
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
    public void AnimaliaActionSets_BindHorseActionsAndTheirOwnAntler_ToClipsThatExist()
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
        // Each animal's OWN antler action is the one exception: TAOM's behavior tree fires it (pinned below).
        var ownAntler = new Dictionary<string, string>
        {
            ["elk"] = AnimaliaConfig.ElkAttackActionName,
            ["moose"] = AnimaliaConfig.MooseAttackActionName,
        };
        var doc = XDocument.Load(Path.Combine(armory, "ModuleData", "action_sets.xml"));
        foreach (var m in Mounts)
        {
            foreach (var action in ActionSet(doc, m.ActionSet).Elements("action"))
            {
                string type = (string?)action.Attribute("type") ?? "";
                string anim = (string?)action.Attribute("animation") ?? "";
                Assert.IsTrue(horseActions.Contains(type) || type == ownAntler[m.Animal],
                    $"{m.ActionSet}: {type} is neither an as_horse action nor this animal's own antler, so nothing fires it");
                Assert.IsTrue(clips.Contains(anim), $"{m.ActionSet}: {type} names clip {anim}, which no _anm.tpac defines");
                StringAssert.StartsWith(anim, "anim_animalia_" + m.Animal + "_", $"{m.ActionSet}: {type} plays another animal's clip");
            }
        }
    }

    [TestMethod]
    public void AntlerActions_AreKickTyped_AndBoundToTheirAttackClip()
    {
        // Typed actt_kick like the war ram's act_war_ram_butt, the ram's proven type: outside Rear (Agent.Mount refuses a
        // mount whose channel-0 action is Rear) and outside the 48..51 band IsInBeingStruckAction reads. TAOM's own busy
        // check (ElephantLikeCombatProfile.IsAttack) compares action indices, so the type never reaches it; and
        // act_horse_kick itself is fired by the engine (the horse usage set's kick_action).
        string armory = Armory();
        var types = XDocument.Load(Path.Combine(armory, "ModuleData", "action_types.xml")).Descendants("action").ToList();
        var sets = XDocument.Load(Path.Combine(armory, "ModuleData", "action_sets.xml"));
        foreach (var (set, action, clip) in new[]
                 {
                     ("as_animalia_elk", AnimaliaConfig.ElkAttackActionName, AnimaliaConfig.ElkAttackClip),
                     ("as_animalia_moose", AnimaliaConfig.MooseAttackActionName, AnimaliaConfig.MooseAttackClip),
                 })
        {
            var declared = types.Where(t => (string?)t.Attribute("name") == action).ToList();
            Assert.AreEqual(1, declared.Count, $"{action} must be declared once in the Armory's action_types.xml");
            Assert.AreEqual("actt_kick", (string?)declared[0].Attribute("type"), $"{action} must be typed actt_kick");
            var bound = ActionSet(sets, set).Elements("action").SingleOrDefault(a => (string?)a.Attribute("type") == action);
            Assert.IsNotNull(bound, $"{set} does not bind {action}");
            Assert.AreEqual(clip, (string?)bound!.Attribute("animation"), $"{set} binds {action} to the wrong clip");
        }
    }

    // Who rides them (Mike, 2026-09-23): the moose carries Thranduil and the Mirkwood lords, the Animalia elk the
    // lower Mirkwood cavalry and the elk_rider career start; the great elk (#636) keeps the top cavalry troop
    // (ElkMountWiringTests). Every one of them sits on the great elk's saddle, the only elk seat there is.
    private const string AnimaliaElk = "Item.taom_animalia_elk_a";
    private const string Moose = "Item.taom_animalia_moose_a";

    private static XDocument LoadRepo(params string[] relative)
    {
        var path = Path.Combine(new[] { ModuleDataPath }.Concat(relative).ToArray());
        Assert.IsTrue(File.Exists(path), $"{path} not found");
        return XDocument.Load(path);
    }

    private static bool IsBattleSet(XElement set) => set.Attribute("equipmentType") == null;

    private static void AssertBattleSetsRide(XDocument doc, IEnumerable<string> rosterIds, string mount, string what)
    {
        foreach (var rosterId in rosterIds)
        {
            var roster = doc.Descendants("EquipmentRoster").SingleOrDefault(r => (string?)r.Attribute("id") == rosterId);
            Assert.IsNotNull(roster, $"{rosterId} is missing");
            var sets = roster!.Elements("EquipmentSet").Where(IsBattleSet).ToList();
            Assert.IsTrue(sets.Count > 0, $"{rosterId} has no battle set");
            foreach (var set in sets)
            {
                Assert.AreEqual(mount, Slot(set, "Horse"), $"{rosterId} must ride the {what}");
                Assert.AreEqual(ElkSaddle, Slot(set, "HorseHarness"), $"{rosterId} must carry the elk saddle");
            }
        }
    }

    [TestMethod]
    public void MirkwoodLowerCavalry_RidesTheAnimaliaElk_OnTheElkSaddle()
    {
        // mirkwood_rochenlas upgrades to mirkwood_beleglas, the top of the line, which keeps the great elk. Pinned on
        // the troop-level override: MBEquipmentRoster.AddOverriddenEquipments copies it into every set (v1.5.3).
        var troop = LoadRepo("troops", "troops_mirkwood.xml").Descendants("NPCCharacter")
            .SingleOrDefault(n => (string?)n.Attribute("id") == "mirkwood_rochenlas");
        Assert.IsNotNull(troop, "mirkwood_rochenlas is missing from troops_mirkwood.xml");
        var equipments = troop!.Element("Equipments");
        Assert.IsNotNull(equipments, "mirkwood_rochenlas has no <Equipments>");
        Assert.AreEqual(AnimaliaElk, Slot(equipments!, "Horse"), "mirkwood_rochenlas must ride the Animalia elk");
        Assert.AreEqual(ElkSaddle, Slot(equipments!, "HorseHarness"), "mirkwood_rochenlas must carry the elk saddle");
    }

    [TestMethod]
    public void ThranduilAndTheMirkwoodLordTemplates_RideTheMoose_OnTheElkSaddle()
    {
        // thranduil_bat_equipment is his own set; the five templates are the other mounted Mirkwood lords' shared
        // battle rosters. Legolas keeps his horseless set, and the civilian sets keep their horses.
        var ids = new[] { "thranduil_bat_equipment" }.Concat("abcde".Select(c => $"mirkwood_bat_template_medium_{c}"));
        AssertBattleSetsRide(LoadRepo("equipmentsets", "taom_equipment_sets_mirkwood.xml"), ids, Moose, "moose");
    }

    [TestMethod]
    public void GeneratedMirkwoodLordAndRulerTemplates_RideTheMoose_OnTheElkSaddle()
    {
        // The IsLordTemplate / IsKingdomRulerTemplate battle rosters copy mirkwood_bat_template_medium_a: the engine
        // hands them to every Mirkwood hero who comes of age and to a new ruler after Thranduil, so a stale copy
        // would put the next generation of lords back on the old mount.
        var ids = new[] { "lord", "ruler" }
            .SelectMany(rank => new[] { "male", "female" }.Select(sex => $"taom_mirkwood_{rank}_battle_{sex}"));
        AssertBattleSetsRide(LoadRepo("equipmentsets", "taom_lord_template_equipment.xml"), ids, Moose, "moose");
    }

    [TestMethod]
    public void ElkRiderCareerStart_MountsThePlayerOnTheAnimaliaElk_OnTheElkSaddle()
    {
        // elk_rider is Mirkwood's only Cavalry career (CareerSystemIoC), so these two rosters are its start. A new
        // character starts on the lower cavalry's mount, as career kits carry the culture's lowest troop gear (#629).
        var ids = new[] { "m", "f" }.Select(s => $"player_career_mirkwood_cavalry_{s}");
        AssertBattleSetsRide(LoadRepo("equipmentsets", "taom_career_starting_equipment.xml"), ids, AnimaliaElk, "Animalia elk");
    }

    [TestMethod]
    public void ElkRiderStartingMount_AndItsSaddle_AreGuaranteedStockInMirkwoodMarkets()
    {
        // Mike, 2026-09-23: "The starting elk should also be available in the marketplace." A player who loses the
        // career's mount can buy the same one. Read from the career roster, so a new starting mount must be routed
        // too. Its Culture.mirkwood tag alone would put it in the Mirkwood pool's random draw; the routing entry's
        // min_stock is what guarantees a town has one (CultureMarketplaceMaintenanceService.EnsureGuaranteedStock
        // adds it by id).
        var career = LoadRepo("equipmentsets", "taom_career_starting_equipment.xml").Descendants("EquipmentRoster")
            .Single(r => (string?)r.Attribute("id") == "player_career_mirkwood_cavalry_m");
        var set = career.Elements("EquipmentSet").Single(IsBattleSet);
        var routing = LoadRepo("culture_marketplace", "culture_marketplace_config.xml").Descendants("Routing").Single();
        foreach (var slot in new[] { "Horse", "HorseHarness" })
        {
            string? itemRef = Slot(set, slot);
            Assert.IsNotNull(itemRef, $"the elk_rider start has no {slot}");
            string itemId = itemRef!.Substring("Item.".Length);
            var entry = routing.Elements("Item").SingleOrDefault(i => (string?)i.Attribute("id") == itemId);
            Assert.IsNotNull(entry, $"{itemId}, the elk_rider start's {slot}, is not routed in culture_marketplace_config.xml");
            var cultures = ((string?)entry!.Attribute("cultures") ?? "").Split(',').Select(c => c.Trim());
            Assert.IsTrue(cultures.Contains("mirkwood"), $"{itemId} must be routed to mirkwood");
            Assert.IsTrue(int.TryParse((string?)entry.Attribute("min_stock"), out int minStock) && minStock >= 1,
                $"{itemId} needs min_stock of at least 1, or a Mirkwood town may have none to sell");
        }
    }

    [TestMethod]
    public void AnimaliaMonsters_DeclareTheirSize_AndTheirItemsHoldTheSchemaPlaceholder()
    {
        // One place for the size (Mike: "the monster xml should control the size of the animal"): the Monster's
        // taom_body_length, copied into the item at game init. Items.xsd requires body_length on <Horse>, so the item
        // keeps the neutral placeholder; any other value there would read as a second size.
        string md = Path.Combine(Armory(), "ModuleData");
        var monsters = XDocument.Load(Path.Combine(md, "Monsters", "LOTR", "lotr_monster_animalia.xml")).Descendants("Monster").ToList();
        var items = XDocument.Load(Path.Combine(md, "LOTRLOME_items", "LOTRAOM_horses.xml")).Descendants("Item").ToList();
        foreach (var m in Mounts)
        {
            var monster = monsters.SingleOrDefault(x => (string?)x.Attribute("id") == m.Monster);
            Assert.IsNotNull(monster, $"Monster {m.Monster} is missing from lotr_monster_animalia.xml");
            string? size = (string?)monster!.Attribute(MonsterSizeConfig.AttributeName);
            Assert.IsTrue(MonsterSizeService.TryParseBodyLength(size, out _),
                $"{m.Monster} must declare {MonsterSizeConfig.AttributeName} as a whole number from " +
                $"{MonsterSizeConfig.MinBodyLength} to {MonsterSizeConfig.MaxBodyLength}, found \"{size}\"");

            var item = items.SingleOrDefault(i => (string?)i.Attribute("id") == m.Item);
            Assert.IsNotNull(item, $"{m.Item} is missing from the Armory");
            Assert.AreEqual(MonsterSizeConfig.ItemPlaceholderBodyLength.ToString(), (string?)item!.Descendants("Horse").Single().Attribute("body_length"),
                $"{m.Item} must keep body_length at the placeholder: the size lives on Monster {m.Monster}, so resize it there");
        }
    }
}
