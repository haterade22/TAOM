using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Config validation tests for the per-culture hero template lists in
/// taom_spcultures.xml.
///
/// Root cause these pin: every TAOM culture shipped the vanilla <c>empire</c>
/// block verbatim for both &lt;lord_templates&gt; and
/// &lt;rebellion_hero_templates&gt; — the sixteen
/// <c>spc_legion_of_the_betrayed / hidden_hand / embers_of_flame / eleftheroi</c>
/// leader templates, every one of them <c>culture="Culture.empire"</c>.
///
/// The engine never re-derives a culture for heroes built from those lists.
/// <c>HeroCreator.CreateSpecialHero</c> leaves the culture unset, so
/// <c>DefaultHeroCreationModel.GetCulture</c> falls through to the *template's*
/// culture and ignores the born settlement entirely. Landing a companion
/// (<c>CompanionRolesCampaignBehavior.SpawnNewHeroesForNewCompanionClan</c>) and
/// a settlement rebellion (<c>RebellionsCampaignBehavior</c>) therefore produced
/// Dunlendings in Erebor, Gondor, Mordor and everywhere else.
/// </summary>
[TestClass]
public class CultureLordTemplateTests
{
    private const string LordTemplates = "lord_templates";
    private const string RebellionHeroTemplates = "rebellion_hero_templates";

    [TestMethod]
    public void EveryLordTemplate_HasTheCultureOfTheCultureThatListsIt()
    {
        AssertTemplateListMatchesItsCulture(LordTemplates);
    }

    [TestMethod]
    public void EveryRebellionHeroTemplate_HasTheCultureOfTheCultureThatListsIt()
    {
        AssertTemplateListMatchesItsCulture(RebellionHeroTemplates);
    }

    [TestMethod]
    public void EveryCulture_ListsAtLeastOneLordTemplateAndOneRebellionHeroTemplate()
    {
        // MBReadOnlyList.GetRandomElement indexes straight into the list, so an
        // empty one throws rather than degrading. Cultures whose only settlements
        // are hideouts are the exception — see HideoutOnlyCultures for why neither
        // reader can reach them, and what would void that.
        var empty = new List<string>();
        foreach (var culture in CulturesInSpCultures())
        {
            var id = culture.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(id) || HideoutOnlyCultures.Contains(id))
                continue;

            foreach (var listName in new[] { LordTemplates, RebellionHeroTemplates })
            {
                if (!ReferencedTemplateIds(culture, listName).Any())
                    empty.Add($"{id}/{listName}");
            }
        }

        Assert.AreEqual(0, empty.Count,
            $"Cultures with an empty hero template list: {string.Join(", ", empty)}. " +
            "GetRandomElement throws on an empty list — give the culture its own templates.");
    }

    private static void AssertTemplateListMatchesItsCulture(string listName)
    {
        var charactersByCulture = TaomCharacterCultures();
        var offenders = new List<string>();

        foreach (var culture in CulturesInSpCultures())
        {
            var cultureId = culture.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(cultureId))
                continue;

            foreach (var templateId in ReferencedTemplateIds(culture, listName))
            {
                if (!charactersByCulture.TryGetValue(templateId, out var owningCulture))
                {
                    offenders.Add($"{cultureId} -> {templateId} (not defined in TAOM ModuleData)");
                    continue;
                }

                if (!string.Equals(owningCulture, cultureId, StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{cultureId} -> {templateId} (culture={owningCulture ?? "unset"})");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            $"Templates in <{listName}> that do not belong to the culture listing them: " +
            $"{string.Join("; ", offenders)}. A hero built from one of these keeps the " +
            "TEMPLATE's culture — DefaultHeroCreationModel.GetCulture never looks at the " +
            "settlement — so it spawns with the wrong name pool, face and equipment.");
    }

    /// <summary>
    /// Minor cultures whose only settlements are hideouts, so neither reader can reach them.
    ///
    /// NOT "landless" — every one of these owns settlements (159 between them in
    /// <c>TAOM_Map/ModuleData/settlements.xml</c>), and all 159 are <c>&lt;Hideout&gt;</c>. The two
    /// exemptions rest on that, and each needs to stay true:
    ///
    /// <list type="bullet">
    /// <item><c>RebellionsCampaignBehavior</c> reads <c>settlement.Culture.RebelliousHeroTemplates</c>
    /// for a town or castle that revolts. A hideout never revolts.</item>
    /// <item><c>CompanionRolesCampaignBehavior</c> reads <c>companionHero.Culture.LordTemplates</c>
    /// when a companion is granted a fief. TAOM ships 208 wanderers across 18 cultures and none of
    /// them is one of these eight, so no companion can carry one of these cultures.</item>
    /// </list>
    ///
    /// Give any of them a town, or ship a wanderer of one, and the exemption is void — the list
    /// becomes an empty <c>MBReadOnlyList</c> and <c>GetRandomElement</c> indexes straight into it.
    ///
    /// Distinct from <c>Patch65_LandlessCultureSpawnGuard</c>'s sense of "landless" (a culture
    /// owning NO settlement, which makes vanilla <c>SpawnLordParty</c>'s unguarded
    /// <c>Settlement.All.First(culture)</c> throw). That guard decides at runtime via
    /// <c>AnySettlementHasHeroCulture</c> and keeps no list. Two different questions — do not merge
    /// the vocabularies.
    /// </summary>
    private static readonly HashSet<string> HideoutOnlyCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dunland_raiders", "rhun_raiders", "harad_raiders", "gundabad_raiders",
        "umbar_corsairs", "gondor_soldiers", "erebor_warriors", "mirkwood_stalkers"
    };

    private static IEnumerable<XElement> CulturesInSpCultures()
    {
        var path = Path.Combine(ModuleDataPath(), "taom_spcultures.xml");
        Assert.IsTrue(File.Exists(path), $"taom_spcultures.xml not found at {path}");
        return XDocument.Load(path).Descendants("Culture").ToList();
    }

    private static IEnumerable<string> ReferencedTemplateIds(XElement culture, string listName)
    {
        var list = culture.Element(listName);
        if (list == null)
            yield break;

        foreach (var entry in list.Elements())
        {
            var reference = entry.Attribute("name")?.Value ?? entry.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(reference))
                yield return StripPrefix(reference);
        }
    }

    /// <summary>Every NPCCharacter TAOM defines, mapped to its declared culture id.</summary>
    private static Dictionary<string, string> TaomCharacterCultures()
    {
        var byId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(ModuleDataPath(), "*.xml", SearchOption.AllDirectories))
        {
            XDocument document;
            try
            {
                document = XDocument.Load(file);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            foreach (var character in document.Descendants("NPCCharacter"))
            {
                var id = character.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id))
                    continue;

                var culture = character.Attribute("culture")?.Value;
                byId[id] = culture == null ? null : StripPrefix(culture);
            }
        }

        return byId;
    }

    private static string StripPrefix(string reference)
    {
        var dot = reference.IndexOf('.');
        return dot < 0 ? reference : reference.Substring(dot + 1);
    }

    private static string ModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail("Could not locate Main/_Module/ModuleData from the test working directory.");
        return null;
    }
}
