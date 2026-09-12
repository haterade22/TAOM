using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordPartyTemplates;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.LordPartyTemplates;

/// <summary>
/// Pins the shipped <c>lord_party_templates.json</c> and the two templates it names (#580). The
/// provider is fail-soft and the patch falls back to vanilla on an unresolvable template, so a typo
/// in either file would look exactly like the feature working. Nothing else reads the JSON: the
/// ModuleData validator sweeps <c>PartyTemplate.</c> references in XML and never sees it.
/// </summary>
[TestClass]
public class ShippedLordPartyTemplateTests
{
    private const string FaramirId = "lord_1_34";
    private const string SauronId = "lord_1_17";
    private const string FaramirTemplate = "kingdom_hero_party_gondor_faramir_template";
    private const string SauronTemplate = "kingdom_hero_party_mordor_sauron_template";

    // tools/rebalance_party_template_maxes.py CULTURE_TARGETS: the lord-party spawn ceilings.
    private const int GondorTarget = 200;
    private const int MordorTarget = 260;

    private static string ModuleDataPath => CultureDataFixture.ModuleDataPath();

    private sealed class Stack
    {
        public string Troop = "";
        public int Min;
        public int Max;
    }

    private sealed class Troop
    {
        public int Level;
        public string Group = "";
    }

