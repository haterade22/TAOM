using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The shipped <c>war_chronicle/war_chronicle.json</c> (#765 v1 content): it loads with no warning, the sieges
/// keep the agreed target and sides (owners confirmed by hand in the live TAOM_Map settlements.xml on
/// 2026-10-08, not re-checked here; at runtime the open precondition Skips a siege whose target the defender
/// no longer holds), every attacker and defender pair is Hostile in diplomacy.json, the stalemate rule pays
/// both sides, and every text key is registered in taom_wotr_strings.xml with the same English.
/// </summary>
[TestClass]
public class ChronicleShippedContentTests
{
    private static string Json => File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "war_chronicle", "war_chronicle.json"));

    private static IReadOnlyList<ChronicleEventDefinition> Load(out List<string> warnings)
    {
        warnings = new List<string>();
        return ChronicleConfigValidator.Parse(Json, warnings);
    }

    [TestMethod]
    public void ShippedFile_LoadsAllThirteenEventsWithNoWarning()
    {
        var events = Load(out var warnings);

        Assert.AreEqual(0, warnings.Count, string.Join("\n", warnings));
        CollectionAssert.AreEqual(
            new[] { "fellowship", "khazaddum", "breaking", "theoden", "hornburg", "ents", "lorien", "osgiliath", "dale", "corsairs", "north", "gondor", "blackgate" },
            events.Select(e => e.Id).ToArray());
    }

    [DataTestMethod]
    [DataRow("hornburg", "town_V2", "vlandia", "isengard,empire")]
    [DataRow("lorien", "town_L1", "lothlorien", "dolguldur")]
    [DataRow("osgiliath", "town_EW3", "empire_w", "empire_s")]
    [DataRow("dale", "town_S3", "sturgia", "khuzait,dolguldur")]
    [DataRow("corsairs", "town_EW4", "empire_w", "umbar,empire_s")]
    [DataRow("north", "town_AN3", "arthedain", "gundabad,empire")]
    [DataRow("gondor", "town_EW1", "empire_w", "empire_s")]
    public void EverySiege_TargetsItsDefendersTownWithTheAgreedAttackers(string id, string target, string defender, string attackers)
    {
        var siege = Load(out _).Single(e => e.Id == id);

        Assert.AreEqual(ChronicleEventKind.Siege, siege.Kind);
        Assert.AreEqual(target, siege.TargetSettlementId);
        Assert.AreEqual(defender, siege.Defender);
        CollectionAssert.AreEqual(attackers.Split(','), siege.Attackers.ToArray());
    }

    [TestMethod]
    public void EverySiegePair_IsHostileInDiplomacyJson()
    {
        var hostile = new HashSet<string>();
        var diplomacy = JObject.Parse(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "diplomacy", "diplomacy.json")));
        foreach (var row in (JArray)diplomacy["relationships"]!)
        {
            if ((string?)row["tier"] == "Hostile")
                hostile.Add(Pair((string)row["kingdomA"]!, (string)row["kingdomB"]!));
        }

        foreach (var siege in Load(out _).Where(e => e.IsSiege))
        {
            foreach (var attacker in siege.Attackers)
                Assert.IsTrue(hostile.Contains(Pair(attacker, siege.Defender!)), $"{siege.Id}: {attacker} -> {siege.Defender} is not Hostile");
        }
    }

    [TestMethod]
    public void EverySiege_PaysBothSidesByTheStalemateRule()
    {
        foreach (var siege in Load(out _).Where(e => e.IsSiege))
        {
            AssertRow(siege.Id + " held", siege.Held!.Effects, "defender", WarEffectKind.VolunteerRate, 0.15f);
            AssertRow(siege.Id + " held", siege.Held.Effects, "attacker", WarEffectKind.PrisonerEscape, 0.20f);
            AssertRow(siege.Id + " fell", siege.Fell!.Effects, "attacker", WarEffectKind.VolunteerRate, 0.15f);
            AssertRow(siege.Id + " fell", siege.Fell.Effects, "defender", WarEffectKind.PrisonerEscape, 0.20f);
            // A held siege earns the Free side nothing from the capture path, so the file pays it momentum.
            Assert.AreEqual(MomentumSide.Free, siege.Held.Momentum?.Side, siege.Id + " held momentum side");
            Assert.AreEqual(100, siege.Held.Momentum!.Units, siege.Id + " held momentum units");
        }
    }

    [TestMethod]
    public void TheBattleOfDale_ALossAlsoSpeedsEreborsEscapes()
    {
        var dale = Load(out _).Single(e => e.Id == "dale");

        AssertRow("dale fell", dale.Fell!.Effects, "erebor", WarEffectKind.PrisonerEscape, 0.20f);
    }

    [TestMethod]
    public void TheEntsAndTheBlackGate_RequireAHeldSiege()
    {
        var events = Load(out _);

        Assert.AreEqual("hornburg", events.Single(e => e.Id == "ents").AnchorEventId);
        Assert.AreEqual(ChronicleEventState.Held, events.Single(e => e.Id == "ents").RequiresOutcome);
        Assert.AreEqual("gondor", events.Single(e => e.Id == "blackgate").AnchorEventId);
        Assert.AreEqual(ChronicleEventState.Held, events.Single(e => e.Id == "blackgate").RequiresOutcome);
        Assert.AreEqual("osgiliath", events.Single(e => e.Id == "gondor").AnchorEventId);
        Assert.IsNull(events.Single(e => e.Id == "gondor").RequiresOutcome, "the Siege of Gondor follows Osgiliath whatever its result");
    }

    [TestMethod]
    public void EveryText_IsAKeyRegisteredInTheWotrStringsWithTheSameEnglish()
    {
        var registered = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_wotr_strings.xml"))
            .Root!.Elements("string").ToDictionary(s => (string)s.Attribute("id")!, s => (string)s.Attribute("text")!);
        var keyed = new Regex(@"^\{=(?<key>taom_wc_[a-z]+_[a-z]+)\}(?<english>.+)$");

        var texts = JToken.Parse(Json).SelectTokens("$..title").Concat(JToken.Parse(Json).SelectTokens("$..text"))
            .Concat(JToken.Parse(Json).SelectTokens("$..openText")).Concat(JToken.Parse(Json).SelectTokens("$..lapsedText"))
            .Concat(JToken.Parse(Json).SelectTokens("$..fateText"))
            .Select(t => (string)t!).ToList();

        Assert.AreEqual(48, texts.Count);
        foreach (var text in texts)
        {
            var match = keyed.Match(text);
            Assert.IsTrue(match.Success, "not a {=taom_wc_<event>_<part>}English text: " + text);
            Assert.IsTrue(registered.TryGetValue(match.Groups["key"].Value, out var english), "unregistered: " + match.Groups["key"].Value);
            Assert.AreEqual(match.Groups["english"].Value, english, match.Groups["key"].Value);
            Assert.IsFalse(english.Contains('—') || english.Contains('–'), "no em or en dash: " + match.Groups["key"].Value);
        }
    }

    [TestMethod]
    public void EverySiegeOpenLine_NamesTheTargetAndTheDays()
    {
        foreach (var siege in Load(out _).Where(e => e.IsSiege))
        {
            StringAssert.Contains(siege.OpenText, "{TARGET}", siege.Id);
            StringAssert.Contains(siege.OpenText, "{DAYS}", siege.Id);
            Assert.IsFalse(string.IsNullOrEmpty(siege.LapsedText), siege.Id);
        }
    }

    [DataTestMethod]
    [DataRow("taom_wotr_chronicle_events", "Chronicle Events")]
    [DataRow("taom_wc_siege_lost", null)]
    public void ACodeDefinedChronicleText_IsRegisteredWithTheSameEnglish(string id, string? english)
    {
        // The two texts written in C#, not in war_chronicle.json, which the JSON key test cannot see.
        if (english == null)
            english = ChronicleService.LostText.Substring(ChronicleService.LostText.IndexOf('}') + 1);
        var row = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_wotr_strings.xml"))
            .Root!.Elements("string").SingleOrDefault(s => (string)s.Attribute("id")! == id);

        Assert.IsNotNull(row, id + " is not registered");
        Assert.AreEqual(english, (string)row!.Attribute("text")!);
    }

    private static string Pair(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

    private static void AssertRow(string what, IReadOnlyList<ChronicleEffectDefinition> rows, string target, WarEffectKind kind, float magnitude)
    {
        Assert.IsTrue(rows.Any(r => r.Target == target && r.Kind == kind && System.Math.Abs(r.Magnitude - magnitude) < 0.0001f && r.DurationDays == 25),
            $"{what}: no {target} {kind} {magnitude} for 25 days");
    }
}
