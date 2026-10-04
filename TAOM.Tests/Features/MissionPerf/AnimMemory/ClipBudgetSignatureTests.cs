using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf.AnimMemory;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// The eviction-pass signature: pattern parsing, the byte scan, rip-relative targets, and the
/// resolve step on the real 50 bytes of the v1.5.3 site.
/// </summary>
[TestClass]
public class ClipBudgetSignatureTests
{
    [TestMethod]
    public void Parse_ThePattern_Has50BytesAnd16Wildcards()
    {
        var pattern = ClipBudgetSignature.Parse(ClipBudgetSignature.Pattern);

        Assert.AreEqual(50, pattern.Length);
        Assert.AreEqual(16, pattern.Count(b => b == -1));
    }

    [TestMethod]
    public void Parse_QuestionMarkAndDoubleQuestionMark_AreWildcards()
    {
        CollectionAssert.AreEqual(new[] { 0x8B, -1, -1, 0x05 }, ClipBudgetSignature.Parse("8B ? ?? 05"));
    }

    [TestMethod]
    public void Find_UniqueMatch_ReturnsItsOffset()
    {
        var hay = new byte[] { 0, 1, 0xAA, 0xBB, 0xCC, 2 };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB, 0xCC }, 2);

        CollectionAssert.AreEqual(new[] { 2 }, hits);
    }

    [TestMethod]
    public void Find_NoMatch_ReturnsEmpty()
    {
        var hits = ClipBudgetSignature.Find(new byte[] { 1, 2, 3, 4 }, new[] { 0xAA, 0xBB }, 2);

        Assert.AreEqual(0, hits.Count);
    }

    [TestMethod]
    public void Find_TwoMatches_StopsAtMaxHitsTwo()
    {
        var hay = new byte[] { 0xAA, 0xBB, 0xAA, 0xBB, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 0, 2 }, hits);
    }

    [TestMethod]
    public void Find_WildcardBytesDiffer_StillMatches()
    {
        var hay = new byte[] { 0x00, 0xAA, 0x77, 0xCC };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, -1, 0xCC }, 2);

        CollectionAssert.AreEqual(new[] { 1 }, hits);
    }

    [TestMethod]
    public void Find_MatchAtVeryEnd_IsFound()
    {
        var hay = new byte[] { 0x00, 0x00, 0x00, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 3 }, hits);
    }

    [TestMethod]
    public void Find_HaystackShorterThanPattern_ReturnsEmpty()
    {
        var hits = ClipBudgetSignature.Find(new byte[] { 0xAA }, new[] { 0xAA, 0xBB }, 2);

        Assert.AreEqual(0, hits.Count);
    }

    [TestMethod]
    public void RipTarget_RealLoadEncoding_IsDABE40()
    {
        var disp = BitConverter.ToInt32(new byte[] { 0x8B, 0x05, 0x2B, 0xDE, 0xB8, 0x00 }, 2);

        Assert.AreEqual(0xDABE40, ClipBudgetSignature.RipTarget(0x21E00F, 6, disp));
    }

    [TestMethod]
    public void RipTarget_RealSubssEncoding_IsB2E2DC()
    {
        var disp = BitConverter.ToInt32(new byte[] { 0xF3, 0x0F, 0x5C, 0x05, 0xA0, 0x02, 0x91, 0x00 }, 4);

        Assert.AreEqual(0xB2E2DC, ClipBudgetSignature.RipTarget(0x21E034, 8, disp));
    }

    [TestMethod]
    public void RipTarget_NegativeDisplacement_PointsBackwards()
    {
        Assert.AreEqual(0x1000 + 6 - 0x100, ClipBudgetSignature.RipTarget(0x1000, 6, -0x100));
    }

    [TestMethod]
    public void Resolve_RealSiteBytesAtTextRva21E00F_GivesTheVerifiedRvas()
    {
        var match = ClipBudgetSignature.Resolve(SyntheticNativeImage.RealSite, 0x21E00F);

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual(0x21E00F, match.LoadSiteRva);
        Assert.AreEqual(0x21E034, match.BudgetSiteRva);
        Assert.AreEqual(0xDABE40, match.CounterRva);
        Assert.AreEqual(0xB2E2DC, match.BudgetRva);
    }

    [TestMethod]
    public void Resolve_NoSite_MatchCountZero()
    {
        var match = ClipBudgetSignature.Resolve(new byte[0x200], 0x1000);

        Assert.AreEqual(0, match.MatchCount);
    }

    [TestMethod]
    public void Resolve_TwoSites_MatchCountTwo()
    {
        var text = SyntheticNativeImage.TextWithSiteAt(0x40, 0x3010, 0x2020);
        SyntheticNativeImage.PutSite(text, 0x100, 0x3010, 0x2020);

        var match = ClipBudgetSignature.Resolve(text, 0x1000);

        Assert.AreEqual(2, match.MatchCount);
    }
}
