using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class FactionMapDataTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    private static readonly Regex KeyTokenRegex = new(@"^\{=([^}]+)\}", RegexOptions.Compiled);

    private static JObject LoadFactions()
    {
        var path = Path.Combine(ModuleDataPath, "factionmap", "factions.json");
        Assert.IsTrue(File.Exists(path), $"factions.json missing at {path}");
        return JObject.Parse(File.ReadAllText(path));
    }

    [TestMethod]
    public void FactionsJson_Parses_AsValidJsonObject()
    {
        var root = LoadFactions();
        Assert.IsTrue(root.Properties().Any(), "factions.json must have at least one entry");
    }

    [TestMethod]
    public void FactionsJson_HasAtLeast16PlayableFactions()
    {
        var root = LoadFactions();
        var playable = root.Properties()
            .Count(p => p.Value is JObject obj && obj.Value<bool?>("playable") == true);
        Assert.IsTrue(playable >= 16, $"Expected ≥16 playable factions, found {playable}");
    }

    [DataTestMethod]
    [DataRow("stewardship_of_gondor")]
    [DataRow("dominion_of_mordor")]
    [DataRow("dominion_of_isengard")]
    [DataRow("overlordship_of_dol_guldur")]
    [DataRow("overlordship_of_gundabad")]
    [DataRow("kingdom_of_erebor")]
    [DataRow("kingdom_of_imladris")]
    [DataRow("kingdom_of_lasgalen")]
    [DataRow("kingdom_of_lothlorien")]
    [DataRow("kingdom_of_rohan")]
    [DataRow("clans_of_dunland")]
    [DataRow("kingdom_of_dale")]
    [DataRow("taskralan_of_harwan")]
    [DataRow("golden_realm_of_rhun")]
    [DataRow("goblins_of_goblin_town")]
    [DataRow("kingdom_of_moria")]
    [DataRow("goblins_of_blue_craig")]
    [DataRow("kingdom_of_arthedain")]
    public void PlayableFaction_HasMinimumContentSections(string factionKey)
    {
        var root = LoadFactions();
        var faction = root[factionKey] as JObject;
        Assert.IsNotNull(faction, $"faction key '{factionKey}' missing from factions.json");
        Assert.AreEqual(true, faction.Value<bool?>("playable"),
            $"'{factionKey}' should be marked playable");

        AssertMinimumContentSections(faction, factionKey);
    }

    // Realms whose troops have no armour yet: the player may not start in them until the art lands.
    // Their cards still show on the faction map, so they keep the playable cards' content minimums.
    [DataTestMethod]
    [DataRow("high_kingdom_of_lindon")]
    [DataRow("kingdom_of_ered_duin")]
    [DataRow("kingdom_of_cardolan")]
    [DataRow("kingdom_of_rhudaur")]
    [DataRow("havens_of_umbar")]
    [DataRow("khudorom_of_khand")]
    public void WorkInProgressFaction_IsNotPlayable(string factionKey)
    {
        var faction = LoadFactions()[factionKey] as JObject;
        Assert.IsNotNull(faction, $"faction key '{factionKey}' missing from factions.json");
        Assert.AreEqual(false, faction.Value<bool?>("playable"),
            $"'{factionKey}' has no armour yet and must stay unplayable");

        AssertMinimumContentSections(faction, factionKey);
    }

    // The two Dale-cut Neutral realms carry only Dale's two culture feats (plain speed, loyalty), so their
    // cards list two bonuses, not the playable cards' three; the rest of the minimums hold.
    [DataTestMethod]
    [DataRow("anduin_vale", "anduinvale")]
    [DataRow("kingdom_of_angaladh", "angaladh")]
    public void DaleCutNeutralRealm_IsParkedNeutralAndCarriesItsFeats(string factionKey, string gameFaction)
    {
        var faction = LoadFactions()[factionKey] as JObject;
        Assert.IsNotNull(faction, $"faction key '{factionKey}' missing from factions.json");
        Assert.AreEqual(false, faction.Value<bool?>("playable"), $"'{factionKey}' has no armour yet and must stay unplayable");
        Assert.AreEqual(gameFaction, faction.Value<string>("game_faction"));
        Assert.AreEqual("neutral", faction.Value<string>("side"));
        AssertArrayLength(faction, "traits", min: 3, factionKey);
        AssertArrayLength(faction, "bonuses", min: 2, factionKey);
        AssertArrayLength(faction, "perks", min: 2, factionKey);
        AssertArrayLength(faction, "special_units", min: 3, factionKey);
        AssertArrayLength(faction, "strengths", min: 3, factionKey);
        AssertArrayLength(faction, "weaknesses", min: 2, factionKey);

        // The numbers come from the feats' own Initialize calls, so a feat change fails this card.
        var speed = (TAOM.Tests.Features.CulturalFeats.TaomCulturalFeatsDefinitionTests.ProductionEffectBonus("_dalePlainSpeed") * 100m)
            .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var loyalty = TAOM.Tests.Features.CulturalFeats.TaomCulturalFeatsDefinitionTests.ProductionEffectBonus("_daleLoyalty")
            .ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture);
        var bonuses = ((JArray)faction["bonuses"]!).Select(b => b.Value<string>("text") ?? "").ToList();
        Assert.IsTrue(bonuses.Any(b => b.Contains($"+{speed}% party speed on plains")), $"taom_dale_plain_speed is +{speed}%");
        Assert.IsTrue(bonuses.Any(b => b.Contains($"{loyalty} settlement loyalty per day")), $"taom_dale_loyalty is {loyalty}");
    }

    private static void AssertMinimumContentSections(JObject faction, string factionKey)
    {
        AssertArrayLength(faction, "perks", min: 2, factionKey);
        AssertArrayLength(faction, "bonuses", min: 3, factionKey);
        AssertArrayLength(faction, "special_units", min: 1, factionKey);
        AssertArrayLength(faction, "strengths", min: 3, factionKey);
        AssertArrayLength(faction, "weaknesses", min: 2, factionKey);
        AssertArrayLength(faction, "traits", min: 3, factionKey);
    }

    private static void AssertArrayLength(JObject faction, string field, int min, string factionKey)
    {
        var arr = faction[field] as JArray;
        Assert.IsNotNull(arr, $"'{factionKey}'.{field} must be an array");
        Assert.IsTrue(arr.Count >= min, $"'{factionKey}'.{field} must have ≥{min} entries (got {arr.Count})");
    }

    [TestMethod]
    public void EveryFactionStringField_StartsWithLocalizationKey()
    {
        var root = LoadFactions();
        var violations = new List<string>();

        foreach (var factionProp in root.Properties())
        {
            if (factionProp.Value is not JObject faction) continue;

            void Check(string fieldPath, string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                if (!KeyTokenRegex.IsMatch(value))
                    violations.Add($"{factionProp.Name}.{fieldPath}: '{value}' (missing {{=KEY}} prefix)");
            }

            Check("name", faction.Value<string>("name") ?? "");
            Check("description", faction.Value<string>("description") ?? "");

            if (faction["traits"] is JArray traits)
                for (int i = 0; i < traits.Count; i++)
                    Check($"traits[{i}]", traits[i]?.Value<string>() ?? "");

            CheckObjectArray(faction, "perks", new[] { "name", "description" }, Check);
            CheckObjectArray(faction, "special_units", new[] { "name", "description" }, Check);

            if (faction["bonuses"] is JArray bonuses)
                for (int i = 0; i < bonuses.Count; i++)
                    if (bonuses[i] is JObject b)
                        Check($"bonuses[{i}].text", b.Value<string>("text") ?? "");

            foreach (var arrName in new[] { "strengths", "weaknesses" })
                if (faction[arrName] is JArray arr)
                    for (int i = 0; i < arr.Count; i++)
                        Check($"{arrName}[{i}]", arr[i]?.Value<string>() ?? "");
        }

        if (violations.Count > 0)
            Assert.Fail("factions.json has un-localized strings:\n" + string.Join("\n", violations));
    }

    private static void CheckObjectArray(JObject faction, string arrName, string[] fields, Action<string, string> check)
    {
        if (faction[arrName] is not JArray list) return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is not JObject obj) continue;
            foreach (var field in fields)
                check($"{arrName}[{i}].{field}", obj.Value<string>(field) ?? "");
        }
    }

    [TestMethod]
    public void EveryFactionMapKey_HasMatchingStringInTaomModuleStrings()
    {
        var root = LoadFactions();
        var jsonKeys = new HashSet<string>();
        foreach (var s in EnumerateStrings(root))
        {
            var m = KeyTokenRegex.Match(s);
            if (m.Success && m.Groups[1].Value.StartsWith("taom_faction_"))
                jsonKeys.Add(m.Groups[1].Value);
        }

        var stringsXml = File.ReadAllText(Path.Combine(ModuleDataPath, "taom_module_strings.xml"));
        var xmlKeys = new HashSet<string>(
            Regex.Matches(stringsXml, @"<string id=""([^""]+)""")
                 .Cast<Match>()
                 .Select(m => m.Groups[1].Value));

        var missing = jsonKeys.Where(k => !xmlKeys.Contains(k)).OrderBy(k => k).ToList();
        if (missing.Count > 0)
        {
            Assert.Fail(
                $"{missing.Count} factionmap keys referenced in factions.json have no matching " +
                $"<string id=\"...\"> in taom_module_strings.xml. Re-run " +
                $"`python tools/harvest_factionmap_strings.py`. First 10 missing:\n" +
                string.Join("\n", missing.Take(10)));
        }
    }

    /// <summary>
    /// The faction card (character-creation culture stage, faction screen) quotes each party-size
    /// bonus as a number typed into factions.json, while the game applies the culture feat in
    /// TaomCulturalFeats.cs. Nothing tied the two together until #818 cut the evil feats and left
    /// the cards promising +40 / +30 / +20 %. Every "+N% party size" in a faction's text must equal
    /// the production feat's EffectBonus.
    /// </summary>
    [DataTestMethod]
    [DataRow("goblins_of_goblin_town", "_goblinPartySize")]
    [DataRow("goblins_of_blue_craig", "_bluecraigPartySize")]
    [DataRow("kingdom_of_moria", "_mistyMountainOrcsPartySize")]
    [DataRow("dominion_of_isengard", "_isengardPartySize")]
    [DataRow("overlordship_of_gundabad", "_gundabadPartySize")]
    [DataRow("overlordship_of_dol_guldur", "_dolguldurPartySize")]
    [DataRow("dominion_of_mordor", "_mordorPartySize")]
    [DataRow("stewardship_of_gondor", "_gondorPartySize")]
    [DataRow("khudorom_of_khand", "_khandPartySize")]
    [DataRow("golden_realm_of_rhun", "_rhunPartySize")]
    [DataRow("taskralan_of_harwan", "_haradPartySize")]
    [DataRow("clans_of_dunland", "_dunlandPartySize")]
    public void FactionCard_PartySizePercent_MatchesProductionFeat(string factionKey, string featField)
    {
        var faction = LoadFactions()[factionKey] as JObject;
        Assert.IsNotNull(faction, $"faction key '{factionKey}' missing from factions.json");

        var expected = TAOM.Tests.Features.CulturalFeats.TaomCulturalFeatsDefinitionTests
            .ProductionEffectBonus(featField) * 100m;

        var quoted = new List<decimal>();
        foreach (var text in EnumerateStrings(faction))
        {
            var found = PartySizePercentRegex.Matches(text).Cast<Match>()
                .Select(m => decimal.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
            var mentions = Regex.Matches(text, "party size", RegexOptions.IgnoreCase).Count;
            Assert.AreEqual(mentions, found.Count,
                $"'{factionKey}': a party-size mention without a '+N%' prefix, which this gate cannot read: '{text}'");
            quoted.AddRange(found);
        }

        Assert.IsTrue(quoted.Count > 0,
            $"'{factionKey}' quotes no party-size percentage; remove the row or the mapping to {featField}");
        foreach (var percent in quoted)
            Assert.AreEqual(expected, percent,
                $"'{factionKey}' card says +{percent}% party size but {featField} is +{expected}%");
    }

    private static readonly Regex PartySizePercentRegex = new(
        @"\+(?<n>\d+(?:\.\d+)?)% party size", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static IEnumerable<string> EnumerateStrings(JToken node)
    {
        switch (node)
        {
            case JValue v when v.Type == JTokenType.String:
                yield return v.Value<string>() ?? "";
                break;
            case JObject obj:
                foreach (var p in obj.Properties())
                    foreach (var s in EnumerateStrings(p.Value))
                        yield return s;
                break;
            case JArray arr:
                foreach (var item in arr)
                    foreach (var s in EnumerateStrings(item))
                        yield return s;
                break;
        }
    }
}
