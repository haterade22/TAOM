using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.MinorClanMigration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Execution;

/// <summary>
/// Every non-bandit clan the game loads resolves a side, on its OWN id and culture (#957).
/// <c>AlignmentService.ResolveSide(id, culture)</c> falls back from an unlisted or Neutral-listed id to
/// the culture, so a minor clan whose vanilla culture alignment.json does not list (the Axemen of Erebor
/// carry the vanilla <c>nord</c>, the Druedain <c>vakken</c>) resolved Neutral, and a clan whose vanilla
/// culture is on the wrong side (the Grey Company carry <c>empire</c>, which is Dunland, Evil) resolved
/// Evil. Clan rows in the table are the fix, and this gate keeps them complete. A row applies to a minor
/// clan only while it serves no kingdom: a mercenary contract makes the employer its map faction, and the
/// clan's heroes then side with the employer's pair, not with the clan's row.
/// </summary>
/// <remarks>
/// Runs the real <c>spclans.xslt</c> over the installed SandBox <c>spclans.xml</c> and adds the repo's
/// <c>characters/clans.xml</c>, the way the engine loads them (SubModule.xml), and the same for the
/// kingdoms (<c>spkingdoms.xslt</c> plus <c>taom_spkingdoms.xml</c>). It reports Inconclusive when the
/// game install is absent. The side rows are read through the real <see cref="AlignmentConfigProvider"/>.
/// </remarks>
[TestClass]
[TestCategory("LiveInstall")]
public class ShippedClanSideCoverageTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    /// <summary>The minor clans, their TAOM names, and the side each is meant to resolve (execution/alignment.json).</summary>
    private static readonly (string ClanId, string TaomName, FactionSide Side)[] IntendedMinorClanSides =
    {
        ("ghilman", "Serpent Guard", FactionSide.Evil),
        ("legion_of_the_betrayed", "Grey Company", FactionSide.Free),
        ("skolderbrotva", "Axemen of Erebor", FactionSide.Free),
        ("company_of_the_boar", "Corsair Blades", FactionSide.Neutral),
        ("beni_zilal", "Blind Eye", FactionSide.Evil),
        ("wolfskins", "Variag Ravagers", FactionSide.Neutral),
        ("brotherhood_of_woods", "Dunlending Reavers", FactionSide.Evil),
        ("hidden_hand", "Mouth's Servants", FactionSide.Evil),
        ("lakepike", "Wreckers of the Long Lake", FactionSide.Free),
        ("embers_of_flame", "Cult of the Lidless Eye", FactionSide.Evil),
        ("jawwal", "Sand-Riders", FactionSide.Evil),
        ("karakhuzaits", "Wild Easterlings", FactionSide.Evil),
        ("forest_people", "Druedain", FactionSide.Free),
        ("eleftheroi", "Beornings", FactionSide.Free),
    };

    /// <summary>Realms the table deliberately leaves Neutral: neither the kingdom nor its culture is listed free or evil.</summary>
    private static readonly string[] NeutralRealmIds = { "battania", "shaghana", "abanissa", "umbar", "cardolan", "rhudaur", "anduinvale", "angaladh" };

    private sealed class ClanRow
    {
        public string Id = "";
        public string CultureId = "";
        public string KingdomId = "";
        public bool IsBandit;
        public bool IsMinor;
        public List<string> LeaderTemplateIds = new();
    }

    private sealed class KingdomRow
    {
        public string Id = "";
        public string CultureId = "";
    }

    private static string ModuleDataPath => RepoPath("Main", "_Module", "ModuleData");

    private static string? InstalledModuleData(string module, string probeFile)
    {
        var game = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        foreach (var root in new[] { game, DefaultGameDir })
        {
            if (string.IsNullOrEmpty(root))
                continue;
            var path = Path.Combine(root, "Modules", module, "ModuleData");
            if (File.Exists(Path.Combine(path, probeFile)))
                return path;
        }
        return null;
    }

    private static string RequireSandBox()
    {
        var path = InstalledModuleData("SandBox", "spclans.xml");
        if (path == null || !File.Exists(Path.Combine(path, "spkingdoms.xml")))
            Assert.Inconclusive("The Bannerlord SandBox module is not installed here (set BANNERLORD_GAME_DIR).");
        return path!;
    }

    /// <summary>
    /// Reads a game file the way the loader does: a DOCTYPE is ignored, nothing external is resolved. Insignificant
    /// whitespace is dropped, as <c>XDocument.Load(path)</c> does, so an element compares equal to its transformed copy.
    /// </summary>
    private static XDocument LoadDocument(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, IgnoreWhitespace = true };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }

    /// <summary>Applies a stylesheet to a document the way MBObjectManager.ApplyXslt does.</summary>
    private static XDocument Transform(XDocument source, string stylesheet)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        var xslt = new XslCompiledTransform();
        using (var sheet = XmlReader.Create(stylesheet, settings))
            xslt.Load(sheet, new XsltSettings(false, false), null);

        using var input = source.CreateReader();
        using var buffer = new StringWriter();
        using (var output = XmlWriter.Create(buffer, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Auto }))
            xslt.Transform(input, output);
        return XDocument.Parse(buffer.ToString());
    }

    private static string IdOf(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
            return "";
        var dot = reference!.IndexOf('.');
        return dot < 0 ? reference : reference.Substring(dot + 1);
    }

    private static bool IsTrue(XElement e, string attribute) =>
        string.Equals((string?)e.Attribute(attribute), "true", StringComparison.OrdinalIgnoreCase);

    // The transforms are the slow part and the inputs never change within a run, so each loader runs once. Callers only
    // read the results. LoadCultureIds is deliberately not cached: its callers add to the set they get.
    private static readonly Lazy<List<ClanRow>> ClansCache = new(ReadClans);
    private static readonly Lazy<Dictionary<string, KingdomRow>> KingdomsCache = new(ReadKingdoms);
    private static readonly Lazy<(XDocument Transformed, Dictionary<string, XElement> Vanilla)> ClanNpcsCache = new(ReadClanNpcs);

    private static List<ClanRow> LoadClans() => ClansCache.Value;

    private static Dictionary<string, KingdomRow> LoadKingdoms() => KingdomsCache.Value;

    private static List<ClanRow> ReadClans()
    {
        var sandBox = RequireSandBox();
        var rows = new Dictionary<string, ClanRow>(StringComparer.Ordinal);

        var vanilla = Transform(LoadDocument(Path.Combine(sandBox, "spclans.xml")), Path.Combine(ModuleDataPath, "spclans.xslt"));
        var taom = XDocument.Load(Path.Combine(ModuleDataPath, "characters", "clans.xml"));
        foreach (var e in vanilla.Descendants("Faction").Concat(taom.Descendants("Faction")))
        {
            var id = (string?)e.Attribute("id");
            if (string.IsNullOrEmpty(id))
                continue;
            rows[id!] = new ClanRow
            {
                Id = id!,
                CultureId = IdOf((string?)e.Attribute("culture")),
                KingdomId = IdOf((string?)e.Attribute("super_faction")),
                IsBandit = IsTrue(e, "is_bandit"),
                IsMinor = IsTrue(e, "is_minor_faction"),
                LeaderTemplateIds = e.Elements("minor_faction_character_templates").Elements("template")
                    .Select(t => IdOf((string?)t.Attribute("id"))).Where(i => i.Length > 0).ToList(),
            };
        }
        return rows.Values.ToList();
    }

    private static Dictionary<string, KingdomRow> ReadKingdoms()
    {
        var sandBox = RequireSandBox();
        var vanilla = Transform(LoadDocument(Path.Combine(sandBox, "spkingdoms.xml")), Path.Combine(ModuleDataPath, "spkingdoms.xslt"));
        var taom = XDocument.Load(Path.Combine(ModuleDataPath, "taom_spkingdoms.xml"));
        var rows = new Dictionary<string, KingdomRow>(StringComparer.Ordinal);
        foreach (var e in vanilla.Descendants("Kingdom").Concat(taom.Descendants("Kingdom")))
        {
            var id = (string?)e.Attribute("id");
            if (!string.IsNullOrEmpty(id))
                rows[id!] = new KingdomRow { Id = id!, CultureId = IdOf((string?)e.Attribute("culture")) };
        }
        return rows;
    }

    /// <summary>
    /// The culture of every troop and leader template the minor clans use, as the game loads them: the vanilla
    /// <c>spnpccharacters.xml</c> troops and <c>spspecialcharacters.xml</c> leader templates, run through TAOM's
    /// <c>lords.xslt</c>, which sees the accumulated NPCCharacters document. Only the clans' own ids are fed in,
    /// which is enough because the stylesheet matches by id.
    /// </summary>
    private static Dictionary<string, string> LoadClanNpcCultures() =>
        ClanNpcsCache.Value.Transformed.Descendants("NPCCharacter")
            .Where(e => !string.IsNullOrEmpty((string?)e.Attribute("id")))
            .ToDictionary(e => (string)e.Attribute("id")!, e => IdOf((string?)e.Attribute("culture")), StringComparer.Ordinal);

    /// <summary>The transformed clan NPCs, and the same elements as the game files hold them.</summary>
    private static (XDocument Transformed, Dictionary<string, XElement> Vanilla) ReadClanNpcs()
    {
        var sandBox = RequireSandBox();
        var core = InstalledModuleData("SandBoxCore", "spnpccharacters.xml");
        if (core == null)
            Assert.Inconclusive("The Bannerlord SandBoxCore module is not installed here (set BANNERLORD_GAME_DIR).");

        var clanIds = IntendedMinorClanSides.Select(r => r.ClanId).ToArray();
        bool Wanted(string id) => clanIds.Any(c =>
            id.StartsWith(c + "_tier_", StringComparison.Ordinal) || id.StartsWith("spc_" + c + "_leader_", StringComparison.Ordinal));

        var merged = new XElement("NPCCharacters");
        var vanilla = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var file in new[] { Path.Combine(core!, "spnpccharacters.xml"), Path.Combine(sandBox, "spspecialcharacters.xml") })
            foreach (var e in LoadDocument(file).Descendants("NPCCharacter"))
            {
                var id = (string?)e.Attribute("id") ?? "";
                if (!Wanted(id))
                    continue;
                merged.Add(new XElement(e));
                vanilla[id] = e;
            }

        return (Transform(new XDocument(merged), Path.Combine(ModuleDataPath, "lords.xslt")), vanilla);
    }

    private static List<ClanRow> MinorClans(List<ClanRow> clans) =>
        clans.Where(c => c.IsMinor && !c.IsBandit && c.Id != "player_faction").ToList();

    private static List<ClanRow> KingdomClans(List<ClanRow> clans) =>
        clans.Where(c => !c.IsBandit && c.KingdomId.Length > 0).ToList();

    private static Dictionary<string, string> ShippedAlignments()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        return new AlignmentConfigProvider(pathService, Substitute.For<IModLogger>()).LoadAlignments();
    }

    private static AlignmentService ShippedService()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        return new AlignmentService(
            new AlignmentConfigProvider(pathService, Substitute.For<IModLogger>()),
            Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void ShippedClans_AreFound_SoTheGuardsAreMeaningful()
    {
        var clans = LoadClans();

        Assert.IsTrue(KingdomClans(clans).Count >= 100,
            $"Found {KingdomClans(clans).Count} kingdom clans; a clan source went missing, so the guards below would pass vacuously.");
    }

    [TestMethod]
    public void EveryMinorClan_HasAnIntendedSideRow()
    {
        var actual = MinorClans(LoadClans()).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToList();
        var intended = IntendedMinorClanSides.Select(r => r.ClanId).OrderBy(i => i, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(intended, actual,
            "The minor clans the game loads differ from the intended-side table in this test. A new or removed minor clan " +
            "needs a deliberate side: add its row to IntendedMinorClanSides and to execution/alignment.json (free or evil; " +
            "leave it unlisted only when Neutral is intended).");
    }

    [TestMethod]
    public void MinorClan_ResolvesItsIntendedSide()
    {
        // The Grey Company and the Corsair Blades have no row: they pass only on their retagged cultures (arthedain, Free;
        // umbar, Neutral). Their vanilla cultures were empire (Dunland, Evil) and vlandia (Rohan, Free).
        var clans = MinorClans(LoadClans()).ToDictionary(c => c.Id, StringComparer.Ordinal);
        var service = ShippedService();

        var wrong = new List<string>();
        foreach (var (clanId, taomName, intended) in IntendedMinorClanSides)
        {
            Assert.IsTrue(clans.TryGetValue(clanId, out var clan), $"Minor clan '{clanId}' ({taomName}) is not loaded.");
            var actual = service.ResolveSide(clan!.Id, clan.CultureId);
            if (actual != intended)
                wrong.Add($"{taomName} ('{clanId}', culture '{clan.CultureId}') resolves {actual}, intended {intended}");
        }

        Assert.AreEqual(0, wrong.Count,
            "Minor clans off their intended side on their own id and culture: " + string.Join("; ", wrong));
    }

    [TestMethod]
    public void EveryLeaderTemplateAMinorClanNames_IsAnNpcCharacter()
    {
        var npcs = LoadClanNpcCultures();

        var missing = new List<string>();
        foreach (var clan in MinorClans(LoadClans()))
        {
            if (clan.LeaderTemplateIds.Count == 0)
                missing.Add($"{clan.Id} names no minor_faction_character_templates");
            foreach (var id in clan.LeaderTemplateIds.Where(i => !npcs.ContainsKey(i)))
                missing.Add($"{clan.Id} -> {id}");
        }

        Assert.AreEqual(0, missing.Count,
            "A minor clan's leader template must resolve to an NPCCharacter after the transforms (the engine reads it " +
            "when the clan spawns its leaders): " + string.Join("; ", missing));
    }

    [TestMethod]
    public void MinorClanTroopsAndLeaders_AreNeverOpposedToTheClansSide()
    {
        // A hired company's own troops must not desert under an employer of the clan's side: AlignmentDesertion sheds a
        // troop whose culture side is Free or Evil and differs from the owner's. So each troop and each leader template
        // reads the clan's intended side or Neutral, never the opposite one.
        var npcs = LoadClanNpcCultures();
        var clans = MinorClans(LoadClans()).ToDictionary(c => c.Id, StringComparer.Ordinal);
        var service = ShippedService();

        var wrong = new List<string>();
        foreach (var (clanId, taomName, intended) in IntendedMinorClanSides)
        {
            var troopIds = npcs.Keys.Where(i => i.StartsWith(clanId + "_tier_", StringComparison.Ordinal)).ToList();
            Assert.IsTrue(troopIds.Count > 0, $"{taomName} ('{clanId}') has no troops in the transformed NPCCharacters.");
            var leaderIds = clans[clanId].LeaderTemplateIds;

            foreach (var id in troopIds.Concat(leaderIds))
            {
                var culture = npcs[id];
                var side = service.GetCultureSide(culture);
                if (side != intended && side != FactionSide.Neutral)
                    wrong.Add($"{id} (culture '{culture}') reads {side}, {taomName} is {intended}");
            }
        }

        Assert.AreEqual(0, wrong.Count,
            "Troops and leader templates opposed to their clan's intended side: " + string.Join("; ", wrong));
    }

    /// <summary>The element with its own attributes in name order, so the clone's id and culture position does not matter.</summary>
    private static XElement SortedAttributes(XElement e) =>
        new XElement(e.Name, e.Attributes().OrderBy(a => a.Name.LocalName, StringComparer.Ordinal), e.Nodes());

    [TestMethod]
    public void LeaderClones_AreTheOriginalsWithOnlyTheIdAndCultureChanged()
    {
        // The clones must look and behave like the templates they replace (face, equipment, skills, traits, name keys),
        // and the originals, which Dunland and Rohan list as lord and rebellion templates, must stay exactly vanilla.
        var (transformedDocument, vanilla) = ClanNpcsCache.Value;
        var transformed = transformedDocument.Descendants("NPCCharacter")
            .ToDictionary(e => (string)e.Attribute("id")!, StringComparer.Ordinal);
        var expectedCulture = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["spc_legion_of_the_betrayed_leader_"] = "arthedain",
            ["spc_company_of_the_boar_leader_"] = "umbar",
            ["spc_eleftheroi_leader_"] = "sturgia",
            ["spc_brotherhood_of_woods_leader_"] = "empire",
        };

        var originals = vanilla.Keys.Where(id => expectedCulture.Keys.Any(p => id.StartsWith(p, StringComparison.Ordinal))).ToList();
        Assert.AreEqual(16, originals.Count, "Four leader templates for each of the four cloned clans.");

        foreach (var id in originals)
        {
            Assert.IsTrue(XNode.DeepEquals(vanilla[id], transformed[id]), $"The original '{id}' must come out of the transforms unchanged.");

            Assert.IsTrue(transformed.TryGetValue("taom_" + id, out var clone), $"No clone 'taom_{id}'.");
            var prefix = expectedCulture.Keys.First(p => id.StartsWith(p, StringComparison.Ordinal));
            Assert.AreEqual(expectedCulture[prefix], IdOf((string?)clone!.Attribute("culture")), $"Culture of 'taom_{id}'.");

            var expected = new XElement(vanilla[id]);
            expected.SetAttributeValue("id", "taom_" + id);
            expected.SetAttributeValue("culture", (string?)clone.Attribute("culture"));
            Assert.IsTrue(XNode.DeepEquals(SortedAttributes(expected), SortedAttributes(clone)),
                $"'taom_{id}' differs from '{id}' by more than its id and culture.");
        }
    }

    [TestMethod]
    public void TheLoadMigrationsTargets_AreWhatTheShippedDataSays()
    {
        // MinorClanMigration brings an old save to these values, so they must be the ones a new campaign loads.
        var clans = MinorClans(LoadClans()).ToDictionary(c => c.Id, StringComparer.Ordinal);
        var npcs = LoadClanNpcCultures();

        foreach (var pair in MinorClanMigrationService.ClanCultures)
            Assert.AreEqual(pair.Value, clans[pair.Key].CultureId, $"The migration sets '{pair.Key}' to '{pair.Value}', the shipped data says otherwise.");

        foreach (var clanId in MinorClanMigrationService.ClanIds)
            foreach (var template in clans[clanId].LeaderTemplateIds)
            {
                StringAssert.StartsWith(template, "taom_spc_", $"'{clanId}' names '{template}', not a TAOM clone the migration moves an old save to.");
                Assert.IsTrue(npcs.ContainsKey(template), $"The clone '{template}' is not defined after the transforms.");
                Assert.IsTrue(npcs.ContainsKey(template.Substring("taom_".Length)), $"The original of '{template}' is gone.");
            }
    }

    /// <summary>Clans whose culture was retagged in an earlier release, so every save already holds it; the migration only keeps their leader templates current.</summary>
    private static readonly string[] CultureRetaggedBeforeTheMigration = { "brotherhood_of_woods" };

    [TestMethod]
    public void EveryRetaggedMinorClan_HasAMigrationEntry()
    {
        // The reverse of the pin above: a clan the transforms retag (a culture other than vanilla's, or leader templates
        // pointing at taom_ clones) must be in the migration, or an old save keeps the vanilla values for good.
        var sandBox = RequireSandBox();
        var vanilla = LoadDocument(Path.Combine(sandBox, "spclans.xml")).Descendants("Faction")
            .Where(e => !string.IsNullOrEmpty((string?)e.Attribute("id")))
            .ToDictionary(e => (string)e.Attribute("id")!, e => IdOf((string?)e.Attribute("culture")), StringComparer.Ordinal);

        foreach (var clan in MinorClans(LoadClans()))
        {
            var retaggedCulture = vanilla.TryGetValue(clan.Id, out var vanillaCulture) && vanillaCulture != clan.CultureId;
            var clonedTemplates = clan.LeaderTemplateIds.Any(t => t.StartsWith("taom_", StringComparison.Ordinal));

            if (retaggedCulture && !CultureRetaggedBeforeTheMigration.Contains(clan.Id))
                Assert.IsTrue(MinorClanMigrationService.ClanCultures.ContainsKey(clan.Id),
                    $"'{clan.Id}' culture changed from '{vanillaCulture}' to '{clan.CultureId}' but MinorClanMigrationService.ClanCultures has no entry.");
            if (retaggedCulture || clonedTemplates)
                Assert.IsTrue(MinorClanMigrationService.ClanIds.Contains(clan.Id),
                    $"'{clan.Id}' is retagged or points at taom_ clones but MinorClanMigrationService.ClanIds does not list it.");
        }
    }

    [TestMethod]
    public void EveryKingdomClan_ResolvesItsKingdomsSide()
    {
        var clans = KingdomClans(LoadClans());
        var kingdoms = LoadKingdoms();
        var service = ShippedService();

        foreach (var clan in clans)
        {
            Assert.IsTrue(kingdoms.TryGetValue(clan.KingdomId, out var kingdom),
                $"Clan '{clan.Id}' names kingdom '{clan.KingdomId}', which no loaded kingdom file defines.");

            // A clan in a kingdom is the kingdom's map faction: its pair is the kingdom's id and the kingdom's culture.
            var side = service.ResolveSide(kingdom!.Id, kingdom.CultureId);
            if (NeutralRealmIds.Contains(kingdom.Id))
                Assert.AreEqual(FactionSide.Neutral, side, $"Realm '{kingdom.Id}' is meant to be Neutral (clan '{clan.Id}').");
            else
                Assert.AreNotEqual(FactionSide.Neutral, side,
                    $"Kingdom '{kingdom.Id}' (culture '{kingdom.CultureId}', clan '{clan.Id}') resolves Neutral: list the kingdom or its culture in execution/alignment.json.");
        }
    }

    [TestMethod]
    public void EveryKingdomClan_OnItsOwnPair_ResolvesItsKingdomsSide()
    {
        // A clan whose kingdom is destroyed or that the kill destroyed is read on its own pair (its id and its
        // own culture), so a victim reads the same side it read while the kingdom still stood.
        var clans = KingdomClans(LoadClans());
        var kingdoms = LoadKingdoms();
        var service = ShippedService();

        var mismatches = new List<string>();
        foreach (var clan in clans)
        {
            Assert.IsTrue(kingdoms.TryGetValue(clan.KingdomId, out var kingdom),
                $"Clan '{clan.Id}' names kingdom '{clan.KingdomId}', which no loaded kingdom file defines.");
            var kingdomSide = service.ResolveSide(kingdom!.Id, kingdom.CultureId);
            var ownSide = service.ResolveSide(clan.Id, clan.CultureId);
            if (ownSide != kingdomSide)
                mismatches.Add($"{clan.Id} (culture '{clan.CultureId}') resolves {ownSide} but its kingdom '{kingdom.Id}' resolves {kingdomSide}");
        }

        Assert.AreEqual(0, mismatches.Count,
            "A kingdom clan on its own pair must read its kingdom's side: " + string.Join("; ", mismatches));
    }

    [TestMethod]
    public void PlayerFaction_IsNotListed()
    {
        Assert.IsFalse(ShippedAlignments().Keys.Contains("player_faction", StringComparer.OrdinalIgnoreCase),
            "player_faction is the player's own clan; a row would side every player clan, and a player kingdom resolves through its culture.");
    }

    [TestMethod]
    public void ListedClanEntries_AreFreeOrEvil()
    {
        var clanIds = LoadClans().Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var neutralClanRows = ShippedAlignments()
            .Where(kv => clanIds.Contains(kv.Key))
            .Where(kv => !string.Equals(kv.Value, "free", StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(kv.Value, "evil", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();

        Assert.AreEqual(0, neutralClanRows.Count,
            "A neutral clan row is a no-op (ResolveSide falls through to the culture): delete it, or list free or evil. " +
            string.Join(", ", neutralClanRows));
    }

    private static HashSet<string> LoadCultureIds()
    {
        var sandBox = RequireSandBox();
        var core = InstalledModuleData("SandBoxCore", "spcultures.xml");
        var cultureFiles = new[]
        {
            core == null ? null : Path.Combine(core, "spcultures.xml"),
            Path.Combine(sandBox, "spcultures.xml"),
            Path.Combine(ModuleDataPath, "taom_spcultures.xml"),
        };
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in cultureFiles.Where(f => f != null && File.Exists(f)))
            foreach (var e in XDocument.Load(file!).Descendants("Culture"))
                if (!string.IsNullOrEmpty((string?)e.Attribute("id")))
                    cultures.Add((string)e.Attribute("id")!);
        return cultures;
    }

    [TestMethod]
    public void EveryAlignmentKey_IsAKingdomCultureOrClanId()
    {
        // A typo in a row (a misspelt clan or kingdom id) is a silent no-op: the id never matches, so the entity falls
        // through to its culture. Keys are checked against the ids the game loads.
        var known = LoadCultureIds();
        known.UnionWith(LoadKingdoms().Keys);
        known.UnionWith(LoadClans().Select(c => c.Id));

        var unknown = ShippedAlignments().Keys.Where(k => !known.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count,
            "These execution/alignment.json keys name no loaded culture, kingdom or clan (a typo?): " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void NoRowIsListedForAClanIdThatIsAlsoACultureOrKingdomId()
    {
        var shared = LoadCultureIds();
        foreach (var id in LoadKingdoms().Keys)
            shared.Add(id);

        var clanIds = LoadClans().Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clashes = ShippedAlignments().Keys.Where(k => clanIds.Contains(k) && shared.Contains(k)).ToList();

        Assert.AreEqual(0, clashes.Count,
            "The alignment table is one id namespace for kingdoms, cultures and clans. A bandit clan shares its id with " +
            "its culture, so a row for that clan would side the culture too; these listed ids are both: " + string.Join(", ", clashes));
    }
}
