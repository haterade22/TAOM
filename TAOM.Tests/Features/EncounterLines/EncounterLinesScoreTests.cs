using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TAOM.Composition;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// Patch149's lord rows win by score: <c>ConversationManager.FindMatchingTextOrNull</c> adds the absolute weight of every tag
/// on a row whose tags all pass (DefaultTag adds 0) and keeps the first row with the strictly highest sum. These tests read
/// the installed vanilla data and the engine's tag classes.
/// </summary>
[TestClass]
public class EncounterLinesScoreTests
{
    /// <summary>The highest score of vanilla's code variations of <c>hero_leave</c> (spouse 10 + 2, LordConversationsCampaignBehavior).</summary>
    private const int VanillaHeroLeaveCodeScore = 12;

    private static readonly string[] VanillaModules = { "Native", "SandBoxCore", "SandBox", "StoryMode", "NavalDLC" };

    private static string GameDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
    }

    private static int Score(IEnumerable<(string Name, int Weight)> tags) =>
        tags.Where(t => t.Name != "DefaultTag").Sum(t => Math.Abs(t.Weight));

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheLowestTaomScorePerId_BeatsTheHighestVanillaScore()
    {
        var modules = Path.Combine(GameDir(), "Modules");
        if (!Directory.Exists(Path.Combine(modules, "SandBox", "ModuleData")))
            Assert.Inconclusive("Vanilla modules not found under " + modules);

        var ours = EncounterLinesDataTests.Load(EncounterLinesDataTests.LordFile)
            .GroupBy(r => r.TextId)
            .ToDictionary(g => g.Key, g => g.Min(r => Score(r.Tags)));
        var vanilla = ours.Keys.ToDictionary(k => k, _ => 0);
        var scanned = 0;
        foreach (var module in VanillaModules)
        {
            var data = Path.Combine(modules, module, "ModuleData");
            if (!Directory.Exists(data))
                continue;
            foreach (var file in Directory.EnumerateFiles(data, "*.xml", SearchOption.AllDirectories))
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(file);
                }
                catch (System.Xml.XmlException)
                {
                    continue;
                }
                foreach (var row in doc.Descendants("string"))
                {
                    var textId = ((string?)row.Attribute("id") ?? "").Split('.')[0];
                    if (!vanilla.ContainsKey(textId))
                        continue;
                    scanned++;
                    var score = Score(row.Elements("tags").Elements("tag")
                        .Select(t => ((string?)t.Attribute("tag_name") ?? "", int.TryParse((string?)t.Attribute("weight"), out var w) ? w : 1)));
                    vanilla[textId] = Math.Max(vanilla[textId], score);
                }
            }
        }
        vanilla["hero_leave"] = Math.Max(vanilla["hero_leave"], VanillaHeroLeaveCodeScore);

        Assert.IsTrue(scanned > 10, "found only " + scanned + " vanilla rows for the five ids; the scan shape changed");
        foreach (var pair in ours)
            Assert.IsTrue(pair.Value > vanilla[pair.Key], $"{pair.Key}: lowest TAOM score {pair.Value} does not beat vanilla's {vanilla[pair.Key]}");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryTagNameInTheLordRows_IsATaomOrVanillaConversationTag()
    {
        var types = AccessTools.GetTypesFromAssembly(typeof(ConversationTag).Assembly)
            .Concat(AccessTools.GetTypesFromAssembly(typeof(FeatureModules).Assembly))
            .Where(t => t.IsSubclassOf(typeof(ConversationTag)) && !t.IsAbstract);
        var known = new HashSet<string>(types.Select(t => ((ConversationTag)Activator.CreateInstance(t)).StringId), StringComparer.Ordinal);
        Assert.IsTrue(known.Count > 10, "found no conversation tags; the scan shape changed");

        var wanted = EncounterLinesDataTests.Load(EncounterLinesDataTests.LordFile)
            .SelectMany(r => r.Tags.Select(t => t.Name)).Distinct().ToList();
        Assert.AreEqual(9 + 5 + 1 + 5, wanted.Count, "tag names in the lord rows: " + string.Join(", ", wanted));
        foreach (var name in wanted)
            Assert.IsTrue(known.Contains(name), name + " is no ConversationTag StringId: the engine asserts and the row never matches");
    }
}
