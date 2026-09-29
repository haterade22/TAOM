using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits;
using TAOM.Tests.Infrastructure;

// Troll bands (#694): hidden bandit twins of Mordor's two trolls, in bands of two to four. The twins keep the Mordor
// trolls' race, level, skills, face and weapons but wear no armour (Mike, 2026-09-29); their ids differ too, so
// Mordor's recruitable trolls stay untouched and the prisoner rule can refuse the twins alone. Bands are hill trolls
// only for now (Mike, 2026-09-29): a bare cave troll's body has no cloth, its trousers belong to the armour mesh.

namespace TAOM.Tests.Features.CreatureBandits;

[TestClass]
public class TrollBanditDataTests
{
    private const string ModuleData = "Main/_Module/ModuleData/";

    private static XElement[] Twins() =>
        XDocument.Parse(RepoPaths.ReadSource(ModuleData + "characters/troll_bandits.xml")).Root!.Elements("NPCCharacter").ToArray();

    private static XElement MordorTroll(string race) =>
        XDocument.Parse(RepoPaths.ReadSource(ModuleData + "troops/troops_mordor.xml")).Root!.Elements("NPCCharacter")
            .Single(t => (string?)t.Attribute("id") == race);

    private static string Id(XElement e) => (string)e.Attribute("id")!;

    private static string[] Skills(XElement troop) =>
        troop.Element("skills")!.Elements("skill").Select(s => $"{s.Attribute("id")}={s.Attribute("value")}").OrderBy(s => s).ToArray();

    // Wild trolls wear no armour (Mike, 2026-09-29): a twin carries its Mordor troll's weapons only.
    private static readonly string[] ArmourSlots = { "Head", "Body", "Leg", "Gloves", "Cape" };

    private static string[] Rosters(XElement troop, bool withoutArmour = false) =>
        troop.Element("Equipments")!.Elements("EquipmentRoster")
            .Select(r => $"civilian={(string?)r.Attribute("civilian") ?? "false"}:"
                         + string.Join(";", r.Elements("equipment")
                             .Where(q => !withoutArmour || !ArmourSlots.Contains((string?)q.Attribute("slot")))
                             .Select(q => $"{q.Attribute("slot")}={q.Attribute("id")}")))
            .ToArray();

    private static XElement BandTemplate()
    {
        var clan = XDocument.Parse(RepoPaths.ReadSource(ModuleData + "characters/clans.xml")).Root!
            .Elements("Faction").Single(f => (string?)f.Attribute("id") == CreatureBanditsConfig.TrollClanId);
        string templateId = ((string)clan.Attribute("default_party_template")!).Replace("PartyTemplate.", "");
        return XDocument.Parse(RepoPaths.ReadSource(ModuleData + "taom_partyTemplates.xml")).Root!
            .Elements("MBPartyTemplate").Single(t => (string?)t.Attribute("id") == templateId);
    }

    [TestMethod]
    public void TrollFile_DeclaresExactlyTheTrollCatalogue()
        => CollectionAssert.AreEquivalent(CreatureBanditsConfig.TrollBanditTroopIds.ToList(), Twins().Select(Id).ToList());

    [TestMethod]
    public void TrollFile_LoadsInTheCampaign()
    {
        var nodes = XDocument.Parse(RepoPaths.ReadSource("Main/_Module/SubModule.xml")).Descendants("XmlNode").ToList();
        static string[] GameTypes(XElement n) => n.Descendants("GameType").Select(g => ((string?)g.Attribute("value"))!.Trim()).ToArray();
        var node = nodes.Single(n => (string?)n.Element("XmlName")?.Attribute("path") == "characters/troll_bandits");
        Assert.AreEqual("NPCCharacters", (string?)node.Element("XmlName")!.Attribute("id"));
        CollectionAssert.Contains(GameTypes(node), "Campaign");
        // The wild_trolls culture and the band template name the twins, so the twins load wherever those files do.
        foreach (var path in new[] { "taom_spcultures", "taom_partyTemplates" })
            foreach (var referrer in nodes.Where(n => (string?)n.Element("XmlName")?.Attribute("path") == path))
                CollectionAssert.IsSubsetOf(GameTypes(referrer), GameTypes(node), $"{path} loads in a game type the twins do not");
    }

    [TestMethod]
    public void EveryTwin_IsAHiddenWildTrollBandit()
    {
        foreach (var twin in Twins())
        {
            string id = Id(twin);
            Assert.AreEqual("true", (string?)twin.Attribute("is_hidden_encyclopedia"), $"{id}: not hidden from the encyclopedia");
            Assert.AreEqual("Bandit", (string?)twin.Attribute("occupation"), $"{id}: not a bandit");
            Assert.AreEqual("Culture." + CreatureBanditsConfig.TrollClanId, (string?)twin.Attribute("culture"), $"{id}: wrong culture");
            Assert.AreNotEqual("true", (string?)twin.Attribute("is_basic_troop"), $"{id}: a basic troop can be volunteered");
            Assert.IsNotNull(twin.Element("face"), $"{id}: an NPCCharacter with no <face> renders as a toddler");
        }
    }

