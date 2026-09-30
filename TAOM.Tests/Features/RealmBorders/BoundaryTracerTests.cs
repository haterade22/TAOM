using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins how province edges become boundary chains. Each chain separates exactly two provinces, keeps
/// the lower province on its left for its whole length, and stops where a third province meets it,
/// which is what lets the selector recolour or drop whole chains when a fief changes hands.
/// </summary>
[TestClass]
public class BoundaryTracerTests
{
    /// <summary>A province map from rows written north first, one char per cell ('.' unclaimed).</summary>
    internal static ProvinceMap Map(params string[] northFirstRows)
    {
        int rows = northFirstRows.Length, columns = northFirstRows[0].Length;
        var labels = new int[columns * rows];
        for (int r = 0; r < rows; r++)
        {
            string line = northFirstRows[rows - 1 - r];
            for (int c = 0; c < columns; c++)
                labels[r * columns + c] = line[c] == '.' ? ProvinceMap.Unclaimed : line[c] - '0';
        }
        return new ProvinceMap(columns, rows, 0f, 0f, 1f, labels);
    }

    [TestMethod]
    public void Trace_TwoProvinces_GiveOneChainWithTheLowerProvinceOnTheLeft()
    {
        var map = Map("000111", "000111", "000111", "000111");

        var chain = BoundaryTracer.Trace(map).Single();

        Assert.AreEqual(0, chain.Left);
        Assert.AreEqual(1, chain.Right);
        Assert.IsFalse(chain.IsClosed);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 5).Select(r => new LatticePoint(3, r)).ToArray(),
            chain.Points.ToArray(),
            "province 0 lies west, so the chain runs north");
    }

    [TestMethod]
    public void Trace_EveryChain_KeepsItsLeftProvinceOnTheLeftOfEverySegment()
    {
        var map = Map("2222220", "2211110", "0011110", "0033330", "0000000");

        foreach (var chain in BoundaryTracer.Trace(map))
        {
            var pts = chain.Points.ToList();
            if (chain.IsClosed)
                pts.Add(pts[0]);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                int dx = pts[i + 1].Column - pts[i].Column, dy = pts[i + 1].Row - pts[i].Row;
                double mx = (pts[i].Column + pts[i + 1].Column) / 2.0, my = (pts[i].Row + pts[i + 1].Row) / 2.0;
                int leftColumn = (int)Math.Floor(mx - 0.5 * dy), leftRow = (int)Math.Floor(my + 0.5 * dx);
                int rightColumn = (int)Math.Floor(mx + 0.5 * dy), rightRow = (int)Math.Floor(my - 0.5 * dx);
                Assert.AreEqual(chain.Left, map[leftColumn, leftRow], $"left of segment {i} in chain {chain.Left}|{chain.Right}");
                Assert.AreEqual(chain.Right, map[rightColumn, rightRow], $"right of segment {i} in chain {chain.Left}|{chain.Right}");
            }
        }
    }

    [TestMethod]
    public void Trace_ThreeProvinces_MeetAtOneJunction()
    {
        var map = Map("222222", "222222", "222222", "000111", "000111", "000111");

        var chains = BoundaryTracer.Trace(map);

        CollectionAssert.AreEquivalent(new[] { "0|1", "0|2", "1|2" }, chains.Select(c => $"{c.Left}|{c.Right}").ToArray());
        var junction = new LatticePoint(3, 3);
        Assert.IsTrue(chains.All(c => c.Points.First().Equals(junction) || c.Points.Last().Equals(junction)));
    }

    [TestMethod]
    public void Trace_Enclave_IsOneClosedChain()
    {
        var map = Map("000000", "000000", "001100", "001100", "000000", "000000");

        var chain = BoundaryTracer.Trace(map).Single();

        Assert.IsTrue(chain.IsClosed);
        Assert.AreEqual(8, chain.Points.Count, "the ring around a 2x2 block has eight corners, the first not repeated");
    }

    [TestMethod]
    public void Trace_UnclaimedLand_DrawsNoChain()
    {
        var map = Map("000...", "000...", "000...");

        Assert.AreEqual(0, BoundaryTracer.Trace(map).Count);
    }

    [TestMethod]
    public void Trace_Saddle_GivesTwoChainsThatEachKeepTheLowerProvinceOnTheLeft()
    {
        // Two provinces touching only at a corner, which the 8-connected flood produces routinely.
        var map = Map("01", "10");

        var chains = BoundaryTracer.Trace(map);

        Assert.AreEqual(2, chains.Count);
        var saddle = new LatticePoint(1, 1);
        foreach (var chain in chains)
        {
            Assert.AreEqual((0, 1), (chain.Left, chain.Right));
            Assert.AreEqual(3, chain.Points.Count, "each chain turns at the saddle");
            Assert.AreEqual(saddle, chain.Points[1]);
        }
    }

    [TestMethod]
    public void Trace_FourProvincesAtOneCorner_GiveFourChainsEndingThere()
    {
        var map = Map("01", "23");

        var chains = BoundaryTracer.Trace(map);

        var corner = new LatticePoint(1, 1);
        CollectionAssert.AreEquivalent(new[] { (0, 1), (0, 2), (1, 3), (2, 3) }, chains.Select(c => (c.Left, c.Right)).ToList());
        Assert.IsTrue(chains.All(c => c.Points.Count == 2 && c.Points.Contains(corner)));
    }
}
