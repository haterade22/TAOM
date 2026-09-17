using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// Pins the level rule for <c>troop_weights.xml</c> (#585): a weighted troop at level 41 or above
/// pays at least 3.0, and below that band nothing pays 3.0 except the mount packages. The file is a
/// flat per-id lookup with no level table, so nothing but this test connects a row's weight to the
/// troop's <c>level=</c>; without it a new L46 capstone added at 2.0 reads exactly like the rest of
/// the file.
/// </summary>
[TestClass]
public class TroopWeightLevelBandTests
{
    private const int HeavyBandFloorLevel = 41;
    private const float HeavyBandWeight = 3.0f;

    /// <summary>
    /// Mount packages price the creature plus its crew, not the rider's tier, so they sit outside
    /// the level rule. <c>taom_spider_creature</c> is not a troop and resolves to no level anyway;
    /// it is listed so the exemption reads complete.
    /// </summary>
    private static readonly HashSet<string> MountPackages = new(StringComparer.Ordinal)
    {
        "harad_elephant_rider",
        "taom_spider_creature",
        "taom_spider_rider_brown",
        "taom_spider_rider_pale",
    };

    private static Dictionary<string, float> _weights = null!;
    private static Dictionary<string, int> _levels = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        var moduleData = CultureDataFixture.ModuleDataPath();

        _weights = XDocument.Load(Path.Combine(moduleData, "TroopWeights", "troop_weights.xml"))
            .Descendants("TroopWeight")
            .ToDictionary(
                e => (string)e.Attribute("id")!,
                e => float.Parse((string)e.Attribute("weight")!, CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

        _levels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(moduleData, "troops"), "troops_*.xml"))
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string?)npc.Attribute("id");
                var level = (int?)npc.Attribute("level");
                if (id != null && level != null)
                    _levels[id] = level.Value;
            }
        }
    }

    private static IEnumerable<(string Id, float Weight, int Level)> ResolvedRows() =>
        _weights
            .Where(kv => _levels.ContainsKey(kv.Key))
            .Select(kv => (kv.Key, kv.Value, _levels[kv.Key]));

    private static string Describe((string Id, float Weight, int Level) r) =>
        $"L{r.Level} {r.Id} = {r.Weight.ToString(CultureInfo.InvariantCulture)}";

    [TestMethod]
    public void EveryWeightedTroopAtLevel41OrAbove_PaysAtLeastThree()
    {
        var band = ResolvedRows().Where(r => r.Level >= HeavyBandFloorLevel).ToList();
        Assert.IsTrue(band.Count > 0, "no weighted troop resolved to level 41+; the sweep is empty");

        var light = band
            .Where(r => r.Weight < HeavyBandWeight)
            .OrderByDescending(r => r.Level).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(Describe)
            .ToList();

        Assert.AreEqual(0, light.Count,
            $"{light.Count} weighted troops at level {HeavyBandFloorLevel}+ pay under {HeavyBandWeight}:\n  "
            + string.Join("\n  ", light));
    }

    [TestMethod]
    public void WeightedTroopsBelowLevel41_StayUnderThree()
    {
        var heavy = ResolvedRows()
            .Where(r => r.Level < HeavyBandFloorLevel && r.Weight >= HeavyBandWeight)
            .Where(r => !MountPackages.Contains(r.Id))
            .Select(Describe)
            .ToList();

        Assert.AreEqual(0, heavy.Count,
            "troops below the heavy band pay 3.0 or more without being a mount package:\n  "
            + string.Join("\n  ", heavy));
    }

    [TestMethod]
    public void EveryWeightedId_ResolvesToATroopOrIsANamedMountPackage()
    {
        var unresolved = _weights.Keys
            .Where(id => !_levels.ContainsKey(id) && !MountPackages.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, unresolved.Count,
            "weighted ids that are neither a troops_*.xml troop nor a named mount package (a dead row, or a renamed troop):\n  "
            + string.Join("\n  ", unresolved));
    }
}
