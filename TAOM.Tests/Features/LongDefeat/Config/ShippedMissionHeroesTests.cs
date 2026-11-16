using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The mission heroes (#1335, Phase 2 of #1325): <c>characters/taom_mission_heroes.xml</c> holds the Long Defeat's own Aragorn, Legolas
/// and Gimli, so no campaign <c>Hero</c> ever stands behind a mission agent. Reads the shipped files. That every item id exists is
/// <c>python tools/validate_moduledata.py</c>'s check, because the weapons live in the Armory module.
/// </summary>
[TestClass]
public class ShippedMissionHeroesTests
{
    private static readonly string[] Ids = { "taom_hero_aragorn", "taom_hero_legolas", "taom_hero_gimli" };

    private static List<XElement> Heroes() =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "characters", "taom_mission_heroes.xml")).Root!.Elements("NPCCharacter").ToList();

    private static XElement Hero(string id) => Heroes().Single(h => (string?)h.Attribute("id") == id);

    // The battle roster: the one with no civilian flag.
    private static Dictionary<string, string> Kit(string id) => Hero(id).Element("Equipments")!.Elements("EquipmentRoster")
        .Single(r => r.Attribute("civilian") == null).Elements("equipment")
        .ToDictionary(e => (string)e.Attribute("slot")!, e => (string)e.Attribute("id")!);

    [TestMethod]
    public void TheFile_HoldsExactlyTheThreeHeroes()
    {
        CollectionAssert.AreEquivalent(Ids, Heroes().Select(h => (string?)h.Attribute("id")).ToArray());
    }

    [TestMethod]
    public void EveryHero_IsMarkedHero_AndStaysOutOfTheCustomBattlePickers()
    {
        foreach (var hero in Heroes())
        {
            var id = (string?)hero.Attribute("id");
            // is_hero reaches the engine's hero checks only in a Custom Game, where CharacterObject has no campaign Hero. In a campaign
            // IsHero is false (CharacterObject.IsHero => _heroObject != null), so no take-over refusal and no shop upgrade skip there.
            Assert.AreEqual("true", (string?)hero.Attribute("is_hero"), id);
            // The Custom Battle pickers list soldiers and heroes that are not obsolete, and Custom Battle loads CustomGame data too.
            Assert.AreEqual("true", (string?)hero.Attribute("is_obsolete"), id);
        }
    }

    [TestMethod]
    public void NoHero_HasACampaignHeroEntry()
    {
        // A <Hero> entry would make a campaign Hero stand behind the character: a lord the world could move, kill and list.
        var root = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var files = Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
            .Where(f => !f.Replace('\\', '/').Contains("/Languages/"));

        foreach (var file in files)
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file), "<Hero\\b[^>]*\\bid=\"(?:Hero\\.)?taom_hero_"), file);
    }

    [TestMethod]
    public void EveryHero_IsNotATemplate_AndAWanderer_SoNoCompanionPoolOrEncyclopediaListsHim()
    {
        // The wanderer pool takes IsTemplate && Occupation == Wanderer (CompanionsCampaignBehavior), and the encyclopedia lists only
        // soldier-like occupations. Wanderer without a template stays out of both, as npcs_harad_lords does.
        foreach (var hero in Heroes())
        {
            var id = (string?)hero.Attribute("id");
            Assert.AreNotEqual("true", (string?)hero.Attribute("is_template"), id);
            Assert.AreEqual("Wanderer", (string?)hero.Attribute("occupation"), id);
        }
    }

    [TestMethod]
    public void EveryHero_IsLevel36_SoACampaignReadsBattleTierSeven()
    {
        // With no Hero entry a campaign reads the XML level: BasicCharacterObject.GetBattleTier returns 7 for a hero but
        // Min(Max(Ceiling((Level - 5) / 5), 0), 7) otherwise, so a missing level is tier 0 and the elf and dwarf race abilities
        // would run at their minimum. Level 36 gives ceil(31 / 5) = 7, the tier a Custom Game already gives the heroes.
        // The level does more, and that is accepted: in a Custom Game GetPower rises from 0.50 to 5.49 (BasicCharacterObject.cs:257-261) and SkillFactor
        // from 1/32 to 1 (:74), which the AI's formation power sums and the agent's position error read (Agent.cs:1440, :5119-5120); in a campaign GetPower
        // and GetBattlePower follow the tier (CharacterObject.cs:609-616).
        foreach (var hero in Heroes())
            Assert.AreEqual("36", (string?)hero.Attribute("level"), (string?)hero.Attribute("id"));
    }

    [TestMethod]
    [DataRow("taom_hero_aragorn", "{=nc_aragorn_name}Aragorn", "Culture.gondor", "human")]
    [DataRow("taom_hero_legolas", "{=nc_legolas_name}Legolas", "Culture.mirkwood", "elf")]
    [DataRow("taom_hero_gimli", "{=nc_gimli_name}Gimli", "Culture.erebor", "dwarf")]
    public void EveryHero_KeepsHisCompanionsNameKeyCultureRaceAndFace(string id, string name, string culture, string race)
    {
        var hero = Hero(id);
        var companion = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "named_companions", "named_companions.xml")).Root!
            .Elements("NPCCharacter").Single(c => (string?)c.Attribute("id") == id.Replace("taom_hero_", "named_companion_"));

        // The same name under the same key: the existing translations apply, as for the Fords of War's troop copies.
        Assert.AreEqual(name, (string?)hero.Attribute("name"));
        Assert.AreEqual((string?)companion.Attribute("name"), (string?)hero.Attribute("name"));
        Assert.AreEqual(culture, (string?)hero.Attribute("culture"));
        Assert.AreEqual(race, (string?)hero.Attribute("race"));
        Assert.AreEqual((string?)companion.Descendants("BodyProperties").Single().Attribute("key"),
            (string?)hero.Descendants("BodyProperties").Single().Attribute("key"), "the face the player knows");
    }

    [TestMethod]
    public void NoHero_HasAHorse()
    {
        // A mounted share of a tenth or more switches a formation to mounted spacing (Formation.CalculateHasSignificantNumberOfMounted), and one rider is a third of the heroes' group.
        foreach (var id in Ids)
        {
            var kit = Kit(id);
            Assert.IsFalse(kit.ContainsKey("Horse") || kit.ContainsKey("HorseHarness"), id);
        }
    }

    [TestMethod]
    public void Aragorn_CarriesHisSwordAndNoBow()
    {
        // An AI hero with a bow stands off with the archers; his troop buffs need him in the melee line.
        var kit = Kit("taom_hero_aragorn");

        Assert.AreEqual("Item.strider_sword", kit["Item0"]);
        Assert.AreEqual(1, kit.Keys.Count(slot => slot.StartsWith("Item")), "the sword only");
    }

    [TestMethod]
    public void Legolas_HoldsHisBowFirst_WithTwoQuivers_AndHisSword()
    {
        // The bow in the first slot: the engine wields the first weapon slot at the spawn (Mission.SpawnTroopWithAgentBuildData runs WieldInitialWeapons).
        var kit = Kit("taom_hero_legolas");

        Assert.AreEqual("Item.wm_mirkwood_bow_a01", kit["Item0"], "the companion's bow (accuracy 98, speed 99), not the lord's a04 (80, 90)");
        Assert.AreEqual("Item.wm_mirkwood_arrow_a03", kit["Item1"]);
        Assert.AreEqual("Item.wm_mirkwood_arrow_a03", kit["Item2"]);
        Assert.AreEqual("Item.wm_legolas_sword", kit["Item3"]);
    }

    [TestMethod]
    public void Gimli_CarriesTheTwoHandedAxeAndNothingElseInHisHands()
    {
        var kit = Kit("taom_hero_gimli");

        Assert.AreEqual("Item.sm_dwarf_dain_axe_a", kit["Item0"]);
        Assert.AreEqual(1, kit.Keys.Count(slot => slot.StartsWith("Item")), "no shield: the axe needs both hands");
    }

    [TestMethod]
    public void TheFile_LoadsForCampaignStoryModeAndCustomGame_BeforeTheCustomBattleFilter()
    {
        // CustomGame is the Long Defeat's game type; Campaign and CampaignStoryMode load the same characters for the free heroes of the
        // Helm's Deep story mission (#1366).
        var nodes = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml")).Descendants("XmlNode")
            .Where(n => (string?)n.Element("XmlName")?.Attribute("id") == "NPCCharacters").ToList();
        var paths = nodes.Select(n => (string?)n.Element("XmlName")!.Attribute("path")).ToList();
        var node = nodes.Single(n => (string?)n.Element("XmlName")!.Attribute("path") == "characters/taom_mission_heroes");

        CollectionAssert.AreEqual(new[] { "Campaign", "CampaignStoryMode", "CustomGame" },
            node.Element("IncludedGameTypes")!.Elements("GameType").Select(g => (string?)g.Attribute("value")).ToArray());
        Assert.IsTrue(paths.IndexOf("characters/taom_mission_heroes") < paths.IndexOf("custom_battle/custom_battle_harad_troop_filter"),
            "the filter node comes after every NPCCharacters node it may hide");
    }

    // ---- the shop's heroes block (#1325) ----

    private static ShopPlan RohanShop(out IModLogger logger)
    {
        logger = Substitute.For<IModLogger>();
        var text = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", "shop.json"));
        return ShopReader.Read(text, new LongDefeatConfigScope(logger, "shop.json"))["rohan"];
    }

    [TestMethod]
    public void TheRohanShop_SellsTheThreeHeroes_EachHisOwnCharacter_WithNoWarning()
    {
        var shop = RohanShop(out var logger);

        CollectionAssert.AreEqual(new[] { "aragorn", "legolas", "gimli" }, shop.Heroes.Select(h => h.Id).ToArray());
        CollectionAssert.AreEquivalent(Ids, shop.Heroes.Select(h => h.TroopId).ToArray(), "a troop id that is not in taom_mission_heroes.xml refuses the launch");
        Assert.AreEqual(0, logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IModLogger.LogWarning)
            && call.GetArguments()[0] is string m && m.Contains(".heroes")));
    }

    [TestMethod]
    public void EveryShopHero_IsCheap_AndHardToKill()
    {
        // "Relatively cheap" (the maintainer, 2026-11-15): no hero costs more than the shop's dearest pack. The numbers are his to tune.
        var shop = RohanShop(out _);
        var dearestPack = shop.Offers.Max(o => o.Price);

        foreach (var hero in shop.Heroes)
        {
            Assert.IsTrue(hero.Price <= dearestPack, $"{hero.Id} costs {hero.Price}, the dearest pack {dearestPack}");
            Assert.IsTrue(hero.HealthFactor > 1f, $"{hero.Id}: a hero at a soldier's health falls in the first minute");
        }
    }

    [TestMethod]
    public void EveryShopHero_ShowsHisCharactersName_AndHisTextIsRegisteredWithTheSameDefault()
    {
        var strings = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(r => (string)r.Attribute("id")!, r => (string)r.Attribute("text")!);

        foreach (var hero in RohanShop(out _).Heroes)
        {
            // The row shows the name the body on the field carries, under the same key.
            Assert.AreEqual((string?)Hero(hero.TroopId).Attribute("name"), hero.Name, hero.Id);
            StringAssert.StartsWith(hero.Text, "{=TAOM_LD_Hero_", hero.Id);
            var key = hero.Text.Substring(2, hero.Text.IndexOf('}') - 2);
            Assert.IsTrue(strings.TryGetValue(key, out var registered), key + " is not in taom_module_strings.xml");
            Assert.AreEqual(hero.Text, registered, key);
        }
    }
}
