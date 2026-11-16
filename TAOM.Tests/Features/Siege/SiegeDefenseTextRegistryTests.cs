using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using TAOM.Features.Siege;
using TAOM.Features.Siege.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Siege;

/// <summary>
/// The siege defense popup shows the English of siege_defense_config.json under keys the service
/// composes (SiegeDefenseText.KeyFor), and a translation reaches the player only through a row
/// registered under that key (#1385). These tests keep taom_module_strings.xml equal to the JSON and
/// to the default literals: a changed or added kingdom text fails here and names the row to write.
/// </summary>
[TestClass]
public class SiegeDefenseTextRegistryTests
{
    private const string Prefix = "taom_siege_defense_";

    private static readonly string[] Fields =
    {
        SiegeDefenseText.Title, SiegeDefenseText.Body, SiegeDefenseText.AcceptButton,
        SiegeDefenseText.AcceptMessage, SiegeDefenseText.RewardMessage
    };

    private static readonly string[] Literals =
    {
        SiegeDefenseText.DefaultTitle, SiegeDefenseText.DefaultBody, SiegeDefenseText.DefaultAcceptButton,
        SiegeDefenseText.DefaultAcceptMessage, SiegeDefenseText.DefaultRewardMessage, SiegeDefenseText.Ignore
    };

    private static SiegeDefenseConfig Config() =>
        JsonConvert.DeserializeObject<SiegeDefenseConfig>(File.ReadAllText(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData", "siege", "siege_defense_config.json")))
        ?? throw new AssertFailedException("siege_defense_config.json deserialized to null");

    /// <summary>id to text of every siege defense row in the English registry.</summary>
    private static Dictionary<string, string> Rows() =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml"))
            .Descendants("string")
            .Where(s => ((string?)s.Attribute("id") ?? "").StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(s => (string)s.Attribute("id")!, s => (string?)s.Attribute("text") ?? "");

    private static string? FieldOf(KingdomSiegeMessages entry, string field) => field switch
    {
        SiegeDefenseText.Title => entry.Title,
        SiegeDefenseText.Body => entry.Body,
        SiegeDefenseText.AcceptButton => entry.AcceptButton,
        SiegeDefenseText.AcceptMessage => entry.AcceptMessage,
        SiegeDefenseText.RewardMessage => entry.RewardMessage,
        _ => throw new AssertFailedException("unknown field " + field)
    };

    private static string KeyOf(string literal) => literal.Substring(2, literal.IndexOf('}') - 2);

    /// <summary>key to the text its row must hold: each configured kingdom field, each default, Ignore.</summary>
    private static Dictionary<string, string> Expected()
    {
        var expected = new Dictionary<string, string>();
        foreach (var kingdom in Config().KingdomMessages)
        {
            if (kingdom.Value == null)
                continue;
            foreach (var field in Fields)
            {
                var english = FieldOf(kingdom.Value, field);
                if (string.IsNullOrEmpty(english))
                    continue;
                var key = SiegeDefenseText.KeyFor(kingdom.Key, field);
                expected[key] = "{=" + key + "}" + english;
            }
        }
        foreach (var literal in Literals)
            expected[KeyOf(literal)] = literal;
        return expected;
    }

    [TestMethod]
    public void Registry_HoldsTheShownEnglishUnderEveryKey()
    {
        var rows = Rows();
        var wrong = Expected()
            .Where(e => !rows.TryGetValue(e.Key, out var text) || text != e.Value)
            .Select(e => $"{e.Key}: needs text=\"{e.Value}\", has " +
                         (rows.TryGetValue(e.Key, out var text) ? $"\"{text}\"" : "no row"))
            .ToList();

        Assert.AreEqual(0, wrong.Count,
            "taom_module_strings.xml is out of step with siege_defense_config.json or SiegeDefenseText:\n" +
            string.Join("\n", wrong));
    }

    [TestMethod]
    public void Registry_HoldsNoSiegeRowThatNothingShows()
    {
        var expected = Expected();
        var stale = Rows().Keys.Where(id => !expected.ContainsKey(id)).OrderBy(id => id).ToList();

        Assert.AreEqual(0, stale.Count,
            "Rows that no kingdom entry or default shows; remove each with its 12 language rows: " +
            string.Join(", ", stale));
    }

    [TestMethod]
    public void KingdomIds_AreKeySafe_AndNoneIsTheDefaultsId()
    {
        var kingdoms = Config().KingdomMessages.Keys.ToList();
        var bad = kingdoms.Where(id => id == "default" || !Regex.IsMatch(id, "^[a-z0-9_]+$")).ToList();

        Assert.IsTrue(kingdoms.Count > 0, "siege_defense_config.json has no kingdom entries: the read is broken.");
        Assert.AreEqual(0, bad.Count,
            "Kingdom ids a key cannot carry, or the id the defaults' rows use: " + string.Join(", ", bad));
    }
}
