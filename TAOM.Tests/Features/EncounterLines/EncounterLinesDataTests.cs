using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// Patch149: the two shipped strings files against the matrix in docs/features/encounter-lines.md and the engine's scoring. The loader keeps only
/// the text before and after the first dot of an id, the dialog lookup stops scoring a row at its first failing tag, and
/// vanilla's robbery code needs all four lines of a variation, so each of those is pinned here. The English is held to the
/// writing rules: no gendered address, no placeholder or dash, never the listener's people, no Calradian name.
/// </summary>
[TestClass]
public class EncounterLinesDataTests
{
    internal const string LordFile = "taom_encounter_lord_strings.xml";
    internal const string KeyedFile = "taom_encounter_strings.xml";

    internal static readonly string[] Groups = { "west", "rohirrim", "northmen", "dunlendings", "southrons", "easterlings", "elves", "dwarves", "orcs" };

    /// <summary>The dark-lord groups: one lord each (Sauron, Saruman) or one family (the Nine). Their race family is none, so they have no kin rows.</summary>
    internal static readonly string[] DarkGroups = { "sauron", "saruman", "nazgul" };

    internal static readonly string[] Clans =
    {
        "dunland_raiders", "rhun_raiders", "harad_raiders", "gundabad_raiders", "umbar_corsairs",
        "gondor_soldiers", "erebor_warriors", "mirkwood_stalkers", "feral_orcs", "looters",
    };

    private static readonly string[] AllLordBuckets = { "liege", "ally", "rival", "foe", "kin" };
    private static readonly string[] WarBuckets = { "rival", "foe", "kin" };
    private static readonly string[] DarkLordBuckets = { "liege", "ally", "rival", "foe" };
    private static readonly string[] DarkWarBuckets = { "rival", "foe" };
    private static readonly string[] TradeBuckets = { "kin", "friend", "foe" };
    private static readonly string[] BanditBuckets = { "kin", "side", "foe" };
    private static readonly string[] RobberyKinds = { "threat", "pay_agreement", "conclusion", "start_fight" };

    /// <summary>
    /// The liege beats and the text variables of the vanilla line each replaces, which its rows must carry exactly (a missing
    /// one loses the player's name or the award; an extra one shows unresolved). Vanilla's gender switch is left out on purpose.
    /// </summary>
    internal static readonly Dictionary<string, string[]> LiegeBeats = new()
    {
        ["leave_ask"] = new[] { "PLAYER.NAME" },
        ["leave_confirm"] = new string[0],
        ["leave_granted"] = new[] { "PLAYER.NAME" },
        ["leave_farewell"] = new[] { "PLAYER.NAME" },
        ["merc_end"] = new string[0],
        ["merc_end_final"] = new string[0],
        ["join_how"] = new string[0],
        ["vassal_low"] = new[] { "PLAYER.NAME" },
        ["vassal_reject"] = new[] { "PLAYER.NAME" },
        ["vassal_accept"] = new[] { "PLAYER.NAME" },
        ["vassal_bond"] = new string[0],
        ["vassal_welcome"] = new[] { "PLAYER.NAME" },
        ["merc_pitch"] = new[] { "MERCENARY_AWARD", "GOLD_ICON" },
        ["merc_accept"] = new[] { "KINGDOM_FORMAL_NAME" },
        ["merc_decline"] = new string[0],
    };

    /// <summary>The beats a realm's ruler speaks in the realm's own words (<c>LiegeLines.RealmTexts</c>).</summary>
    internal static readonly string[] RealmBeats = { "leave_ask", "leave_granted", "vassal_accept", "merc_accept" };

    private static readonly Regex Variable = new(@"\{(?<name>[A-Z_.]+)\}");

    /// <summary>Every kingdom id the shipped data defines: the vanilla kingdoms spkingdoms.xslt rewrites and the kingdoms of taom_spkingdoms.xml.</summary>
    internal static List<string> RealmIds()
    {
        var vanilla = Regex.Matches(File.ReadAllText(ModuleData("spkingdoms.xslt")), @"match=""Kingdom\[@id='(?<id>[^']+)'\]""")
            .Cast<Match>().Select(m => m.Groups["id"].Value);
        var taom = XDocument.Load(ModuleData("taom_spkingdoms.xml")).Descendants("Kingdom").Select(k => (string)k.Attribute("id")!);
        return vanilla.Concat(taom).Distinct().ToList();
    }

