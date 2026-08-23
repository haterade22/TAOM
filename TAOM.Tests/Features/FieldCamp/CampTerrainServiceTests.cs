using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCamp;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Pins the full terrain mapping (every 1.4.8 <see cref="TerrainType"/> member classified, plus
/// the unknown-future-value default) and the forage formula with its non-finite gates.
/// </summary>
[TestClass]
public class CampTerrainServiceTests
{
    private CampTerrainService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new CampTerrainService();
    }

    // Source-decoded set (its compiled `t - 3` switch over the 1.4.8 enum values): Snow IS
    // concealing; UnderBridge postdates the source's cases and fell to its default-false arm.
    private static readonly HashSet<TerrainType> AmbushTerrain = new HashSet<TerrainType>
    {
        TerrainType.Snow,
        TerrainType.Forest,
        TerrainType.Fording,
        TerrainType.Mountain,
        TerrainType.Canyon,
        TerrainType.Swamp,
        TerrainType.Dune,
        TerrainType.Bridge,
    };

    private static readonly HashSet<TerrainType> LookoutTerrain = new HashSet<TerrainType>
    {
        TerrainType.Plain,
        TerrainType.Forest,
        TerrainType.Steppe,
        TerrainType.Mountain,
        TerrainType.Canyon,
    };

    // Source-decoded yields (its compiled `t - 1` switch over the 1.4.8 enum values). The 0.5
    // rows are the members that fell to the source's default arm at runtime, Cliff and the
    // movement-restriction faces included: parity kept over a silent retune.
    private static readonly Dictionary<TerrainType, float> ForageYields = new Dictionary<TerrainType, float>
    {
        [TerrainType.Plain] = 1f,
        [TerrainType.Forest] = 1f,
        [TerrainType.Steppe] = 0.7f,
        [TerrainType.Swamp] = 0.7f,
        [TerrainType.Mountain] = 0.45f,
        [TerrainType.Canyon] = 0.45f,
        [TerrainType.Desert] = 0.2f,
        [TerrainType.Snow] = 0.2f,
        [TerrainType.Lake] = 0f,
        [TerrainType.Water] = 0f,
        [TerrainType.River] = 0f,
        [TerrainType.CoastalSea] = 0f,
        [TerrainType.OpenSea] = 0f,
        [TerrainType.NonNavigableRiver] = 0f,
        [TerrainType.Fording] = 0.5f,
        [TerrainType.RuralArea] = 0.5f,
        [TerrainType.Dune] = 0.5f,
        [TerrainType.Bridge] = 0.5f,
        [TerrainType.Beach] = 0.5f,
        [TerrainType.Cliff] = 0.5f,
        [TerrainType.LandRestriction] = 0.5f,
        [TerrainType.SeaRestriction] = 0.5f,
        [TerrainType.UnderBridge] = 0.5f,
    };

    private static IEnumerable<TerrainType> AllTerrainMembers() =>
        Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>();

    // --- terrain sets ---

    [TestMethod]
    public void AllowsAmbush_EveryEnumMember_MatchesConcealmentSet()
    {
        foreach (var terrain in AllTerrainMembers())
        {
            Assert.AreEqual(
                AmbushTerrain.Contains(terrain), _sut.AllowsAmbush(terrain), terrain.ToString());
        }
    }

    [TestMethod]
    public void AllowsAmbush_UnknownFutureTerrain_Denied()
    {
        Assert.IsFalse(_sut.AllowsAmbush((TerrainType)999));
    }

    [TestMethod]
    public void AllowsLookout_EveryEnumMember_MatchesVantageSet()
    {
        foreach (var terrain in AllTerrainMembers())
        {
            Assert.AreEqual(
                LookoutTerrain.Contains(terrain), _sut.AllowsLookout(terrain), terrain.ToString());
        }
    }

    [TestMethod]
    public void AllowsLookout_UnknownFutureTerrain_Denied()
    {
        Assert.IsFalse(_sut.AllowsLookout((TerrainType)999));
    }

    [TestMethod]
    public void ForageYield_EveryEnumMember_MatchesYieldTable()
    {
        // The test-side table also proves no engine member was silently skipped: a new enum
        // value fails this lookup until someone classifies it in BOTH the service and the table.
        foreach (var terrain in AllTerrainMembers())
        {
            Assert.IsTrue(ForageYields.ContainsKey(terrain), $"unclassified terrain {terrain}");
            Assert.AreEqual(ForageYields[terrain], _sut.ForageYield(terrain), 0.0001f, terrain.ToString());
        }
    }

    [TestMethod]
    public void ForageYield_UnknownFutureTerrain_YieldsNothing()
    {
        Assert.AreEqual(0f, _sut.ForageYield((TerrainType)999));
    }

    // --- forage formula ---

    [TestMethod]
    public void HourlyForage_PlainsFormula_YieldTimesScoutingTimesFactorTimesSqrtTroops()
    {
        // 1.0 * (1 + 50/100) * 0.1 * sqrt(100) = 1.5
        float result = _sut.HourlyForage(TerrainType.Plain, troopCount: 100, scoutingSkill: 50f, perTroopFactor: 0.1f);

        Assert.AreEqual(1.5f, result, 0.0001f);
    }

    [TestMethod]
    public void HourlyForage_SteppeTerrain_AppliesReducedYield()
    {
        // 0.7 * 1 * 0.1 * 10 = 0.7
        float result = _sut.HourlyForage(TerrainType.Steppe, 100, 0f, 0.1f);

        Assert.AreEqual(0.7f, result, 0.0001f);
    }

    [TestMethod]
    public void HourlyForage_QuadrupleTroops_DoublesYield()
    {
        float small = _sut.HourlyForage(TerrainType.Plain, 25, 0f, 0.1f);
        float large = _sut.HourlyForage(TerrainType.Plain, 100, 0f, 0.1f);

        Assert.AreEqual(2f, large / small, 0.0001f);
    }

    [TestMethod]
    public void HourlyForage_ZeroYieldTerrain_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Water, 100, 50f, 0.1f));
    }

    [TestMethod]
    public void HourlyForage_ZeroTroops_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, 0, 50f, 0.1f));
    }

    [TestMethod]
    public void HourlyForage_NegativeTroops_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, -5, 50f, 0.1f));
    }

    [TestMethod]
    public void HourlyForage_NaNScouting_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, 100, float.NaN, 0.1f));
    }

    [TestMethod]
    public void HourlyForage_InfiniteScouting_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, 100, float.PositiveInfinity, 0.1f));
    }

    [TestMethod]
    public void HourlyForage_NegativeScouting_ClampsToBaseYield()
    {
        float corrupt = _sut.HourlyForage(TerrainType.Plain, 100, -200f, 0.1f);
        float baseline = _sut.HourlyForage(TerrainType.Plain, 100, 0f, 0.1f);

        Assert.AreEqual(baseline, corrupt, 0.0001f);
    }

    [TestMethod]
    public void HourlyForage_NaNPerTroopFactor_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, 100, 50f, float.NaN));
    }

    [TestMethod]
    public void HourlyForage_InfinitePerTroopFactor_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, 100, 50f, float.PositiveInfinity));
    }

    [TestMethod]
    public void HourlyForage_ZeroPerTroopFactor_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, 100, 50f, 0f));
    }

    [TestMethod]
    public void HourlyForage_NegativePerTroopFactor_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.HourlyForage(TerrainType.Plain, 100, 50f, -0.1f));
    }
}
