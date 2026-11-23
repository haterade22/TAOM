using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// The size of each upgrade step in the shipped troop trees (<c>troops/*.xml</c>). A tier is five levels;
/// the generated realm trees take two tiers in one step (ten levels), so no step is larger than that.
/// </summary>
[TestClass]
public class TroopUpgradeStepDataTests
{
    private const int MaxLevelStep = 10;

    private static (Dictionary<string, int> Levels, List<(string From, string To)> Steps) ReadTrees()
    {
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        var steps = new List<(string, string)>();
        var folder = Path.Combine(TAOM.Tests.Core.CultureDataFixture.ModuleDataPath(), "troops");
        foreach (var file in Directory.GetFiles(folder, "*.xml"))
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string)npc.Attribute("id");
                if (string.IsNullOrEmpty(id)) continue;
                levels[id] = int.TryParse((string)npc.Attribute("level"), out var level) ? level : 1;
                foreach (var target in npc.Descendants("upgrade_target"))
                    steps.Add((id, ((string)target.Attribute("id") ?? string.Empty).Replace("NPCCharacter.", string.Empty)));
            }
        }
        return (levels, steps);
    }

    [TestMethod]
    public void UpgradeSteps_ShippedTrees_NeverSkipMoreThanTwoTiers()
    {
        var (levels, steps) = ReadTrees();
        Assert.IsTrue(steps.Count > 500, $"only {steps.Count} upgrade steps read");

        var jumps = steps.Where(s => levels.ContainsKey(s.To) && levels[s.To] - levels[s.From] > MaxLevelStep)
                         .Select(s => $"{s.From} ({levels[s.From]}) -> {s.To} ({levels[s.To]})").ToList();

        Assert.AreEqual(0, jumps.Count, "an upgrade that skips tiers: " + string.Join(", ", jumps));
    }
}