    private static bool IsLiege(Row row) => row.TextId.StartsWith("str_taom_liege_", StringComparison.Ordinal);

    /// <summary>A lord row's text id and the short name its key uses.</summary>
    private static readonly Dictionary<string, string> LordShort = new()
    {
        ["str_context_line"] = "context_line",
        ["str_surrender_demand_hero"] = "surrender_demand",
        ["str_surrender_offer"] = "surrender_offer",
        ["str_prisoner_released"] = "prisoner_released",
        ["hero_leave"] = "hero_leave",
    };

    internal sealed record Row(string File, string Id, string Key, string Body, IReadOnlyList<(string Name, int Weight)> Tags)
    {
        public string TextId => Id.Split('.')[0];

        public string Variation => Id.Split('.').Last();
    }

    private static readonly Regex Keyed = new(@"^\{=(?<key>[^}]+)\}(?<body>.*)$", RegexOptions.Singleline);

    internal static string ModuleData(string file) => RepoPaths.RepoPath("Main", "_Module", "ModuleData", file);

    internal static List<Row> Load(string file)
    {
        var path = ModuleData(file);
        Assert.IsTrue(File.Exists(path), file + " is missing from ModuleData");

        return XDocument.Load(path).Descendants("string").Select(e =>
        {
            var m = Keyed.Match((string?)e.Attribute("text") ?? "");
            var tags = e.Elements("tags").Elements("tag")
                .Select(t => ((string?)t.Attribute("tag_name") ?? "", int.TryParse((string?)t.Attribute("weight"), out var w) ? w : 1))
                .ToList();
            return new Row(file, (string?)e.Attribute("id") ?? "", m.Success ? m.Groups["key"].Value : "", m.Success ? m.Groups["body"].Value : "", tags);
        }).ToList();
    }

    private static readonly Lazy<List<Row>> LordRows = new(() => Load(LordFile));
    private static readonly Lazy<List<Row>> KeyedRows = new(() => Load(KeyedFile));

    private static IEnumerable<Row> AllRows => LordRows.Value.Concat(KeyedRows.Value);

