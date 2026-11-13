using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every culture that declares <c>notable_templates</c> must offer at least one template of each
/// gender, and every template reference must resolve.
///
/// <para>
/// v1.5.0's Advanced Starting Options Trader start does
/// <c>kingdom.Culture.NotableTemplates.Where(t =&gt; t.IsFemale == Hero.MainHero.IsFemale)</c> and then
/// indexes the result with <c>MBFastRandom.Next(0, list.Count)</c>. <c>Next(0, 0)</c> returns 0, so
/// an empty list indexes <c>list[0]</c> and throws <c>ArgumentOutOfRangeException</c>. A culture
/// with no template of the player's gender is therefore a hard crash at campaign start, not a
/// cosmetic gap.
/// </para>
///
/// <para>
/// This shipped: all 16 TAOM cultures with notable templates had zero female entries (vanilla ships
/// 4 to 5 per culture) until the v1.5.0 bump audit. Nothing caught it because it is pure data, the
/// references all resolved, and no engine code exercised the gender filter before v1.5.0.
/// </para>
///
/// <para>
/// Reads the repo's own ModuleData, so it needs no game install and no engine assemblies.
/// </para>
/// </summary>
[TestClass]
public class NotableTemplateGenderTests
{
    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    // id -> is_female, across every NPCCharacter TAOM defines.
    private static Dictionary<string, bool> CharacterGenderIndex()
    {
        var index = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        // ALL of ModuleData, not just characters/: notable_templates also reference wanderers, which
        // live in taom_wanderers.xml at the ModuleData root. Languages/ is excluded because those are
        // translation files, not definitions.
        foreach (var file in Directory.GetFiles(ModuleData(), "*.xml", SearchOption.AllDirectories))
        {
            if (file.IndexOf($"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}",
                             StringComparison.OrdinalIgnoreCase) >= 0) continue;

            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch { continue; }

            foreach (var ch in doc.Descendants("NPCCharacter"))
            {
                var id = (string)ch.Attribute("id");
                if (string.IsNullOrEmpty(id) || index.ContainsKey(id)) continue;
                index[id] = string.Equals((string)ch.Attribute("is_female"), "true",
                                          StringComparison.OrdinalIgnoreCase);
            }
        }
        return index;
    }

    private static IEnumerable<XElement> CulturesWithNotableTemplates()
    {
        var path = Path.Combine(ModuleData(), "taom_spcultures.xml");
        return XDocument.Load(path)
                        .Descendants("Culture")
                        .Where(c => c.Element("notable_templates") != null);
    }

    [TestMethod]
    public void EveryCulture_OffersAtLeastOneNotableTemplateOfEachGender()
    {
        var index = CharacterGenderIndex();
        Assert.IsTrue(index.Count > 0, "No NPCCharacter definitions were discovered.");

        var noFemale = new List<string>();
        var noMale = new List<string>();
        var cultureCount = 0;

        foreach (var culture in CulturesWithNotableTemplates())
        {
            cultureCount++;
            var id = (string)culture.Attribute("id");
            var refs = culture.Element("notable_templates")!
                              .Elements("template")
                              .Select(t => ((string)t.Attribute("name") ?? string.Empty)
                                           .Replace("NPCCharacter.", string.Empty))
                              .Where(n => n.Length > 0)
                              .ToList();

            var known = refs.Where(index.ContainsKey).ToList();
            if (!known.Any(n => index[n])) noFemale.Add($"{id} ({refs.Count} templates)");
            if (!known.Any(n => !index[n])) noMale.Add($"{id} ({refs.Count} templates)");
        }

        Assert.IsTrue(cultureCount > 0, "No cultures with notable_templates were found.");

        Assert.AreEqual(0, noFemale.Count,
            "These cultures have NO female notable template. v1.5.0's Advanced Starting Options "
            + "Trader start filters notable templates by the player's gender and indexes the result "
            + "unguarded, so a female player crashes at campaign start:"
            + Environment.NewLine + string.Join(Environment.NewLine, noFemale));

        Assert.AreEqual(0, noMale.Count,
            "These cultures have NO male notable template, which crashes a male player the same way:"
            + Environment.NewLine + string.Join(Environment.NewLine, noMale));
    }

