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
/// the file. The rule only sees troops that have a row, so a cloned tree that ships with no rows at
/// all passes it; the clone-parity test below covers that case for Lindon.
/// </summary>
[TestClass]
public class TroopWeightLevelBandTests
{
    private const int HeavyBandFloorLevel = 41;
    private const float HeavyBandWeight = 3.0f;

    /// <summary>
    /// Mount packages price the creature plus its crew, not the rider's tier, so they sit outside
    /// the level rule. The Dol Guldur spider riders live in <c>characters/</c>, not a troops file, and
    /// resolve to no level anyway; they are listed so the exemption reads complete. The goblin tree's
    /// mountain spider riders (2026-09-29) are troops in <c>troops_goblin.xml</c> and do resolve.
    /// </summary>
    private static readonly HashSet<string> MountPackages = new(StringComparer.Ordinal)
    {
        "harad_elephant_rider",
        "taom_spider_creature",
        "taom_spider_rider_brown",
        "taom_spider_rider_pale",
        "goblin_spider_rider",
        "goblin_spider_lord",
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

    /// <summary>
    /// Lindon's tree is a clone of Rivendell's under a <c>lindon_</c> prefix: <c>lindon_X</c> twins
    /// <c>rivendell_X</c> where that troop exists, else plain <c>X</c> (the <c>imladris_*</c> and
    /// unprefixed elites). Lindon shipped with no rows at all, so every Mithlond elite paid 1.0.
    /// </summary>
    [TestMethod]
    public void EveryLindonTroop_WeighsTheSameAsItsRivendellTwin()
    {
        const string prefix = "lindon_";
        var lindon = _levels.Keys.Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        Assert.IsTrue(lindon.Count > 0, "no lindon_ troop resolved; the sweep is empty");

        float WeightOf(string id) => _weights.TryGetValue(id, out var w) ? w : 1.0f;

        var mismatched = new List<string>();
        var unpaired = new List<string>();
        foreach (var id in lindon.OrderBy(id => id, StringComparer.Ordinal))
        {
            var bare = id.Substring(prefix.Length);
            var prefixed = "rivendell_" + bare;
            var twin = _levels.ContainsKey(prefixed) ? prefixed : bare;
            if (!_levels.ContainsKey(twin))
                unpaired.Add(id);
            else if (WeightOf(id) != WeightOf(twin))
                mismatched.Add($"{id} = {WeightOf(id).ToString(CultureInfo.InvariantCulture)}, "
                    + $"{twin} = {WeightOf(twin).ToString(CultureInfo.InvariantCulture)}");
        }

        Assert.AreEqual(0, unpaired.Count,
            $"{unpaired.Count} Lindon troops have no Rivendell twin (a renamed twin, or a troop of Lindon's own: "
            + "weigh it by the level rule and exempt it here by name):\n  " + string.Join("\n  ", unpaired));
        Assert.AreEqual(0, mismatched.Count,
            $"{mismatched.Count} Lindon troops weigh differently from their Rivendell twin:\n  "
            + string.Join("\n  ", mismatched));
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
