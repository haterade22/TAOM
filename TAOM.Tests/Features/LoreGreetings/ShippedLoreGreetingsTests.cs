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
using TAOM.Features.LoreGreetings;
using TAOM.Features.LoreGreetings.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LoreGreetings;

/// <summary>
/// The shipped <c>lore_greetings.json</c> against the shipped characters: it loads with no warning, every character id is a hero
/// of the repo's ModuleData (<c>characters/*.xml</c>, <c>named_companions.xml</c>, or a lord that <c>lords.xslt</c> rewrites), each person's gender is its
/// characters' gender, none of the Nine speaks a pair line (haterade22's Nazgûl greeting row stays unbeaten, docs/features/lore-greetings.md
/// "Priority"), and every line keeps the writing rules.
/// </summary>
[TestClass]
public class ShippedLoreGreetingsTests
{
    private static readonly string ModuleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static readonly Regex XsltLord = new(
        @"<xsl:template match=""NPCCharacter\[@id='(?<id>[^']+)'\]"">(?<body>.*?)</xsl:template>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static (LoreGreetingTable Table, IModLogger Logger) Load()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var logger = Substitute.For<IModLogger>();
        return (new LoreGreetingsConfigProvider(paths, logger).GetTable(), logger);
    }

    private sealed record ShippedCharacter(bool IsFemale, string Race);

    private static Dictionary<string, ShippedCharacter> Characters()
    {
        var all = new Dictionary<string, ShippedCharacter>(StringComparer.Ordinal);
        var files = Directory.GetFiles(Path.Combine(ModuleData, "characters"), "*.xml")
            .Append(Path.Combine(ModuleData, "named_companions", "named_companions.xml"));
        foreach (var file in files)
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string?)npc.Attribute("id");
                if (string.IsNullOrEmpty(id) || (string?)npc.Attribute("is_template") == "true")
                    continue;
                all[id!] = new ShippedCharacter((string?)npc.Attribute("is_female") == "true", (string?)npc.Attribute("race") ?? "human");
            }
        }

        foreach (Match lord in XsltLord.Matches(File.ReadAllText(Path.Combine(ModuleData, "lords.xslt"))))
        {
            var body = lord.Groups["body"].Value;
            if (!body.Contains("<xsl:attribute name=\"name\">"))
                continue;
            var race = Regex.Match(body, @"<xsl:attribute name=""race"">([^<]+)<").Groups[1].Value;
            all[lord.Groups["id"].Value] = new ShippedCharacter(body.Contains("<xsl:attribute name=\"is_female\">true<"),
                race.Length == 0 ? "human" : race);
        }

        Assert.IsTrue(all.Count > 1000, "Only " + all.Count + " characters parsed: the parse is broken.");
        return all;
    }

    private static IEnumerable<(string Pair, string Line)> Lines(LoreGreetingTable table) =>
        table.Pairs.SelectMany(p => new[] { p.Lines.Text, p.Lines.Hostile, p.Lines.First }
            .Where(l => l != null)
            .Select(l => (p.Speaker + ">" + p.Listener, l!)));

    [TestMethod]
    public void ShippedFile_LoadsEveryRow_WithNoWarning()
    {
        var (table, logger) = Load();

        Assert.IsTrue(table.PairCount > 0, "no greeting loaded");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void EveryCharacterId_IsAHeroOfTheShippedModuleData()
    {
        var characters = Characters();
        var missing = Load().Table.Persons.SelectMany(p => p.Characters.Where(c => !characters.ContainsKey(c)).Select(c => p.Id + ":" + c)).ToList();

        Assert.AreEqual(0, missing.Count, "unknown character ids: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryPersonsGender_IsTheGenderOfItsCharacters()
    {
        var characters = Characters();
        var wrong = Load().Table.Persons
            .SelectMany(p => p.Characters.Where(c => characters.TryGetValue(c, out var ch) && ch.IsFemale != p.IsFemale).Select(c => p.Id + ":" + c))
            .ToList();

        Assert.AreEqual(0, wrong.Count, "gender differs: " + string.Join(", ", wrong));
    }

    [TestMethod]
    public void NoneOfTheNine_SpeaksAPairLine()
    {
        var characters = Characters();
        var table = Load().Table;
        var nine = table.Persons
            .Where(p => p.Characters.Any(c => characters.TryGetValue(c, out var ch) && ch.Race == "nazghul"))
            .Select(p => p.Id)
            .ToList();
        Assert.IsTrue(nine.Count > 0, "no Nazgûl person in the table, so this test proves nothing");

        var spoken = table.Pairs.Where(p => nine.Contains(p.Speaker)).Select(p => p.Speaker + ">" + p.Listener).ToList();

        Assert.AreEqual(0, spoken.Count, "lines spoken by the Nine: " + string.Join(", ", spoken));
    }

    [TestMethod]
    public void EveryPersonIsInAtLeastOnePair()
    {
        var table = Load().Table;
        var used = new HashSet<string>(table.Pairs.SelectMany(p => new[] { p.Speaker, p.Listener }));

        var idle = table.Persons.Where(p => !used.Contains(p.Id)).Select(p => p.Id).ToList();

        Assert.AreEqual(0, idle.Count, "persons with no greeting: " + string.Join(", ", idle));
    }

    [TestMethod]
    public void EveryLine_IsUnique()
    {
        var repeated = Lines(Load().Table).GroupBy(l => l.Line, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .Select(g => string.Join(" and ", g.Select(l => l.Pair)) + ": " + g.Key)
            .ToList();

        Assert.AreEqual(0, repeated.Count, "lines said twice: " + string.Join("; ", repeated));
    }

    [TestMethod]
    public void EveryLine_KeepsTheWritingRules()
    {
        var bad = new List<string>();
        foreach (var (pair, line) in Lines(Load().Table))
        {
            if (line.IndexOf('—') >= 0 || line.IndexOf('–') >= 0)
                bad.Add(pair + " has an em or en dash");
            if (line.Contains("  ") || line.Contains("\""))
                bad.Add(pair + " has a double space or a double quote");
            if (line.Length > 160)
                bad.Add(pair + " is " + line.Length + " characters long");
            if (!(line.EndsWith(".") || line.EndsWith("!") || line.EndsWith("?")))
                bad.Add(pair + " does not end a sentence");
        }

        Assert.AreEqual(0, bad.Count, string.Join("; ", bad));
    }
}
