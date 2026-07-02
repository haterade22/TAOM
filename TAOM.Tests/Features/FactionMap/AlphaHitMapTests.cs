using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap.Widgets;

namespace TAOM.Tests.Features.FactionMap;

// Pure hit-testing math extracted from PolygonWidget (T4 refactor): the downsampled max-alpha map
// and the normalized-coordinate opaque-pixel lookup that gates region hover/click. Behavior is
// pinned 1-for-1 to the pre-extraction widget code: strict > threshold, [0,1) norm range, index
// clamp to map edges, out-of-range index -> not opaque.
[TestClass]
public class AlphaHitMapTests
{
    private static AlphaHitMap MapFromCells(byte[] cells, int texWidth, int texHeight)
        => new AlphaHitMap(cells, texWidth, texHeight);

    // ------------------------------------------------------------------ MapDimension

    [TestMethod]
    public void MapDimension_ExactMultiple_DividesByDownsample()
        => Assert.AreEqual(2, AlphaHitMap.MapDimension(8));

    [TestMethod]
    public void MapDimension_Remainder_RoundsUp()
        => Assert.AreEqual(2, AlphaHitMap.MapDimension(5));

    [TestMethod]
    public void MapDimension_BelowDownsample_IsOne()
        => Assert.AreEqual(1, AlphaHitMap.MapDimension(1));

    // ------------------------------------------------------------------ IsOpaqueAtNormalized

    [TestMethod]
    public void IsOpaqueAtNormalized_OpaqueCell_ReturnsTrue()
    {
        // 8x8 texture -> 2x2 map. Top-left cell opaque.
        var map = MapFromCells(new byte[] { 255, 0, 0, 0 }, 8, 8);
        Assert.IsTrue(map.IsOpaqueAtNormalized(0.1f, 0.1f));
    }

    [TestMethod]
    public void IsOpaqueAtNormalized_TransparentCell_ReturnsFalse()
    {
        var map = MapFromCells(new byte[] { 255, 0, 0, 0 }, 8, 8);
        Assert.IsFalse(map.IsOpaqueAtNormalized(0.9f, 0.9f));
    }

    [TestMethod]
    public void IsOpaqueAtNormalized_AlphaExactlyAtThreshold_ReturnsFalse()
    {
        // Pre-extraction widget used strict "> AlphaThreshold" (20) — at-threshold is transparent.
        var map = MapFromCells(new byte[] { AlphaHitMap.DefaultOpaqueThreshold }, 4, 4);
        Assert.IsFalse(map.IsOpaqueAtNormalized(0.5f, 0.5f));
    }

    [TestMethod]
    public void IsOpaqueAtNormalized_AlphaJustAboveThreshold_ReturnsTrue()
    {
        var map = MapFromCells(new byte[] { AlphaHitMap.DefaultOpaqueThreshold + 1 }, 4, 4);
        Assert.IsTrue(map.IsOpaqueAtNormalized(0.5f, 0.5f));
    }

    [TestMethod]
    public void IsOpaqueAtNormalized_NormBelowZero_ReturnsFalse()
    {
        var map = MapFromCells(new byte[] { 255 }, 4, 4);
        Assert.IsFalse(map.IsOpaqueAtNormalized(-0.01f, 0.5f));
    }

    [TestMethod]
    public void IsOpaqueAtNormalized_NormAtOne_ReturnsFalse()
    {
        // Pre-extraction range check is norm >= 1f -> miss.
        var map = MapFromCells(new byte[] { 255 }, 4, 4);
        Assert.IsFalse(map.IsOpaqueAtNormalized(0.5f, 1f));
    }

    [TestMethod]
    public void IsOpaqueAtNormalized_NearOne_ClampsToLastCell()
    {
        // 8x8 -> 2x2 map; bottom-right cell opaque; norm 0.999 must land there, not out of range.
        var map = MapFromCells(new byte[] { 0, 0, 0, 255 }, 8, 8);
        Assert.IsTrue(map.IsOpaqueAtNormalized(0.999f, 0.999f));
    }

    [TestMethod]
    public void IsOpaqueAtNormalized_MapShorterThanExpected_ReturnsFalse()
    {
        // Pre-extraction code bounds-checked idx against the array and fell back to "not opaque".
        var map = MapFromCells(new byte[] { 255 }, 8, 8); // 2x2 expected, only 1 cell supplied
        Assert.IsFalse(map.IsOpaqueAtNormalized(0.9f, 0.9f));
    }

    // ------------------------------------------------------------------ FromArgbPixels (downsample builder)

    [TestMethod]
    public void FromArgbPixels_SingleOpaquePixelInBlock_CellTakesMaxAlpha()
    {
        // 8x8 bitmap, all transparent except pixel (5,6) alpha=200 -> map cell (1,1) == 200 drives a hit.
        const int w = 8, h = 8;
        var argb = new byte[w * h * 4];
        argb[(6 * w + 5) * 4 + 3] = 200;

        var handle = GCHandle.Alloc(argb, GCHandleType.Pinned);
        try
        {
            var map = AlphaHitMap.FromArgbPixels(handle.AddrOfPinnedObject(), stride: w * 4,
                bitmapWidth: w, bitmapHeight: h, texWidth: w, texHeight: h);
            Assert.IsTrue(map.IsOpaqueAtNormalized(0.75f, 0.75f), "cell containing the opaque pixel");
            Assert.IsFalse(map.IsOpaqueAtNormalized(0.25f, 0.25f), "fully transparent cell");
        }
        finally
        {
            handle.Free();
        }
    }

    [TestMethod]
    public void FromArgbPixels_AllTransparent_NoCellOpaque()
    {
        const int w = 4, h = 4;
        var argb = new byte[w * h * 4];
        var handle = GCHandle.Alloc(argb, GCHandleType.Pinned);
        try
        {
            var map = AlphaHitMap.FromArgbPixels(handle.AddrOfPinnedObject(), w * 4, w, h, w, h);
            Assert.IsFalse(map.IsOpaqueAtNormalized(0.5f, 0.5f));
        }
        finally
        {
            handle.Free();
        }
    }
}
