using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using TAOM.Features.InnRounds;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.InnRounds;

/// <summary>
/// #901: the shipped rows and keys. Every main culture pours a named drink and a priced one; every <c>{=taom_round_*}</c> key the
/// code carries is a registered row and every registered row is referenced; and the English is gender-neutral and names none
/// of the listener's people (the player can be a dwarf, an orc or an elf, of either sex, at any tavern).
/// </summary>
[TestClass]
public class InnRoundsDataTests
{
    // The six vanilla cultures TAOM renames (spcultures.xslt); the rest come from taom_spcultures.xml.
    private static readonly string[] VanillaMainCultures = { "empire", "vlandia", "battania", "khuzait", "aserai", "sturgia" };

    private static readonly Regex Keyed = new(@"^\{=(?<key>[^}]+)\}(?<body>.*)$", RegexOptions.Singleline);
    private static readonly Regex RoundKey = new(@"\{=(taom_round_[A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    private static readonly Lazy<List<(string Id, string Text)>> ModuleRows = new(() =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Descendants("string")
            .Select(e => ((string)e.Attribute("id")!, (string)e.Attribute("text")!))
            .ToList());

    private static IEnumerable<string> MainCultureIds()
    {
        var doc = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_spcultures.xml"));
        return doc.Descendants("Culture")
            .Where(c => (string?)c.Attribute("is_main_culture") == "true")
            .Select(c => (string)c.Attribute("id")!)
            .Concat(VanillaMainCultures)
            .ToList();
    }

    private static Dictionary<string, string> ShippedDrinks() =>
        JsonConvert.DeserializeObject<InnRoundsConfig>(File.ReadAllText(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData", "inn_rounds", "inn_rounds.json")))!.CultureDrinks;

    private static IEnumerable<string> SourceFiles() =>
        Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "InnRounds"), "*.cs", SearchOption.AllDirectories)
            .Append(RepoPaths.RepoPath("Main", "Adapters", "InnRoundsAdapter.cs"));

