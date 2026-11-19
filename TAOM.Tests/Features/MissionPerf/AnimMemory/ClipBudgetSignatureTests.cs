using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf.AnimMemory;
using TAOM.Features.SkeletonBuffer;

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
    public void Find_LeadingWildcard_MatchesAnyFirstByte()
    {
        var hay = new byte[] { 0x01, 0xBB, 0xFF, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { -1, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 0, 2 }, hits);
    }

    [TestMethod]
    public void Find_FirstByteOnlyInTheLastBytes_ReturnsEmpty()
    {
        // 0xAA sits at the last offset, where the two-byte pattern no longer fits.
        var hay = new byte[] { 0x00, 0x00, 0x00, 0xAA };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 2);

        Assert.AreEqual(0, hits.Count);
    }

    [TestMethod]
    public void Find_OverlappingMatches_ReturnsEachStart()
    {
        var hay = new byte[] { 0xAA, 0xAA, 0xAA, 0xAA };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xAA }, 3);

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, hits);
    }

    [TestMethod]
    public void Find_CandidateFailsAfterTheFirstByte_KeepsSearching()
    {
        var hay = new byte[] { 0xAA, 0x00, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 2 }, hits);
    }

    [TestMethod]
    public void Find_MaxHitsOne_StopsAtTheFirstHit()
    {
        var hay = new byte[] { 0xAA, 0xBB, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 1);

        CollectionAssert.AreEqual(new[] { 0 }, hits);
    }

    [TestMethod]
    public void Find_MaxHitsZero_ReturnsTheFirstHit()
    {
        // Pins today's behaviour: the hit is added before the maxHits check.
        var hay = new byte[] { 0xAA, 0xBB, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 0);

        CollectionAssert.AreEqual(new[] { 0 }, hits);
    }

    [TestMethod]
    public void Find_EmptyHaystack_ReturnsEmpty()
    {
        Assert.AreEqual(0, ClipBudgetSignature.Find(new byte[0], new[] { 0xAA }, 2).Count);
        Assert.AreEqual(0, ClipBudgetSignature.Find(new byte[0], new[] { -1 }, 2).Count);
    }

    [TestMethod]
    public void Find_EmptyPattern_MatchesEveryOffsetUpToMaxHits()
    {
        var hits = ClipBudgetSignature.Find(new byte[] { 1, 2, 3 }, new int[0], 2);

        CollectionAssert.AreEqual(new[] { 0, 1 }, hits);
    }

    [TestMethod]
    public void Find_HaystackSameLengthAsPattern_MatchesAtZero()
    {
        var hits = ClipBudgetSignature.Find(new byte[] { 0xAA, 0xBB }, new[] { 0xAA, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 0 }, hits);
    }

    [TestMethod]
    public void Find_RandomSmallInputs_MatchesTheBaseLoop()
    {
        // A four-byte alphabet and short haystacks make candidates, overlaps and near-end misses common.
        var rng = new Random(784);
        var bytes = new byte[] { 0x00, 0xAA, 0xBB, 0xFE };
        var tokens = new[] { 0x00, 0xAA, 0xBB, 0xFE, -1 };
        for (var n = 0; n < 20000; n++)
        {
            var hay = new byte[rng.Next(0, 25)];
            for (var k = 0; k < hay.Length; k++)
                hay[k] = bytes[rng.Next(bytes.Length)];
            var pattern = new int[rng.Next(0, 6)];
            for (var k = 0; k < pattern.Length; k++)
                pattern[k] = tokens[rng.Next(tokens.Length)];
            var maxHits = rng.Next(0, 4);

            CollectionAssert.AreEqual(BaseFind(hay, pattern, maxHits), ClipBudgetSignature.Find(hay, pattern, maxHits),
                $"case {n}: hay [{BitConverter.ToString(hay)}], pattern [{string.Join(" ", pattern)}], maxHits {maxHits}");
        }
    }

    [TestMethod]
    public void Find_RealPatternsInRandomCode_MatchesTheBaseLoop()
    {
        var rng = new Random(785);
        var patterns = new[]
        {
            ClipBudgetSignature.Pattern, SkeletonBufferSignature.WatchPattern, SkeletonBufferSignature.Pool2Pattern,
            SkeletonBufferSignature.GuardPattern, SkeletonBufferSignature.ForeignGuardPattern,
            SkeletonBufferSignature.Pool2GuardPattern, SkeletonBufferSignature.Pool2ForeignGuardPattern,
        };
        foreach (var text in patterns)
        {
            var pattern = ClipBudgetSignature.Parse(text);
            var code = new byte[64 * 1024];
            rng.NextBytes(code);
            var end = code.Length - pattern.Length;
            Plant(code, 0x100, pattern, rng);
            Plant(code, 0x8000, pattern, rng);
            Plant(code, end, pattern, rng);

            foreach (var maxHits in new[] { 2, 3 })
            {
                var expected = BaseFind(code, pattern, maxHits);
                CollectionAssert.AreEqual(new[] { 0x100, 0x8000, end }.Take(maxHits).ToList(), expected, text);
                CollectionAssert.AreEqual(expected, ClipBudgetSignature.Find(code, pattern, maxHits), text);
            }
        }
    }

    /// <summary>The scan loop as it was before the first-byte jump (commit c6a82aa04), kept as the oracle.</summary>
    private static List<int> BaseFind(byte[] haystack, int[] pattern, int maxHits)
    {
        var hits = new List<int>();
        for (var i = 0; i <= haystack.Length - pattern.Length; i++)
        {
            var j = 0;
            while (j < pattern.Length && (pattern[j] == -1 || haystack[i + j] == pattern[j]))
                j++;
            if (j < pattern.Length)
                continue;
            hits.Add(i);
            if (hits.Count >= maxHits)
                break;
        }
        return hits;
    }

    /// <summary>Writes <paramref name="pattern"/> at <paramref name="at"/>, with a random byte for each wildcard.</summary>
    private static void Plant(byte[] code, int at, int[] pattern, Random rng)
    {
        for (var k = 0; k < pattern.Length; k++)
            code[at + k] = pattern[k] == -1 ? (byte)rng.Next(256) : (byte)pattern[k];
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
