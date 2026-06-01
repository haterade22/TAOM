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
    [DataRow("havens_of_umbar")]
    [DataRow("kingdom_of_erebor")]
    [DataRow("kingdom_of_imladris")]
    [DataRow("kingdom_of_lasgalen")]
    [DataRow("kingdom_of_lothlorien")]
    [DataRow("kingdom_of_rohan")]
    [DataRow("clans_of_dunland")]
    [DataRow("kingdom_of_dale")]
    [DataRow("khudorom_of_khand")]
    [DataRow("taskralan_of_harwan")]
    [DataRow("golden_realm_of_rhun")]
    public void PlayableFaction_HasMinimumContentSections(string factionKey)
    {
        var root = LoadFactions();
        var faction = root[factionKey] as JObject;
        Assert.IsNotNull(faction, $"faction key '{factionKey}' missing from factions.json");
        Assert.AreEqual(true, faction.Value<bool?>("playable"),
            $"'{factionKey}' should be marked playable");

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