    private static Dictionary<string, List<Stack>> _templates = null!;
    private static Dictionary<string, Troop> _troops = null!;
    private static Dictionary<string, string> _overrides = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _templates = LoadTemplates();
        _troops = LoadTroops();
        _overrides = LoadShippedOverrides();
    }

    private static Dictionary<string, List<Stack>> LoadTemplates()
    {
        var doc = XDocument.Load(Path.Combine(ModuleDataPath, "taom_partyTemplates.xml"));
        return doc.Descendants("MBPartyTemplate")
            .ToDictionary(
                t => (string)t.Attribute("id")!,
                t => t.Descendants("PartyTemplateStack")
                    .Select(s => new Stack
                    {
                        Troop = CultureDataFixture.StripPrefix((string)s.Attribute("troop")!),
                        Min = (int)s.Attribute("min_value")!,
                        Max = (int)s.Attribute("max_value")!,
                    })
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, Troop> LoadTroops()
    {
        var troops = new Dictionary<string, Troop>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(ModuleDataPath, "troops"), "troops_*.xml"))
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string?)npc.Attribute("id");
                var level = (int?)npc.Attribute("level");
                if (id == null || level == null) continue;
                troops[id] = new Troop { Level = level.Value, Group = (string?)npc.Attribute("default_group") ?? "Infantry" };
            }
        }
        return troops;
    }

    private static Dictionary<string, string> LoadShippedOverrides()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        var logger = Substitute.For<IModLogger>();

        var config = new LordPartyTemplateConfigProvider(pathService, logger).GetConfig();

        logger.DidNotReceive().LogError(Arg.Any<string>());
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        return config.Overrides;
    }

    private static int MaxSum(string templateId, Func<Troop, bool> where) =>
        _templates[templateId].Where(s => where(_troops[s.Troop])).Sum(s => s.Max);

    // ---- the JSON ---------------------------------------------------------------------------

    [TestMethod]
    public void ShippedConfig_MapsFaramirAndSauron()
    {
        Assert.AreEqual(FaramirTemplate, _overrides[FaramirId], "Faramir");
        Assert.AreEqual(SauronTemplate, _overrides[SauronId], "Sauron");
    }

    [TestMethod]
    public void ShippedConfig_EveryHeroIdIsALordTaomDefinesOrRetags()
    {
        // Faramir and Sauron are vanilla ids retagged by lords.xslt; a TAOM-authored lord would sit
        // in characters/lords.xml. Either is a real NPCCharacter the engine will spawn a party for.
        var xslt = File.ReadAllText(Path.Combine(ModuleDataPath, "lords.xslt"));
        var lordsXml = File.ReadAllText(Path.Combine(ModuleDataPath, "characters", "lords.xml"));

        foreach (var heroId in _overrides.Keys)
        {
            var retagged = xslt.Contains("NPCCharacter[@id='" + heroId + "']");
            var authored = Regex.IsMatch(lordsXml, "\\bid=\"" + Regex.Escape(heroId) + "\"");
            Assert.IsTrue(retagged || authored, heroId + " is neither retagged in lords.xslt nor defined in characters/lords.xml");
        }
    }

    [TestMethod]
    public void ShippedConfig_EveryTemplateIdExists()
    {
        foreach (var kv in _overrides)
            Assert.IsTrue(_templates.ContainsKey(kv.Value), kv.Key + " maps to " + kv.Value + ", which taom_partyTemplates.xml does not define");
    }

    [TestMethod]
    public void ShippedConfig_TemplatesAreBoundByNoClanOrCulture()
    {
        // The whole point is that ONLY the named hero fields the roster. A clan or culture binding
        // would hand it to every lord of that clan, which is the outcome the override exists to avoid.
        var bindingFiles = new[]
        {
            Path.Combine(ModuleDataPath, "spclans.xslt"),
            Path.Combine(ModuleDataPath, "characters", "clans.xml"),
            Path.Combine(ModuleDataPath, "spcultures.xslt"),
            Path.Combine(ModuleDataPath, "taom_spcultures.xml"),
        };

        foreach (var templateId in _overrides.Values.Distinct())
        foreach (var file in bindingFiles)
            Assert.IsFalse(File.ReadAllText(file).Contains("PartyTemplate." + templateId),
                Path.GetFileName(file) + " binds " + templateId + ", which is reserved for the per-hero override");
    }

    // ---- the templates ----------------------------------------------------------------------

    [TestMethod]
    public void ShippedTemplates_EveryStackTroopExistsWithALevel()
    {
        foreach (var templateId in _overrides.Values.Distinct())
        foreach (var stack in _templates[templateId])
            Assert.IsTrue(_troops.ContainsKey(stack.Troop), templateId + " names " + stack.Troop + ", which no troops_*.xml defines with a level");
    }

    [TestMethod]
    public void ShippedTemplates_NoStackHasMinAboveMaxOrZeroMax()
    {
        foreach (var templateId in _overrides.Values.Distinct())
        foreach (var stack in _templates[templateId])
        {
            Assert.IsTrue(stack.Max > 0, templateId + "/" + stack.Troop + " max is 0, a dead stack");
            Assert.IsTrue(stack.Min <= stack.Max, templateId + "/" + stack.Troop + " min " + stack.Min + " above max " + stack.Max);
        }
    }

    [TestMethod]
    public void ShippedTemplates_MinSumsStayInTheSiblingBand()
    {
        // The engine draws one ratio per party and fills every stack to min + (max - min) * r, so
        // the min sum is the smallest party the lord can spawn. Siblings sit at 10 to 14.
        foreach (var templateId in _overrides.Values.Distinct())
        {
            var minSum = _templates[templateId].Sum(s => s.Min);
            Assert.IsTrue(minSum >= 8 && minSum <= 16, templateId + " min sum " + minSum + " is outside the 8 to 16 band");
        }
    }

    [TestMethod]
    public void FaramirTemplate_SumsToGondorTarget()
    {
        Assert.AreEqual(GondorTarget, _templates[FaramirTemplate].Sum(s => s.Max));
    }

    [TestMethod]
    public void FaramirTemplate_IsHalfRangedFifthCavalryRestInfantry()
    {
        // The brief: 50 ranged, 20 cavalry, 30 infantry, as ratios of the spawn ceiling.
        Assert.AreEqual(GondorTarget / 2, MaxSum(FaramirTemplate, t => t.Group == "Ranged"), "ranged");
        Assert.AreEqual(GondorTarget / 5, MaxSum(FaramirTemplate, t => t.Group == "Cavalry"), "cavalry");
        Assert.AreEqual(GondorTarget * 3 / 10, MaxSum(FaramirTemplate, t => t.Group == "Infantry"), "infantry");
    }

    [TestMethod]
    public void FaramirTemplate_IsIthilienWithMinasTirithHorse()
    {
        var stacks = _templates[FaramirTemplate];

        // Every foot soldier is Ithil Guard; the Ithilien Ranger capstone is in; the horse is the
        // Anorien (Minas Tirith) line because Ithilien has no mounted troops.
        foreach (var stack in stacks.Where(s => _troops[s.Troop].Group != "Cavalry"))
            Assert.IsTrue(stack.Troop.StartsWith("gondor_ith", StringComparison.Ordinal), stack.Troop + " is not an Ithilien troop");
        Assert.IsTrue(stacks.Any(s => s.Troop == "gondor_ithilien_ranger"), "the Ithilien Ranger is missing");
        foreach (var stack in stacks.Where(s => _troops[s.Troop].Group == "Cavalry"))
            Assert.IsTrue(stack.Troop.StartsWith("gondor_ano_mt_", StringComparison.Ordinal), stack.Troop + " is not Minas Tirith cavalry");
    }

    [TestMethod]
    public void SauronTemplate_SumsToMordorTarget()
    {
        Assert.AreEqual(MordorTarget, _templates[SauronTemplate].Sum(s => s.Max));
    }

    [TestMethod]
    public void SauronTemplate_IsFortyLowThirtyMidThirtyHigh()
    {
        // Tiers by level: low up to 16, mid 21 to 31, high 36 and up. Levels step by 5 from 1, so
        // the bands are exhaustive.
        Assert.AreEqual(MordorTarget * 2 / 5, MaxSum(SauronTemplate, t => t.Level <= 16), "low");
        Assert.AreEqual(MordorTarget * 3 / 10, MaxSum(SauronTemplate, t => t.Level >= 21 && t.Level <= 31), "mid");
        Assert.AreEqual(MordorTarget * 3 / 10, MaxSum(SauronTemplate, t => t.Level >= 36), "high");
    }

    [TestMethod]
    public void SauronTemplate_HighTierIsBlackNumenoreanAndUruk()
    {
        foreach (var stack in _templates[SauronTemplate].Where(s => _troops[s.Troop].Level >= 36))
            Assert.IsTrue(
                stack.Troop.StartsWith("mordor_num_", StringComparison.Ordinal) || stack.Troop.StartsWith("mordor_uruk_", StringComparison.Ordinal),
                stack.Troop + " is high tier but neither Black Numenorean nor Uruk");
    }

    [TestMethod]
    public void SauronTemplate_FieldsNoRecruitOrLackey()
    {
        // The Melkondili clan roster Sauron used to share opens with orc lackeys and warg tamers.
        // "Middle to high tier" means nothing below a level-11 fighter.
        foreach (var stack in _templates[SauronTemplate])
            Assert.IsTrue(_troops[stack.Troop].Level >= 11, stack.Troop + " (level " + _troops[stack.Troop].Level + ") is rabble");
    }
}