    [TestMethod]
    public void EveryNotableTemplateReference_ResolvesToADefinedCharacter()
    {
        var index = CharacterGenderIndex();
        var unresolved = new List<string>();

        foreach (var culture in CulturesWithNotableTemplates())
        {
            var id = (string)culture.Attribute("id");
            foreach (var t in culture.Element("notable_templates")!.Elements("template"))
            {
                var name = ((string)t.Attribute("name") ?? string.Empty)
                           .Replace("NPCCharacter.", string.Empty);
                if (name.Length > 0 && !index.ContainsKey(name))
                    unresolved.Add($"{id} -> {name}");
            }
        }

        Assert.AreEqual(0, unresolved.Count,
            "notable_templates entries pointing at characters TAOM does not define:"
            + Environment.NewLine + string.Join(Environment.NewLine, unresolved));
    }

    // ---- the renamed vanilla cultures, which live in spcultures.xslt, not taom_spcultures.xml ----

    // spcultures.xslt REPLACES vanilla's notable_templates wholesale for the six cultures TAOM
    // renames, so vanilla's own female notables are gone unless the stylesheet adds one back. The
    // XML-only checks above cannot see this: it is an emitted list, not a declared one. Codex caught
    // exactly this gap after the first fix pass, which is why the gate now asserts on the OUTPUT.
    //
    // A stub input suffices because each override emits its own complete list and never copies the
    // source's, so the test needs no game install.
    private static XDocument RenamedCultureStub()
    {
        var root = new XElement("SPCultures");
        foreach (var id in new[] { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" })
            root.Add(new XElement("Culture", new XAttribute("id", id)));
        return new XDocument(root);
    }

    [TestMethod]
    public void RenamedVanillaCultures_AlsoOfferAFemaleNotableTemplate()
    {
        var transform = CompiledXslt.Get(Path.Combine(ModuleData(), "spcultures.xslt"), XsltLoadStyle.FrameworkDefaults);

        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(RenamedCultureStub().CreateReader(), null, writer);

        var index = CharacterGenderIndex();
        var noFemale = new List<string>();
        var unresolved = new List<string>();
        var seen = 0;

        foreach (var culture in output.Descendants("Culture"))
        {
            var templates = culture.Element("notable_templates");
            if (templates == null) continue;
            seen++;

            var id = (string)culture.Attribute("id");
            var refs = templates.Elements("template")
                                .Select(t => ((string)t.Attribute("name") ?? string.Empty)
                                             .Replace("NPCCharacter.", string.Empty))
                                .Where(n => n.Length > 0)
                                .ToList();

            unresolved.AddRange(refs.Where(n => !index.ContainsKey(n)).Select(n => $"{id} -> {n}"));
            if (!refs.Any(n => index.TryGetValue(n, out var female) && female))
                noFemale.Add($"{id} ({refs.Count} templates)");
        }

        Assert.AreEqual(6, seen,
            "Expected all six renamed vanilla cultures to emit a notable_templates list.");

        Assert.AreEqual(0, noFemale.Count,
            "These XSLT-renamed cultures emit NO female notable template, so a female player on the "
            + "Advanced Starting Options Trader start crashes in any of their towns:"
            + Environment.NewLine + string.Join(Environment.NewLine, noFemale));

        Assert.AreEqual(0, unresolved.Count,
            "XSLT-emitted notable_templates entries pointing at undefined characters:"
            + Environment.NewLine + string.Join(Environment.NewLine, unresolved));
    }

    // ---- the two-layer registration: a template NPC is inert until a notable_templates block lists it ----

    // Occupations of the notable and headman template NPCs in characters/npcs_*.xml. Lord templates
    // (occupation Lord) are not notables and are not listed in notable_templates. A Preacher is checked too, but no
    // spawn reaches one: DefaultNotableSpawnModel gives it a target count of 0 (TaomNotableSpawnModel returns that 0)
    // and NotablesCampaignBehavior never creates one, so for a Preacher the failure below is a registration gap only.
    private static readonly string[] NotableOccupations =
        { "Merchant", "Artisan", "Preacher", "GangLeader", "RuralNotable", "Headman" };

    private static readonly Regex CultureMatch = new(@"Culture\[@id='([^']+)'\]", RegexOptions.Compiled);

    // The culture a notable_templates block belongs to: the id of its Culture element in the declared
    // file, or the Culture[@id='x'] an xsl:template in the stylesheet matches.
    private static string BlockCulture(XElement block)
    {
        var culture = block.Ancestors().FirstOrDefault(e => e.Name.LocalName == "Culture");
        if (culture != null)
            return (string)culture.Attribute("id") ?? string.Empty;

        var match = block.Ancestors().Select(e => (string)e.Attribute("match")).Where(m => m != null)
                         .Select(m => CultureMatch.Match(m)).FirstOrDefault(m => m.Success);
        return match?.Groups[1].Value ?? string.Empty;
    }

    // "culture|id" for each <template name="NPCCharacter.id"/> inside that culture's notable_templates
    // block, in the declared file and in the stylesheet that emits the renamed vanilla cultures' lists.
    // In the stylesheet the literal result element has no namespace, which sets it apart from xsl:template.
    private static HashSet<string> ReferencedNotableTemplates(IEnumerable<XDocument> docs)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in docs)
        {
            foreach (var block in doc.Descendants().Where(e => e.Name.LocalName == "notable_templates"))
                foreach (var t in block.Descendants().Where(e => e.Name.LocalName == "template" && e.Name.NamespaceName.Length == 0))
                    ids.Add(BlockCulture(block) + "|" + ((string)t.Attribute("name") ?? string.Empty).Replace("NPCCharacter.", string.Empty));
        }

        return ids;
    }