    private static IEnumerable<(string Key, string Body)> CodeRoundTexts() =>
        SourceFiles().SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"\\{=(taom_round_[A-Za-z0-9_]+)\\}((?:\\\\.|[^\"\\\\])*)\"").Cast<Match>())
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value.Replace("\\\\", "\\")));

    [TestMethod]
    public void ThereAreThirtyMainCultures_AndEveryOneHasAShippedDrinkRow_ThatIsBeerOrWine()
    {
        var cultures = MainCultureIds().ToList();
        var drinks = ShippedDrinks();

        Assert.AreEqual(30, cultures.Count);
        foreach (var culture in cultures)
        {
            Assert.IsTrue(drinks.TryGetValue(culture, out var drink), culture + " has no cultureDrinks row");
            Assert.IsTrue(drink == "beer" || drink == "wine", culture + ": " + drink);
        }
    }

    [TestMethod]
    public void TheShippedDrinks_NameNoCultureThatDoesNotExist()
    {
        CollectionAssert.IsSubsetOf(ShippedDrinks().Keys.ToList(), MainCultureIds().ToList());
    }

    [TestMethod]
    public void EveryMainCulture_HasANamedDrinkRow_UnderTheGameTextWithItsCultureIdAsTheVariation()
    {
        foreach (var culture in MainCultureIds())
        {
            var rows = ModuleRows.Value.Where(r => r.Id == "str_taom_round_drink." + culture).ToList();
            Assert.AreEqual(1, rows.Count, culture);
            var match = Keyed.Match(rows[0].Text);
            Assert.IsTrue(match.Success, culture + " must be {=KEY}English");
            Assert.AreEqual("taom_round_drink_" + culture, match.Groups["key"].Value);
            Assert.IsTrue(match.Groups["body"].Value.Trim().Length >= 3, culture);
        }
    }

    [TestMethod]
    public void TheDrinkRows_HoldExactlyOneDot_SoTheLoaderKeepsTheWholeVariation_AndNoOtherCultures()
    {
        var rows = ModuleRows.Value.Where(r => r.Id.StartsWith("str_taom_round_drink", StringComparison.Ordinal)).ToList();

        Assert.AreEqual(MainCultureIds().Count(), rows.Count);
        foreach (var (id, _) in rows)
            Assert.AreEqual(2, id.Split('.').Length, id);
    }

    [TestMethod]
    public void TheFallbackDrinkName_IsACodeKey_AndIsRegistered()
    {
        Assert.IsTrue(CodeRoundTexts().Any(t => t.Key == "taom_round_drink_default"));
        Assert.AreEqual(1, ModuleRows.Value.Count(r => r.Id == "taom_round_drink_default"), "run tools/harvest_literal_loc_keys.py --apply");
    }

    [TestMethod]
    public void EveryRoundKeyInTheCode_IsARegisteredRow_WithTheSameEnglish()
    {
        foreach (var (key, body) in CodeRoundTexts())
        {
            var rows = ModuleRows.Value.Where(r => r.Id == key).ToList();
            Assert.AreEqual(1, rows.Count, key + " is not registered once (run tools/harvest_literal_loc_keys.py --apply)");
            Assert.AreEqual(body, Keyed.Match(rows[0].Text).Groups["body"].Value, key + ": the row and the literal drifted");
        }
    }

    [TestMethod]
    public void EveryRegisteredRoundKey_IsReferencedByTheCodeOrADrinkRow()
    {
        var inCode = CodeRoundTexts().Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (id, text) in ModuleRows.Value)
        {
            foreach (Match key in RoundKey.Matches(text))
            {
                var name = key.Groups[1].Value;
                var isDrinkRow = id.StartsWith("str_taom_round_drink.", StringComparison.Ordinal) && name.StartsWith("taom_round_drink_", StringComparison.Ordinal);
                Assert.IsTrue(isDrinkRow || inCode.Contains(name), name + " is registered but nothing references it");
            }
        }
    }

    [TestMethod]
    public void TheRoundKeys_AreUniqueAcrossTheCode_ExceptTheSameTextRepeated()
    {
        var byKey = CodeRoundTexts().GroupBy(t => t.Key).ToList();
        foreach (var group in byKey)
            Assert.AreEqual(1, group.Select(t => t.Body).Distinct().Count(), group.Key + " carries two different English texts");
        Assert.IsTrue(byKey.Count >= 10, "the round texts are gone from the code");
    }

    [TestMethod]
    public void NoRoundKey_IsInAnotherFeatures_StringsFile()
    {
        var dir = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        foreach (var file in Directory.EnumerateFiles(dir, "*strings*.xml", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(file) == "taom_module_strings.xml")
                continue;
            Assert.IsFalse(File.ReadAllText(file).Contains("{=taom_round_"), Path.GetFileName(file) + " carries a taom_round_ key");
        }
    }

    private static IEnumerable<(string Where, string Body)> AllEnglish() =>
        ModuleRows.Value.Where(r => r.Text.StartsWith("{=taom_round_", StringComparison.Ordinal))
            .Select(r => (r.Id, Keyed.Match(r.Text).Groups["body"].Value))
            .Concat(CodeRoundTexts().Select(t => (t.Key, t.Body)));

    [TestMethod]
    public void TheEnglish_NamesNoGender_NoMaleOnlyAddress_AndHasNoDash()
    {
        var gendered = new Regex(
            @"\b(lads?|boys?|sir|sirs|madam|milady|milord|lords?|ladies|lady|kings?|queens?|masters?|mistress|brothers?|gentlem[ae]n|fellows?|sons?|daughters?|he|him|his|himself|she|her|hers|herself|woman|women|sisters?|girls?|lass|lasses|maids?|wenches?|(?!human)[\w-]*(?:man|men))\b",
            RegexOptions.IgnoreCase);
        var rows = AllEnglish().ToList();

        Assert.IsTrue(rows.Count >= 30, "the round English was not found: " + rows.Count);
        foreach (var (where, body) in rows)
        {
            Assert.IsFalse(gendered.IsMatch(body), where + " names a gender: " + body);
            Assert.IsFalse(body.Contains('\u2014') || body.Contains('\u2013'), where + " has an em or en dash");
        }
    }

    [TestMethod]
    public void TheEnglish_NamesNoneOfTheListenersPeople()
    {
        // The player can be of any race: a line may place or flatter the listener, never by race, build or homeland.
        var peoples = new Regex(
            @"\b(dwarf|dwarves|dwarven|dwarvish|elf|elves|elven|elvish|orcs?|orcish|uruks?|goblins?|trolls?|humans?|hobbits?|halflings?|men|mankind|folk|kin|race|people|peoples|kind|blood|tall|short|small|big)\b|-folk\b|\w+landers?\b|\w+erners?\b",
            RegexOptions.IgnoreCase);
        foreach (var (where, body) in AllEnglish())
        {
            // The drink rows may carry a place name (a vintage), never a people; the pattern is the same for them.
            Assert.IsFalse(peoples.IsMatch(body), where + " names the listener's people: " + body);
        }
    }

    [TestMethod]
    public void TheEnglish_CarriesOnlyThePlaceholdersTheCodeFills()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "PRICE", "GOLD_ICON", "DRINK", "MORALE", "XP", "SKILL_NAME",
        };
        foreach (var (where, body) in AllEnglish())
        {
            foreach (Match name in Regex.Matches(body, @"\{([A-Z_]+)\}"))
                Assert.IsTrue(allowed.Contains(name.Groups[1].Value), where + " uses {" + name.Groups[1].Value + "}");
        }
    }

    [TestMethod]
    public void TheDrinkNames_AreShort_PlainTextWithNoPlaceholderAndNoCapitalisedPeople()
    {
        foreach (var culture in MainCultureIds())
        {
            var body = Keyed.Match(ModuleRows.Value.Single(r => r.Id == "str_taom_round_drink." + culture).Text).Groups["body"].Value;

            Assert.IsTrue(body.Length <= 24, culture + ": " + body);
            Assert.IsFalse(body.Contains('{') || body.Contains('['), culture + ": " + body);
        }
    }

    [TestMethod]
    public void ThePartyAndHouseLines_AreWholeLineVariables_AndTheKeeperRepliesAreKeyed()
    {
        var keys = CodeRoundTexts().Select(t => t.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var key in new[] { "taom_round_party", "taom_round_house", "taom_round_cap", "taom_round_party_notice", "taom_round_house_notice",
                     "taom_round_reveal_done", "taom_round_reveal_war", "taom_round_reveal_cold", "taom_round_reveal_nothing" })
            Assert.IsTrue(keys.Contains(key), key + " is missing from the code");
    }
}