    [TestMethod]
    public void EveryTwin_MatchesItsMordorTroll()
    {
        foreach (var twin in Twins())
        {
            string race = (string)twin.Attribute("race")!;
            Assert.IsTrue(race == "cave_troll" || race == "hill_troll", $"{Id(twin)}: race {race} is not a troll");
            var mordor = MordorTroll(race);
            Assert.AreEqual((string?)mordor.Attribute("level"), (string?)twin.Attribute("level"), $"{Id(twin)}: level");
            Assert.AreEqual((string?)mordor.Attribute("default_group"), (string?)twin.Attribute("default_group"), $"{Id(twin)}: group");
            CollectionAssert.AreEqual(Skills(mordor), Skills(twin), $"{Id(twin)}: skills");
            CollectionAssert.AreEqual(Rosters(mordor, withoutArmour: true), Rosters(twin), $"{Id(twin)}: the Mordor troll's weapons, no armour");
            Assert.AreEqual((string?)mordor.Element("face")!.Element("face_key_template")!.Attribute("value"),
                (string?)twin.Element("face")!.Element("face_key_template")!.Attribute("value"), $"{Id(twin)}: face");
        }
    }

    [TestMethod]
    public void TrollClan_IsABanditLooterClanOfItsOwnCulture()
    {
        // Three seams key on the clan id: the looter cap (TaomBanditDensityModel), the troll spawner and the no-parley
        // patch. A rename in the XML alone would fail silently: no band spawns, and vanilla spawns the clan map-wide.
        var clan = XDocument.Parse(RepoPaths.ReadSource(ModuleData + "characters/clans.xml")).Root!
            .Elements("Faction").Single(f => (string?)f.Attribute("id") == CreatureBanditsConfig.TrollClanId);
        Assert.AreEqual("true", (string?)clan.Attribute("is_bandit"));
        Assert.AreEqual("Culture." + CreatureBanditsConfig.TrollClanId, (string?)clan.Attribute("culture"));

        var culture = XDocument.Parse(RepoPaths.ReadSource(ModuleData + "taom_spcultures.xml")).Root!
            .Elements("Culture").Single(c => (string?)c.Attribute("id") == CreatureBanditsConfig.TrollClanId);
        Assert.AreEqual("true", (string?)culture.Attribute("is_bandit"));
        Assert.AreEqual("false", (string?)culture.Attribute("can_have_settlement"),
            "a looter faction: no hideouts, and the spawner places every band");
    }

    [TestMethod]
    public void BandTemplate_HoldsTwoToFourHillTrolls()
    {
        // Vanilla's bandit roll is min + (max - min) x ratio with the ratio below 1 (v1.5.3
        // DefaultPartySizeLimitModel.cs:346-399), and Patch39 caps its growth at each stack's max_value, so the stack
        // sums are the band's bounds.
        var stacks = BandTemplate().Descendants("PartyTemplateStack").ToList();
        var troops = stacks.Select(s => ((string)s.Attribute("troop")!).Replace("NPCCharacter.", "")).ToList();
        // Roster index 0 leads the map icon, drawn with the race's as_hill_troll_map (CreatureBanditLiveDataTests).
        Assert.IsTrue(troops.All(t => t == "taom_troll_bandit_hill"), "a band is hill trolls only for now");
        Assert.AreEqual(2, stacks.Sum(s => (int)s.Attribute("min_value")!), "at least two trolls");
        Assert.AreEqual(4, stacks.Sum(s => (int)s.Attribute("max_value")!), "at most four trolls");
    }

    [TestMethod]
    public void Twins_JoinNoOtherTemplateOrCulture()
    {
        // A twin in a lord's template, a recruitment config or a settled culture's troop slots would put an
        // uncapturable troll in an army. The name keys (taom_troll_bandit_cave_name) are not references.
        string root = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var twinRef = new Regex(@"taom_troll_bandit_(cave|hill)(?!_name)\b");
        var hits = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".xml") || f.EndsWith(".xslt") || f.EndsWith(".json"))
            .Where(f => !f.Replace('\\', '/').Contains("/Languages/"))
            .Where(f => twinRef.IsMatch(File.ReadAllText(f)))
            .Select(f => f.Substring(root.Length + 1).Replace('\\', '/'))
            .ToList();
        CollectionAssert.AreEquivalent(new[] { "characters/troll_bandits.xml", "taom_partyTemplates.xml", "taom_spcultures.xml" }, hits);

        string bandTemplate = (string)BandTemplate().Attribute("id")!;
        foreach (var template in XDocument.Parse(RepoPaths.ReadSource(ModuleData + "taom_partyTemplates.xml")).Root!.Elements("MBPartyTemplate"))
            if (template.ToString().Contains("taom_troll_bandit_"))
                Assert.AreEqual(bandTemplate, Id(template), "only the band template carries a twin");
        foreach (var culture in XDocument.Parse(RepoPaths.ReadSource(ModuleData + "taom_spcultures.xml")).Root!.Elements("Culture"))
            if (culture.ToString().Contains("taom_troll_bandit_"))
                Assert.AreEqual(CreatureBanditsConfig.TrollClanId, Id(culture), "only the troll culture names a twin");
    }
}