    private static HashSet<string> ReferencedNotableTemplateIds() =>
        ReferencedNotableTemplates(new[] { "taom_spcultures.xml", "spcultures.xslt" }
            .Select(file => XDocument.Load(Path.Combine(ModuleData(), file))));

    private static List<string> UnregisteredNotableTemplates(IEnumerable<XElement> characters, ISet<string> referenced) =>
        characters
            .Where(c => string.Equals((string)c.Attribute("is_template"), "true", StringComparison.OrdinalIgnoreCase))
            .Where(c => NotableOccupations.Contains((string)c.Attribute("occupation") ?? string.Empty))
            .Where(c => !referenced.Contains(
                ((string)c.Attribute("culture") ?? string.Empty).Replace("Culture.", string.Empty) + "|" + (string)c.Attribute("id")))
            .Select(c => (string)c.Attribute("id") ?? string.Empty)
            .ToList();

    [TestMethod]
    public void EveryNotableAndHeadmanTemplate_IsListedInANotableTemplatesBlock()
    {
        var characters = Directory.GetFiles(Path.Combine(ModuleData(), "characters"), "npcs_*.xml")
                                  .SelectMany(f => XDocument.Load(f).Descendants("NPCCharacter"))
                                  .ToList();
        Assert.IsTrue(characters.Count(c => (string)c.Attribute("is_template") == "true") > 100,
            "The notable template NPCs were not found in characters/npcs_*.xml.");
        var referenced = ReferencedNotableTemplateIds();
        Assert.IsTrue(referenced.Count > 100, "No notable_templates references were read.");

        var missing = UnregisteredNotableTemplates(characters, referenced);

        Assert.AreEqual(0, missing.Count,
            "These is_template notable or headman NPCs are in no notable_templates block of their own culture "
            + "(taom_spcultures.xml or spcultures.xslt), so each stays out of its own culture's pool; it may spawn only under the culture that lists it:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [TestMethod]
    public void UnregisteredNotableTemplates_FlagsAnUnlistedNotableOnly()
    {
        var characters = XElement.Parse(
            "<NPCCharacters>"
            + "<NPCCharacter id=\"listed\" culture=\"Culture.a\" is_template=\"true\" occupation=\"Merchant\"/>"
            + "<NPCCharacter id=\"unlisted_notable\" culture=\"Culture.a\" is_template=\"true\" occupation=\"RuralNotable\"/>"
            + "<NPCCharacter id=\"unlisted_headman\" culture=\"Culture.a\" is_template=\"true\" occupation=\"Headman\"/>"
            + "<NPCCharacter id=\"unlisted_lord\" culture=\"Culture.a\" is_template=\"true\" occupation=\"Lord\"/>"
            + "<NPCCharacter id=\"not_a_template\" culture=\"Culture.a\" occupation=\"Merchant\"/>"
            + "</NPCCharacters>").Elements("NPCCharacter");

        var missing = UnregisteredNotableTemplates(characters, new HashSet<string> { "a|listed" });

        CollectionAssert.AreEqual(new[] { "unlisted_notable", "unlisted_headman" }, missing);
    }

    [TestMethod]
    public void UnregisteredNotableTemplates_FlagsATemplateListedOnlyUnderAnotherCulture()
    {
        var characters = XElement.Parse(
            "<NPCCharacters><NPCCharacter id=\"x\" culture=\"Culture.a\" is_template=\"true\" occupation=\"Merchant\"/></NPCCharacters>")
            .Elements("NPCCharacter");

        Assert.AreEqual(0, UnregisteredNotableTemplates(characters, new HashSet<string> { "a|x" }).Count);
        CollectionAssert.AreEqual(new[] { "x" }, UnregisteredNotableTemplates(characters, new HashSet<string> { "b|x" }));
    }

    [TestMethod]
    public void ReferencedNotableTemplates_KeyEachTemplateByItsBlocksCulture()
    {
        var plain = XDocument.Parse(
            "<Cultures><Culture id=\"a\"><notable_templates><template name=\"NPCCharacter.x\"/></notable_templates></Culture></Cultures>");
        var xslt = XDocument.Parse(
            "<xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" version=\"1.0\">"
            + "<xsl:template match=\"Culture[@id='b']\"><xsl:copy><notable_templates><template name=\"NPCCharacter.y\"/></notable_templates></xsl:copy></xsl:template>"
            + "</xsl:stylesheet>");

        var keys = ReferencedNotableTemplates(new[] { plain, xslt });

        CollectionAssert.AreEquivalent(new[] { "a|x", "b|y" }, keys.ToList());
    }

    // ---- the Trader start also indexes the template's battle and civilian sets ----

    // id -> NPCCharacter element, across the same ModuleData files as CharacterGenderIndex.
    private static Dictionary<string, XElement> CharacterElementIndex()
    {
        var index = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(ModuleData(), "*.xml", SearchOption.AllDirectories))
        {
            if (file.IndexOf($"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}",
                             StringComparison.OrdinalIgnoreCase) >= 0) continue;

            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch { continue; }

            foreach (var ch in doc.Descendants("NPCCharacter"))
            {
                var id = (string?)ch.Attribute("id");
                if (!string.IsNullOrEmpty(id) && !index.ContainsKey(id)) index[id] = ch;
            }
        }

        return index;
    }

    // How BasicCharacterObject.Deserialize and MBEquipmentRoster.InitEquipment type a set: equipmentType first, then
    // civilian="true", else Battle.
    private static string SetType(XElement node)
    {
        var type = (string?)node.Attribute("equipmentType");
        if (type != null) return type;
        return string.Equals((string?)node.Attribute("civilian"), "true", StringComparison.OrdinalIgnoreCase) ? "Civilian" : "Battle";
    }

    // The battle and civilian sets the engine builds for a character: each inline EquipmentRoster is one set and each
    // EquipmentSet reference adds one of its type. A reference is counted without opening the roster it names, so a
    // referenced roster that holds no set of that type is not seen here.
    private static (int Battle, int Civilian) RosterSetCounts(XElement character)
    {
        int battle = 0, civilian = 0;
        foreach (var equipments in character.Elements().Where(e => e.Name.LocalName == "Equipments" || e.Name.LocalName == "equipments"))
            foreach (var set in equipments.Elements().Where(e => e.Name.LocalName is "EquipmentRoster" or "equipmentRoster" or "EquipmentSet" or "equipmentSet"))
            {
                var type = SetType(set);
                if (type == "Battle") battle++;
                else if (type == "Civilian") civilian++;
            }

        return (battle, civilian);
    }

    [TestMethod]
    public void RosterSetCounts_ReadsInlineRostersAndEquipmentSetReferences()
    {
        XElement Character(string equipments) => XElement.Parse("<NPCCharacter id=\"c\">" + equipments + "</NPCCharacter>");

        Assert.AreEqual((1, 1), RosterSetCounts(Character("<Equipments><EquipmentRoster civilian=\"true\"/><EquipmentRoster/></Equipments>")));
        Assert.AreEqual((0, 1), RosterSetCounts(Character("<Equipments><EquipmentRoster civilian=\"true\"/></Equipments>")));
        Assert.AreEqual((1, 0), RosterSetCounts(Character("<Equipments><EquipmentRoster civilian=\"false\"/></Equipments>")));
        Assert.AreEqual((1, 1), RosterSetCounts(Character("<Equipments><EquipmentSet id=\"b\"/><EquipmentSet id=\"c\" equipmentType=\"Civilian\"/></Equipments>")));
        Assert.AreEqual((0, 1), RosterSetCounts(Character("<Equipments><EquipmentRoster equipmentType=\"Civilian\" civilian=\"false\"/></Equipments>")));
        Assert.AreEqual((0, 0), RosterSetCounts(Character("<Equipments><EquipmentRoster equipmentType=\"Stealth\"/></Equipments>")));
        Assert.AreEqual((0, 0), RosterSetCounts(Character("")));
    }

    [TestMethod]
    public void EveryListedNotableTemplate_HasABattleAndACivilianSet()
    {
        var characters = CharacterElementIndex();
        var listed = ReferencedNotableTemplateIds()
            .Select(key => key.Substring(key.IndexOf('|') + 1))
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.IsTrue(listed.Count > 100, "No notable_templates references were read.");

        var noBattle = new List<string>();
        var noCivilian = new List<string>();
        var counted = 0;
        foreach (var id in listed)
        {
            // An id that resolves to no character is reported by EveryNotableTemplateReference_ResolvesToADefinedCharacter.
            if (!characters.TryGetValue(id, out var character)) continue;
            counted++;
            var (battle, civilian) = RosterSetCounts(character);
            if (battle == 0) noBattle.Add(id);
            if (civilian == 0) noCivilian.Add(id);
        }

        Assert.IsTrue(counted > 100, "Too few listed templates resolved to a character (" + counted + ").");
        Assert.AreEqual(0, noBattle.Count,
            "These listed notable templates have no battle equipment set. The Advanced Starting Options Trader start "
            + "indexes list[Next(0, Count)] of the template's BattleEquipments, so an empty list throws at campaign start:"
            + Environment.NewLine + string.Join(Environment.NewLine, noBattle));
        Assert.AreEqual(0, noCivilian.Count,
            "These listed notable templates have no civilian equipment set, which throws the same way on CivilianEquipments:"
            + Environment.NewLine + string.Join(Environment.NewLine, noCivilian));
    }
}