    /// <summary>Every id the spec asks for, with the key the generator gives it.</summary>
    internal static Dictionary<string, string> ExpectedIds()
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string textId, string variation, string shortName, string keyVariation) =>
            ids.Add(textId + "." + variation, "taom_enc_" + shortName + "_" + keyVariation);

        void AddLordRows(string g, string[] buckets, string[] warBuckets)
        {
            foreach (var b in buckets)
            {
                foreach (var when in new[] { "first", "met" })
                    Add("str_context_line", $"taom_{g}_{b}_{when}", "context_line", $"{g}_{b}_{when}");
                Add("hero_leave", $"taom_{g}_{b}", "hero_leave", $"{g}_{b}");
            }
            foreach (var b in warBuckets)
            {
                foreach (var n in new[] { 1, 2 })
                    Add("str_surrender_demand_hero", $"taom_{g}_{b}_{n}", "surrender_demand", $"{g}_{b}_{n}");
                Add("str_surrender_offer", $"taom_{g}_{b}", "surrender_offer", $"{g}_{b}");
                Add("str_prisoner_released", $"taom_{g}_{b}", "prisoner_released", $"{g}_{b}");
            }
        }

        foreach (var g in DarkGroups)
            AddLordRows(g, DarkLordBuckets, DarkWarBuckets);
        foreach (var g in Groups)
        {
            AddLordRows(g, AllLordBuckets, WarBuckets);
            foreach (var b in TradeBuckets)
            {
                foreach (var beat in new[] { "caravan_greet", "caravan_defy", "villager_greet", "villager_defy" })
                    Add("str_taom_enc_" + beat, $"{g}_{b}", beat, $"{g}_{b}");
            }
        }
        foreach (var c in Clans)
        {
            foreach (var b in BanditBuckets)
            {
                foreach (var n in new[] { 1, 2 })
                {
                    foreach (var kind in RobberyKinds)
                        Add("str_robbery_" + kind, $"{c}_{b}_{n}", "robbery_" + kind, $"{c}_{b}_{n}");
                    Add("str_taom_enc_bandit_greet", $"{c}_{b}_{n}", "bandit_greet", $"{c}_{b}_{n}");
                }
                // Looters never hold a hideout (BanditSpawnCampaignBehavior spawns them on the map), so they have no boss rows.
                if (c != "looters")
                    Add("str_taom_enc_hideout_boss", $"{c}_{b}", "hideout_boss", $"{c}_{b}");
            }
        }
        foreach (var beat in LiegeBeats.Keys)
        {
            foreach (var g in Groups.Concat(DarkGroups))
                Add("str_taom_liege_" + beat, "group_" + g, "liege_" + beat, "group_" + g);
            if (RealmBeats.Contains(beat))
            {
                foreach (var realm in RealmIds())
                    Add("str_taom_liege_" + beat, "realm_" + realm, "liege_" + beat, "realm_" + realm);
            }
        }
        return ids;
    }

    [TestMethod]
    public void TheSpecMatrix_Is1038Ids_303ForLords_435KeyedEncounterRows_And300LiegeRows()
    {
        var ids = ExpectedIds();

        Assert.AreEqual(30, RealmIds().Count, "a kingdom was added or removed: give it its realm lines");
        Assert.AreEqual(1038, ids.Count);
        Assert.AreEqual(300, ids.Keys.Count(k => k.StartsWith("str_taom_liege_", StringComparison.Ordinal)));
        Assert.AreEqual(303, ids.Keys.Count(k => k.Contains(".taom_")));
        Assert.AreEqual(60, ids.Keys.Count(k => DarkGroups.Any(g => k.Contains(".taom_" + g + "_"))));
    }

    [TestMethod]
    public void TheTwoFiles_HoldExactlyTheSpecIds_WithTheGeneratorsKeys()
    {
        var expected = ExpectedIds();
        var actual = AllRows.ToList();

        var missing = expected.Keys.Except(actual.Select(r => r.Id)).ToList();
        var extra = actual.Select(r => r.Id).Except(expected.Keys).ToList();
        Assert.AreEqual(0, missing.Count, "missing: " + string.Join(", ", missing.Take(20)));
        Assert.AreEqual(0, extra.Count, "extra: " + string.Join(", ", extra.Take(20)));
        Assert.AreEqual(1038, actual.Count, "an id appears twice");
        Assert.AreEqual(303, LordRows.Value.Count);
        Assert.AreEqual(735, KeyedRows.Value.Count);
        foreach (var row in actual)
            Assert.AreEqual(expected[row.Id], row.Key, row.Id + " key");
    }

    [TestMethod]
    public void EveryId_HasExactlyOneDot_SoTheLoaderKeepsTheWholeVariation()
    {
        foreach (var row in AllRows)
        {
            var parts = row.Id.Split('.');
            Assert.AreEqual(2, parts.Length, row.Id + " must be <textId>.<variation> with no further dot");
            Assert.IsTrue(parts[1].Length > 0 && !parts[1].Contains(' '), row.Id);
        }
    }

    [TestMethod]
    public void EveryRobberyVariation_HasAllFourRows()
    {
        var ids = KeyedRows.Value.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var c in Clans)
        {
            foreach (var b in BanditBuckets)
            {
                foreach (var n in new[] { 1, 2 })
                {
                    foreach (var kind in RobberyKinds)
                        Assert.IsTrue(ids.Contains($"str_robbery_{kind}.{c}_{b}_{n}"), $"str_robbery_{kind}.{c}_{b}_{n}");
                }
            }
        }
    }

    [TestMethod]
    public void NoRobberyVariation_LooksLikeVanillasOrTheLegacyClanVariation()
    {
        // Vanilla asks for <clan>_<k> with k from 1 to 6; a TAOM variation of that shape would join vanilla's own pool.
        foreach (var row in KeyedRows.Value.Where(r => r.TextId.StartsWith("str_robbery_", StringComparison.Ordinal)))
        {
            var clan = Clans.Single(c => row.Variation.StartsWith(c + "_", StringComparison.Ordinal));
            var rest = row.Variation.Substring(clan.Length + 1);
            Assert.IsFalse(Regex.IsMatch(rest, @"^[1-6]$"), row.Id + " has vanilla's variation shape");
        }
    }

    [TestMethod]
    public void EveryRow_CarriesATaomEncKey_UsedOnce_WithEnglish()
    {
        var keys = new List<string>();
        foreach (var row in AllRows)
        {
            StringAssert.StartsWith(row.Key, "taom_enc_", row.Id + " key");
            Assert.IsTrue(row.Body.Trim().Length > 10, row.Id + " has no English");
            keys.Add(row.Key);
        }

        Assert.AreEqual(keys.Count, keys.Distinct(StringComparer.Ordinal).Count(), "a key shared by two rows shows one line for both");
    }

    [TestMethod]
    public void NoOtherShippedStringsFile_UsesAnEncounterKey()
    {
        var dir = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name == LordFile || name == KeyedFile)
                continue;
            Assert.IsFalse(File.ReadAllText(file).Contains("{=taom_enc_"), name + " carries a taom_enc_ key");
        }
    }

    [TestMethod]
    public void TheEnglish_HasNoGenderedWord_NoPlaceholder_NoTag_AndNoDash()
    {
        var gendered = new Regex(@"\b(lads?|boys?|sir|sirs|brothers?|gentlemen|fellows?|sons?|he|him|his|himself|woman|women|lady|ladies|she|her|hers|herself|sisters?|girls?|lass|lasses|maids?|(?!human)[\w-]*(?:man|men))\b", RegexOptions.IgnoreCase);
        foreach (var row in AllRows)
        {
            var body = IsLiege(row) ? Variable.Replace(row.Body, "") : row.Body;
            Assert.IsFalse(gendered.IsMatch(body), row.Id + " names a gender: " + row.Body);
            Assert.IsFalse(body.IndexOfAny(new[] { '{', '}', '[', ']' }) >= 0, row.Id + " carries a placeholder or a tag: " + row.Body);
            Assert.IsFalse(row.Body.Contains('—') || row.Body.Contains('–'), row.Id + " has an em or en dash");
        }
    }

    [TestMethod]
    public void EveryLiegeRow_CarriesExactlyTheTextVariablesOfTheVanillaLineItReplaces()
    {
        var liege = KeyedRows.Value.Where(IsLiege).ToList();
        Assert.AreEqual(300, liege.Count);
        foreach (var row in liege)
        {
            var beat = row.TextId.Substring("str_taom_liege_".Length);
            var found = Variable.Matches(row.Body).Cast<Match>().Select(m => m.Groups["name"].Value).Distinct().ToList();
            CollectionAssert.AreEquivalent(LiegeBeats[beat], found, row.Id + ": " + row.Body);
        }
    }

    [TestMethod]
    public void TheLiegeBeats_AreTheTextsTheServiceAsksFor()
    {
        CollectionAssert.AreEquivalent(
            LiegeBeats.Keys.Select(b => "str_taom_liege_" + b).ToList(),
            TAOM.Features.EncounterLines.LiegeLines.TextOfSentence.Values.Distinct().ToList());
        CollectionAssert.AreEquivalent(
            RealmBeats.Select(b => "str_taom_liege_" + b).ToList(),
            TAOM.Features.EncounterLines.LiegeLines.RealmTexts.ToList());
    }

    [TestMethod]
    public void NoLine_NamesTheListenersPeople()
    {
        var outsider = new Regex(
            @"-folk\b|\bso (?:tall|short|small|big)\b|soft-?skin|\w+landers?\b|\w+erners?\b|\b(?:green|western|eastern|northern|southern) lands\b|\b(?:your|you) (?:kind|people|race|folk)\b|\blandsm[ae]n\b|\btarks?\b",
            RegexOptions.IgnoreCase);
        foreach (var row in AllRows)
            Assert.IsFalse(outsider.IsMatch(row.Body), row.Id + " names the listener's people: " + row.Body);
    }

    [TestMethod]
    public void NoLine_UsesACalradianName()
    {
        var calradia = new Regex(@"\b(empire|sturgi\w*|vlandi\w*|battani\w*|aserai|khuzait\w*|calradi\w*|senate|denars?)\b", RegexOptions.IgnoreCase);
        foreach (var row in AllRows)
            Assert.IsFalse(calradia.IsMatch(row.Body), row.Id + " uses a Calradian name: " + row.Body);
    }

    [TestMethod]
    public void EveryLordRow_StartsWithItsSpeakerTagThenItsBucketTag_BothWeight100()
    {
        var variation = new Regex(@"^taom_(?<group>[a-z]+)_(?<bucket>liege|ally|rival|foe|kin)(?:_(?:first|met|1|2))?$");
        foreach (var row in LordRows.Value)
        {
            var m = variation.Match(row.Variation);
            Assert.IsTrue(m.Success, row.Id + " variation shape");
            Assert.IsTrue(row.Tags.Count >= 2, row.Id + " needs a speaker and a bucket tag");
            Assert.AreEqual(("taom_speaker_" + m.Groups["group"].Value, 100), row.Tags[0], row.Id + " first tag");
            Assert.AreEqual(("taom_player_" + m.Groups["bucket"].Value, 100), row.Tags[1], row.Id + " second tag");
        }
    }

    [TestMethod]
    public void TheTailTags_FollowTheBeat()
    {
        foreach (var row in LordRows.Value)
        {
            var tail = row.Tags.Skip(2).ToList();
            var expected = new List<(string, int)>();
            if (row.TextId == "str_context_line")
                expected.Add((row.Variation.EndsWith("_first", StringComparison.Ordinal) ? "FirstMeetingTag" : "MetBeforeTag", 1));
            if (row.TextId == "str_surrender_demand_hero")
                expected.Add(("taom_alt_line", row.Variation.EndsWith("_2", StringComparison.Ordinal) ? 1 : -1));
            if (row.TextId == "str_surrender_offer")
                expected.Add(("CombatantTag", 1));
            if (row.TextId == "str_context_line" || row.TextId == "hero_leave")
                expected.Add(("RomanticallyInvolvedTag", -1));
            if ((row.TextId == "str_context_line" || row.TextId == "hero_leave") && row.Variation.Contains("_kin"))
                expected.Add(("PlayerIsEnemyTag", -1));

            CollectionAssert.AreEqual(expected, tail, row.Id + " tags after the bucket: " + string.Join(", ", tail));
        }
    }

    [TestMethod]
    public void EveryKinGreetingAndFarewell_IsWrittenForPeace_SoItFailsAtWar()
    {
        // The kin bucket also wins at war on the same side (EncounterBuckets.Lord), and these 27 rows are peace text.
        // PlayerIsEnemyTag weight -1 drops the row when the speaker's realm is at war with the player; vanilla's war
        // greeting and farewell then show.
        var kin = LordRows.Value
            .Where(r => (r.TextId == "str_context_line" || r.TextId == "hero_leave") && r.Variation.Contains("_kin"))
            .ToList();

        Assert.AreEqual(27, kin.Count);
        foreach (var row in kin)
            CollectionAssert.Contains(row.Tags.ToList(), ("PlayerIsEnemyTag", -1), row.Id);
        Assert.AreEqual(0, LordRows.Value.Except(kin).Count(r => r.Tags.Any(t => t.Name == "PlayerIsEnemyTag")),
            "a row outside the kin greetings and farewells carries PlayerIsEnemyTag");
    }

    [TestMethod]
    public void EverySurrenderDemandPair_HasOneRowPassingOnEachDay()
    {
        foreach (var one in LordRows.Value.Where(r => r.TextId == "str_surrender_demand_hero" && r.Variation.EndsWith("_1", StringComparison.Ordinal)))
        {
            var twoId = one.Id.Substring(0, one.Id.Length - 1) + "2";
            var two = LordRows.Value.Single(r => r.Id == twoId);
            Assert.AreEqual(-1, one.Tags.Single(t => t.Name == "taom_alt_line").Weight, one.Id);
            Assert.AreEqual(1, two.Tags.Single(t => t.Name == "taom_alt_line").Weight, two.Id);
        }
    }

    [TestMethod]
    public void LiegeAndAllyRows_AreOnlyGreetingsAndPartings()
    {
        foreach (var row in LordRows.Value.Where(r => r.Variation.Contains("_liege") || r.Variation.Contains("_ally")))
            Assert.IsTrue(row.TextId == "str_context_line" || row.TextId == "hero_leave", row.Id + " is a war beat for a liege or an ally");
    }

    [TestMethod]
    public void TheKeyedRows_CarryNoTags()
    {
        foreach (var row in KeyedRows.Value)
            Assert.AreEqual(0, row.Tags.Count, row.Id + " is read by id and variation, a tag would only confuse the lookup");
    }

    [TestMethod]
    public void TheLordRowIds_AreVanillaIds_TheEngineAsksFor()
    {
        CollectionAssert.AreEquivalent(LordShort.Keys.ToList(), LordRows.Value.Select(r => r.TextId).Distinct().ToList());
    }
}
