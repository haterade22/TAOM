using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits;
using TAOM.Tests.Infrastructure;

// Creature Bandits (#692): the C# catalogue and the troop XML must agree. A catalogue id with no troop spawns
// nothing; a troop missing from the catalogue spawns as a goblin husk riding its spider.

namespace TAOM.Tests.Features.CreatureBandits;

[TestClass]
public class CreatureBanditDataTests
{
    private static readonly string[] SpiderItems = { "Item.spider_mount_a", "Item.spider_mount_brown", "Item.spider_mount_pale" };

    private static XElement[] Troops() =>
        XDocument.Parse(RepoPaths.ReadSource("Main/_Module/ModuleData/characters/creature_bandits.xml"))
            .Root!.Elements("NPCCharacter").ToArray();

    [TestMethod]
    public void TroopFile_DeclaresExactlyTheCatalogue()
    {
        CollectionAssert.AreEquivalent(CreatureBanditsConfig.CreatureTroopIds.ToList(),
            Troops().Select(t => (string)t.Attribute("id")!).ToList());
    }

    [TestMethod]
    public void EveryCreatureTroop_IsAHiddenBanditWithASpiderAndNoWeapon()
    {
        foreach (var troop in Troops())
        {
            string id = (string)troop.Attribute("id")!;
            Assert.AreEqual("true", (string?)troop.Attribute("is_hidden_encyclopedia"), $"{id}: not hidden from the encyclopedia");
            Assert.AreEqual("Bandit", (string?)troop.Attribute("occupation"), $"{id}: not a bandit");

            var slots = troop.Descendants("equipment").ToList();
            var horse = slots.Where(s => (string?)s.Attribute("slot") == "Horse").Select(s => (string?)s.Attribute("id")).ToList();
            Assert.IsTrue(horse.Count > 0 && horse.All(h => SpiderItems.Contains(h)), $"{id}: every roster needs a spider in its Horse slot");
            Assert.IsFalse(slots.Any(s => ((string?)s.Attribute("slot"))?.StartsWith("Item") == true),
                $"{id}: a creature carries no weapon (the fallback husk would wield it)");
        }
    }

    [TestMethod]
    public void BroodClan_AndItsTemplate_MatchTheCode()
    {
        // Three seams key on the clan id: the looter cap (TaomBanditDensityModel), the brood spawner and the no-parley
        // patch. A rename in the XML alone would fail silently: no brood spawns, and vanilla spawns the clan map-wide.
        var clan = XDocument.Parse(RepoPaths.ReadSource("Main/_Module/ModuleData/characters/clans.xml")).Root!
            .Elements("Faction").Single(f => (string?)f.Attribute("id") == CreatureBanditsConfig.BroodClanId);
        Assert.AreEqual("true", (string?)clan.Attribute("is_bandit"));
        string templateId = ((string)clan.Attribute("default_party_template")!).Replace("PartyTemplate.", "");

        var template = XDocument.Parse(RepoPaths.ReadSource("Main/_Module/ModuleData/taom_partyTemplates.xml")).Root!
            .Elements("MBPartyTemplate").Single(t => (string?)t.Attribute("id") == templateId);
        var troops = template.Descendants("PartyTemplateStack")
            .Select(s => ((string)s.Attribute("troop")!).Replace("NPCCharacter.", "")).ToList();
        Assert.AreEqual("taom_spider_brood_pale", troops[0],
            "the broodmother is the first stack, so roster index 0, the map icon's party leader (Patch94)");
        CollectionAssert.IsSubsetOf(troops, CreatureBanditsConfig.CreatureTroopIds.ToList(), "a brood carries creature troops only");
    }

    [TestMethod]
    public void TheLeaderIsTheHighestLevelTroop()
    {
        // The encounter conversation and the map icon both fall to the brood's leader stack; the conversation
        // picks the highest-tier troop, so the pale broodmother must outrank the rest.
        var byLevel = Troops().OrderByDescending(t => (int)t.Attribute("level")!).ToList();
        Assert.AreEqual("taom_spider_brood_pale", (string)byLevel[0].Attribute("id")!);
        Assert.IsTrue((int)byLevel[0].Attribute("level")! > (int)byLevel[1].Attribute("level")!);
    }
}
