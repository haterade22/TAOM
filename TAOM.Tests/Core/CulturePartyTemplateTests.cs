using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Config validation for every culture party-template binding TAOM ships.
///
/// Root cause these pin: nine town-owning TAOM cultures resolved their party templates to vanilla
/// Calradian ones, so Imperial infantrymen patrolled Dunland and Vlandian crossbowmen patrolled
/// Rohan. Two separate mechanisms produced it, and this class covers both.
///
/// <para><b>1. The XSLT identity passthrough.</b> Each <c>Culture[@id='X']</c> block in
/// <c>spcultures.xslt</c> starts with <c>&lt;xsl:apply-templates select="@*"/&gt;</c>, which copies
/// every vanilla attribute, and then overrides the ones it names. An attribute the block never
/// mentions is silently inherited from Calradia. Four of the six retagged cultures never mentioned
/// <c>settlement_patrol_template_level_*</c> at all, so nothing in the file looked wrong.</para>
///
/// <para><b>2. Explicit vanilla bindings</b> in <c>taom_spcultures.xml</c> and the battania block,
/// written as placeholders and never revisited.</para>
///
/// Two engine facts make these crashes rather than merely wrong troops, which is why the checks
/// below assert presence and non-emptiness, not just non-vanilla-ness:
/// <list type="bullet">
/// <item><c>PatrolPartiesCampaignBehavior.SpawnPatrolParty</c> dereferences
/// <c>partyTemplate.ShipHulls</c> with no null guard, so a dropped patrol attribute is an NRE.</item>
/// <item><c>CaravansCampaignBehavior.SpawnCaravan</c> calls <c>GetRandomElementWithPredicate</c> on
/// the caravan list with no empty guard, so an empty list is an NRE on the first caravan.</item>
/// </list>
/// </summary>
[TestClass]
public class CulturePartyTemplateTests
{
    /// <summary>
    /// The six vanilla cultures <c>spcultures.xslt</c> retags. Every one of them owns towns, so
    /// every one of them reaches the patrol, militia, villager, rebel and caravan readers.
    /// </summary>
    private static readonly string[] RetaggedCultures =
        { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" };

    /// <summary>
    /// The party-template attributes <c>CultureObject.Deserialize</c> reads (v1.4.8,
    /// <c>CultureObject.cs:269-280</c>) AND vanilla <c>spcultures.xml</c> declares on a main culture.
    ///
    /// Three engine-read attributes are deliberately absent from this list:
    /// <list type="bullet">
    /// <item><c>bandit_boss_party_template</c> is declared only on bandit cultures, which are
    /// hideout-only and exempt below.</item>
    /// <item><c>fishing_party_template</c> and <c>settlement_patrol_template_coastal</c> are declared
    /// by no vanilla culture. Only NavalDLC's XSLT injects them, only NavalDLC reads them, and the
    /// reader is unreachable in TAOM because no settlement carries a <c>port_posX</c> pair, so
    /// <c>Settlement.HasPort</c> is false everywhere. Tracked as its own issue alongside the parked
    /// NavalTravel work (#120/#296). Widen this list if TAOM ever ships ports.</item>
    /// </list>
    /// </summary>
    private static readonly string[] RequiredPartyTemplateAttributes =
    {
        "default_party_template",
        "villager_party_template",
        "militia_party_template",
        "rebels_party_template",
        "vassal_reward_party_template",
        "settlement_patrol_template_level_1",
        "settlement_patrol_template_level_2",
        "settlement_patrol_template_level_3",
    };

    /// <summary>
    /// The two attributes <c>CultureObject.Deserialize</c> never reads. Caravans come only from the
    /// plural child elements, so an attribute here is dead markup that reads as "caravans are
    /// handled" to the next author.
    /// </summary>
    private static readonly string[] UnreadCaravanAttributes =
        { "caravan_party_template", "elite_caravan_party_template" };

    private const string SentinelPrefix = "PartyTemplate.SENTINEL_";

    // ---------------------------------------------------------------------------------------
    // spcultures.xslt: run the transform, read the output
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void EveryRetaggedCulture_BindsEveryPartyTemplateAttributeToATaomTemplate()
    {
        var taomTemplates = TaomPartyTemplateIds();
        var output = TransformSentinelStub();
        var offenders = new List<string>();

        foreach (var cultureId in RetaggedCultures)
        {
            var culture = OutputCulture(output, cultureId);
            foreach (var attribute in RequiredPartyTemplateAttributes)
            {
                var value = culture.Attribute(attribute)?.Value;

                if (value == null)
                {
                    offenders.Add($"{cultureId}/{attribute}: DROPPED (null template is an NRE in the reader)");
                    continue;
                }

                if (value.StartsWith(SentinelPrefix, StringComparison.Ordinal))
                {
                    offenders.Add($"{cultureId}/{attribute}: UNBOUND (the block never sets it, so the " +
                                  "identity passthrough hands the culture whatever vanilla declared)");
                    continue;
                }

                var id = CultureDataFixture.StripPrefix(value);
                if (!taomTemplates.Contains(id))
                    offenders.Add($"{cultureId}/{attribute}: {id} is not defined in taom_partyTemplates.xml");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Retagged cultures with a party template that does not resolve to TAOM troops:\n  " +
            string.Join("\n  ", offenders) +
            "\n\nFix in Main/_Module/ModuleData/spcultures.xslt. A binding that is merely absent is " +
            "still a defect: the block's <xsl:apply-templates select=\"@*\"/> copies the vanilla " +
            "attribute in, so the culture spawns Calradians with nothing in the file to show for it.");
    }

    [TestMethod]
    public void EveryRetaggedCulture_ReplacesTheVanillaCaravanChildElements()
    {
        var taomTemplates = TaomPartyTemplateIds();
        var output = TransformSentinelStub();
        var offenders = new List<string>();

        foreach (var cultureId in RetaggedCultures)
        {
            var culture = OutputCulture(output, cultureId);
            foreach (var wrapper in new[] { "caravan_party_templates", "elite_caravan_party_templates" })
            {
                var lists = culture.Elements(wrapper).ToList();

                if (lists.Count == 0)
                {
                    offenders.Add($"{cultureId}/{wrapper}: MISSING (an empty caravan list is an NRE in SpawnCaravan)");
                    continue;
                }

                // Deserialize appends every matching child into ONE list rather than overwriting, so
                // a second element does not replace the first, it unions with it. Emitting a TAOM
                // block without also excluding vanilla's from the passthrough filter therefore makes
                // caravans roll Calradian roughly half the time, which is worse than a clean miss.
                if (lists.Count > 1)
                {
                    offenders.Add($"{cultureId}/{wrapper}: {lists.Count} elements. CultureObject.Deserialize " +
                                  "unions them, it does not overwrite. Add the wrapper to this block's " +
                                  "not(self::...) passthrough filter.");
                    continue;
                }

                var ids = lists[0].Elements()
                    .Select(entry => entry.Attribute("id")?.Value)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToList();

                if (ids.Count == 0)
                {
                    offenders.Add($"{cultureId}/{wrapper}: empty (GetRandomElementWithPredicate throws on it)");
                    continue;
                }

                foreach (var value in ids)
                {
                    if (value.StartsWith(SentinelPrefix, StringComparison.Ordinal))
                    {
                        offenders.Add($"{cultureId}/{wrapper}: vanilla's element survived the passthrough. " +
                                      "The block must emit its own AND exclude vanilla's.");
                        continue;
                    }

                    var id = CultureDataFixture.StripPrefix(value);
                    if (!taomTemplates.Contains(id))
                        offenders.Add($"{cultureId}/{wrapper}: {id} is not defined in taom_partyTemplates.xml");
                }
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Retagged cultures whose caravan child elements are wrong:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void EveryRetaggedCulture_DoesNotEmitTheUnreadCaravanAttributes()
    {
        var output = TransformSentinelStub();
        var offenders = new List<string>();

        foreach (var cultureId in RetaggedCultures)
        {
            var culture = OutputCulture(output, cultureId);
            foreach (var attribute in UnreadCaravanAttributes)
            {
                var value = culture.Attribute(attribute)?.Value;
                if (value != null)
                    offenders.Add($"{cultureId}/{attribute} = {value}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Dead caravan ATTRIBUTES emitted by spcultures.xslt:\n  " + string.Join("\n  ", offenders) +
            "\n\nCultureObject.Deserialize reads caravans only from the plural CHILD elements. These " +
            "attributes do nothing, and repointing one at a TAOM id makes the block look fixed while " +
            "the culture keeps rolling Calradian caravans. Delete them.");
    }

    [TestMethod]
    public void EveryRetaggedCulture_StillPassesThroughTheChildElementsItDoesNotOverride()
    {
        var output = TransformSentinelStub();
        var offenders = new List<string>();

        // The caravan work adds two names to each block's not(self::...) filter. A fat-fingered edit
        // there drops a child element the block meant to keep, and nothing else in the suite reads
        // the XSLT's output.
        foreach (var cultureId in RetaggedCultures)
        {
            var culture = OutputCulture(output, cultureId);
            foreach (var child in new[] { "lord_templates", "rebellion_hero_templates", "basic_mercenary_troops" })
            {
                if (!culture.Elements(child).Any())
                    offenders.Add($"{cultureId}/{child}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Child elements dropped by a passthrough filter that should have kept them:\n  " +
            string.Join("\n  ", offenders));
    }

    // ---------------------------------------------------------------------------------------
    // taom_spcultures.xml: plain XML, no transform
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void EveryTaomCulture_ReferencesOnlyTaomAuthoredPartyTemplates()
    {
        var taomTemplates = TaomPartyTemplateIds();
        var offenders = new List<string>();

        foreach (var culture in NativeCultures())
        {
            var cultureId = culture.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(cultureId))
                continue;

            foreach (var reference in PartyTemplateReferences(culture))
            {
                var id = CultureDataFixture.StripPrefix(reference.Value);
                if (!taomTemplates.Contains(id))
                    offenders.Add($"{cultureId}/{reference.Key}: {id}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "TAOM cultures pointing at a party template TAOM does not define, which means it resolves " +
            "to vanilla Calradian troops:\n  " + string.Join("\n  ", offenders) +
            "\n\nFix in Main/_Module/ModuleData/taom_spcultures.xml. Cross-culture sharing is fine " +
            "(Lothlorien uses Rivendell's, Umbar uses Harad's) as long as the target is TAOM-authored.");
    }

    [TestMethod]
    public void EverySettledTaomCulture_DeclaresEveryPartyTemplateAttribute()
    {
        var offenders = new List<string>();

        foreach (var culture in NativeCultures())
        {
            var cultureId = culture.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(cultureId) || CultureDataFixture.HideoutOnlyCultures.Contains(cultureId))
                continue;

            foreach (var attribute in RequiredPartyTemplateAttributes)
            {
                if (culture.Attribute(attribute) == null)
                    offenders.Add($"{cultureId}/{attribute}");
            }

            foreach (var wrapper in new[] { "caravan_party_templates", "elite_caravan_party_templates" })
            {
                if (!culture.Elements(wrapper).Elements().Any())
                    offenders.Add($"{cultureId}/{wrapper} (empty or absent)");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Settled TAOM cultures missing a party-template binding:\n  " + string.Join("\n  ", offenders) +
            "\n\nUnlike the retagged vanilla cultures, a native culture inherits nothing. A missing " +
            "attribute here is a null PartyTemplateObject, and both SpawnPatrolParty and SpawnCaravan " +
            "dereference it without a guard.");
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Runs <c>spcultures.xslt</c> over a synthetic vanilla document whose every party-template
    /// binding is a unique sentinel.
    ///
    /// Reading the <c>&lt;xsl:attribute&gt;</c> elements textually instead cannot see exclusion
    /// filters or child passthrough, and cannot express "a missing attribute is a defect". Running
    /// the transform can. Using a stub rather than the installed <c>SandBoxCore/spcultures.xml</c>
    /// keeps the test deterministic and free of any dependency on a game install.
    /// </summary>
    private static XDocument TransformSentinelStub()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "spcultures.xslt"));

        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(SentinelStub().CreateReader(), null, writer);

        return output;
    }

    private static XDocument SentinelStub()
    {
        var root = new XElement("SPCultures");

        foreach (var cultureId in RetaggedCultures)
        {
            var culture = new XElement("Culture", new XAttribute("id", cultureId));

            foreach (var attribute in RequiredPartyTemplateAttributes)
                culture.Add(new XAttribute(attribute, SentinelPrefix + attribute));

            culture.Add(SentinelCaravanList("caravan_party_templates"));
            culture.Add(SentinelCaravanList("elite_caravan_party_templates"));

            // Children no block should be dropping, so the passthrough filters stay honest.
            foreach (var child in new[] { "lord_templates", "rebellion_hero_templates", "basic_mercenary_troops" })
                culture.Add(new XElement(child));

            root.Add(culture);
        }

        return new XDocument(root);
    }

    private static XElement SentinelCaravanList(string wrapper) =>
        new XElement(wrapper,
            new XElement("caravan_party_template", new XAttribute("id", SentinelPrefix + wrapper)));

    private static XElement OutputCulture(XDocument output, string cultureId)
    {
        var culture = output.Descendants("Culture")
            .FirstOrDefault(element => element.Attribute("id")?.Value == cultureId);

        Assert.IsNotNull(culture,
            $"spcultures.xslt did not emit a Culture element for '{cultureId}'. Either the template " +
            "match was renamed or the block stopped copying the id attribute.");
        return culture;
    }

    /// <summary>
    /// Every party-template reference on a culture: the engine-read attributes plus both caravan
    /// child lists. Keyed by attribute or wrapper name so a failure names the exact binding.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> PartyTemplateReferences(XElement culture)
    {
        foreach (var attribute in culture.Attributes())
        {
            var name = attribute.Name.LocalName;
            if (name.EndsWith("_party_template", StringComparison.Ordinal) ||
                name.StartsWith("settlement_patrol_template_", StringComparison.Ordinal))
            {
                yield return new KeyValuePair<string, string>(name, attribute.Value);
            }
        }

        foreach (var wrapper in new[] { "caravan_party_templates", "elite_caravan_party_templates" })
        {
            foreach (var entry in culture.Elements(wrapper).Elements())
            {
                var value = entry.Attribute("id")?.Value;
                if (!string.IsNullOrEmpty(value))
                    yield return new KeyValuePair<string, string>(wrapper, value);
            }
        }
    }

    private static IEnumerable<XElement> NativeCultures()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_spcultures.xml");
        Assert.IsTrue(File.Exists(path), $"taom_spcultures.xml not found at {path}");
        return XDocument.Load(path).Descendants("Culture").ToList();
    }

    /// <summary>
    /// Every party template id TAOM authors. This is a whitelist on purpose: TAOM redefines no
    /// vanilla party-template id, so "not in this set" is exactly equivalent to "resolves to
    /// Calradian troops". A blacklist of vanilla prefixes would need an exemption for every
    /// intentional cross-culture share and would go stale on the next engine bump.
    /// </summary>
    private static HashSet<string> TaomPartyTemplateIds()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_partyTemplates.xml");
        Assert.IsTrue(File.Exists(path), $"taom_partyTemplates.xml not found at {path}");

        var ids = new HashSet<string>(
            XDocument.Load(path).Descendants("MBPartyTemplate")
                .Select(template => template.Attribute("id")?.Value)
                .Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(ids.Count > 300,
            $"Only {ids.Count} party templates parsed from taom_partyTemplates.xml. The element name " +
            "is probably no longer MBPartyTemplate, which would make every check below vacuously fail.");
        return ids;
    }
}
