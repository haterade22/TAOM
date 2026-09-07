using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// SupplyLines spawns its caravans from its OWN crew templates, not from the AI notable-caravan
/// templates it used to share.
///
/// <para>The coupling it replaces caused #549: `PickCaravanTemplate` returned
/// <c>culture.CaravanPartyTemplates[0]</c>, so when those templates were resized for bandit parity
/// (#543) the player's supply escort went from 20-29 troops to 60-70 and the provisioning cost,
/// which is linear in headcount, went with it. The two templates answer different questions. An AI
/// caravan has to survive a warband alone; a supply caravan is escorted by whatever the player
/// paid for.</para>
///
/// <para>The old fallback was worse than the primary path: a culture with no caravan templates got
/// <c>culture.DefaultPartyTemplate</c>, a LORD party template running to hundreds of troops.</para>
///
/// <para>Sizing: a supply caravan reaches none of
/// <c>DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit</c>'s bonus branches (no
/// <c>LeaderHero</c>, and <c>SupplyCaravanComponent</c> derives from <c>PartyComponent</c> so it is
/// neither <c>IsCaravan</c> nor <c>IsVillager</c>), so its member cap is the flat
/// <c>ExplainedNumber(20f)</c>.</para>
/// </summary>
[TestClass]
public class SupplyCaravanTemplateTests
{
    /// <summary>Vanilla's flat member cap for a party that hits no bonus branch.</summary>
    private const int FlatVanillaCap = 20;

    /// <summary>`SupplyLinesSettingsProvider.MercenaryGuardCount` default.</summary>
    private const int DefaultEscort = 10;

    // ---------------------------------------------------------------- id derivation

    [TestMethod]
    public void SupplyTemplateIdFor_DerivesTheSiblingIdFromTheCaravanBinding()
    {
        // Deriving from the culture's own binding rather than from its StringId is what makes a
        // shared roster work: Lothlorien binds Rivendell's caravan template, so it must resolve to
        // Rivendell's supply template too, with no mapping table to go stale.
        Assert.AreEqual("supply_caravan_template_rivendell",
            SupplyCaravanService.SupplyTemplateIdFor("caravan_template_rivendell"));
        Assert.AreEqual("supply_caravan_template_rohan",
            SupplyCaravanService.SupplyTemplateIdFor("caravan_template_rohan"));
    }

    [TestMethod]
    public void SupplyTemplateIdFor_RefusesAnythingThatIsNotACaravanTemplate()
    {
        // The elite list, a lord template, or junk must not produce a plausible-looking id that
        // then silently resolves to nothing.
        foreach (var notACaravanTemplate in new[]
                 {
                     null, "", "   ",
                     "kingdom_hero_party_gondor_template",
                     "elite_caravan_template_erebor",
                     "villager_erebor_template",
                 })
        {
            Assert.IsNull(SupplyCaravanService.SupplyTemplateIdFor(notACaravanTemplate),
                $"'{notACaravanTemplate}' must not derive a supply template id");
        }
    }

    // ---------------------------------------------------------------- shipped data

    private static XDocument Templates()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_partyTemplates.xml");
        Assert.IsTrue(File.Exists(path), $"taom_partyTemplates.xml not found at {path}");
        return XDocument.Load(path);
    }

    private static IEnumerable<string> BoundCaravanTemplateIds()
    {
        // Read the bindings, not a glob: an unbound template is unreachable, and a culture that
        // binds something unexpected must be followed rather than guessed at.
        var ids = new HashSet<string>(StringComparer.Ordinal);

        var xml = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(),
            "taom_spcultures.xml"));
        foreach (var node in xml.Descendants("caravan_party_templates")
                     .Elements("caravan_party_template"))
        {
            var id = (string)node.Attribute("id") ?? "";
            if (id.StartsWith("PartyTemplate.caravan_template_", StringComparison.Ordinal))
                ids.Add(id.Replace("PartyTemplate.", ""));
        }

        var xslt = File.ReadAllText(Path.Combine(CultureDataFixture.ModuleDataPath(),
            "spcultures.xslt"));
        foreach (Match m in Regex.Matches(
                     xslt, @"<caravan_party_template id=""PartyTemplate\.(caravan_template_[a-z_]+)"""))
            ids.Add(m.Groups[1].Value);

        return ids;
    }

    [TestMethod]
    public void EveryBoundCaravanTemplate_HasASupplyCrewSibling()
    {
        // The invariant that makes the derivation safe. If this fails, PickCaravanTemplate resolves
        // null for that culture and its supply caravans spawn with no crew at all.
        var present = new HashSet<string>(
            Templates().Descendants("MBPartyTemplate").Select(t => (string)t.Attribute("id")),
            StringComparer.Ordinal);

        var bound = BoundCaravanTemplateIds().ToList();
        Assert.IsTrue(bound.Count >= 15, $"only {bound.Count} caravan bindings found; the sweep is empty");

        var missing = bound
            .Select(SupplyCaravanService.SupplyTemplateIdFor)
            .Where(id => id != null && !present.Contains(id))
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "every bound caravan template needs a supply crew sibling. Re-run "
            + "`python tools/generate_supply_caravan_templates.py --apply`. Missing:\n  "
            + string.Join("\n  ", missing));
    }

    [TestMethod]
    public void EverySupplyCrewTemplate_PlusTheDefaultEscort_FitsUnderTheFlatVanillaCap()
    {
        // The coupling that #549 was: the template size and the party's cap live in different
        // places, and nothing connected them. A supply caravan has no mechanism to grow its cap,
        // so the template is the only lever and it has to be sized against 20.
        var offenders = new List<string>();
        foreach (var template in Templates().Descendants("MBPartyTemplate"))
        {
            var id = (string)template.Attribute("id") ?? "";
            if (!id.StartsWith("supply_caravan_template_", StringComparison.Ordinal))
                continue;

            int crewMax = template.Descendants("PartyTemplateStack")
                .Sum(s => (int)s.Attribute("max_value"));
            if (crewMax + DefaultEscort > FlatVanillaCap)
                offenders.Add($"{id}: crew {crewMax} + escort {DefaultEscort} > cap {FlatVanillaCap}");
        }

        Assert.AreEqual(0, offenders.Count,
            "a supply caravan's cap is the flat 20; over it, vanilla applies the over-size speed "
            + "term and the player pays provisions for men the party cannot keep:\n  "
            + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void SupplyCrewTemplates_AreMuchSmallerThanTheAiCaravanTemplatesTheyReplaced()
    {
        // Guards the whole point of the split: if someone ever regenerates these from the AI
        // caravan numbers again, this fails rather than quietly restoring #549.
        var all = Templates().Descendants("MBPartyTemplate").ToList();

        int supplyMax = all
            .Where(t => ((string)t.Attribute("id") ?? "")
                .StartsWith("supply_caravan_template_", StringComparison.Ordinal))
            .Max(t => t.Descendants("PartyTemplateStack").Sum(s => (int)s.Attribute("max_value")));

        int aiCaravanMin = all
            .Where(t => ((string)t.Attribute("id") ?? "")
                .StartsWith("caravan_template_", StringComparison.Ordinal))
            .Min(t => t.Descendants("PartyTemplateStack").Sum(s => (int)s.Attribute("max_value")));

        Assert.IsTrue(supplyMax < aiCaravanMin,
            $"supply crew tops out at {supplyMax} but the smallest AI caravan is {aiCaravanMin}; "
            + "the two are supposed to be independently sized");
    }
}
