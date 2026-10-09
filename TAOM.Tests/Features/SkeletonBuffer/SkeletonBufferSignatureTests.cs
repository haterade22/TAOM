// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SkeletonBuffer;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>The six byte patterns resolved over synthetic code: counts, RVAs, and the shape another module's guard leaves.</summary>
[TestClass]
public class SkeletonBufferSignatureTests
{
    private const int TextRva = 0x1000;

    private static readonly byte[] WatchBytes =
    {
        0x48, 0x8B, 0x0D, 0x78, 0x34, 0xD3, 0x00,             // mov rcx, [rip+0xD33478]
        0x48, 0x81, 0xC1, 0xD0, 0x09, 0x00, 0x00,             // add rcx, 0x9D0
        0x48, 0x63, 0x81, 0x50, 0x02, 0x00, 0x00,             // movsxd rax, [rcx+0x250]
        0x48, 0x69, 0xE8, 0x28, 0x01, 0x00, 0x00,             // imul rbp, rax, 0x128
    };

    private static readonly byte[] GuardBytes =
    {
        0x41, 0x8B, 0xF4, 0x4C, 0x89, 0x7C, 0x24, 0x20, 0xF0, 0x0F, 0xC1, 0x75, 0x00,
        0x45, 0x8D, 0x7C, 0x24, 0xFF, 0x8B, 0xC6, 0xC1, 0xE8, 0x0B,
    };

    private static readonly byte[] ForeignBytes =
    {
        0xE9, 0x11, 0x22, 0x33, 0x44, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
        0x45, 0x8D, 0x7C, 0x24, 0xFF, 0x8B, 0xC6, 0xC1, 0xE8, 0x0B,
    };

    private static readonly byte[] Pool2Bytes =
    {
        0x48, 0x8B, 0x15, 0x84, 0x36, 0xD3, 0x00,             // mov rdx, [rip+0xD33684]
        0x48, 0x81, 0xC2, 0x28, 0x0C, 0x00, 0x00,             // add rdx, 0xC28
        0x48, 0x89, 0x5C, 0x24, 0x50,                         // mov [rsp+50h], rbx
        0x48, 0x63, 0x82, 0x50, 0x02, 0x00, 0x00,             // movsxd rax, [rdx+0x250]
        0x48, 0x69, 0xD8, 0x28, 0x01, 0x00, 0x00,             // imul rbx, rax, 0x128
    };

    // Pool 2's reservation: 13 site bytes (mov [rsp+20h], r15; mov r15d, edx; lock xadd [rcx], r15d), then the resume bytes.
    private static readonly byte[] Guard2Bytes =
    {
        0x4C, 0x89, 0x7C, 0x24, 0x20, 0x44, 0x8B, 0xFA, 0xF0, 0x44, 0x0F, 0xC1, 0x39,
        0x8D, 0x7A, 0xFF, 0x41, 0x8B, 0xC7, 0xC1, 0xE8, 0x0D,
    };

    private static readonly byte[] Foreign2Bytes =
    {
        0xE9, 0x11, 0x22, 0x33, 0x44, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
        0x8D, 0x7A, 0xFF, 0x41, 0x8B, 0xC7, 0xC1, 0xE8, 0x0D,
    };

    /// <summary>The pool 2 load (0x1A0), its call at 0x1C9 (0x29 after the load, as in v1.5.4) and the callee entry at 0x220.</summary>
    private static byte[] Pool2Call(int callOffset = 0x1C9, int targetOffset = 0x220)
    {
        var call = new byte[5];
        call[0] = 0xE8;
        BitConverter.GetBytes(targetOffset - (callOffset + 5)).CopyTo(call, 1);
        return call;
    }

    private static byte[] Text(params (int Offset, byte[] Bytes)[] placed)
    {
        var text = new byte[0x400];
        for (var i = 0; i < text.Length; i++) text[i] = 0xCC;
        foreach (var (offset, bytes) in placed)
            Array.Copy(bytes, 0, text, offset, bytes.Length);
        return text;
    }

    [TestMethod]
    public void Resolve_OneWatchAndOneGuard_ReturnsTheFourRvas()
    {
        var text = Text((0x40, WatchBytes), (0x73, GuardBytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(1, result.WatchMatchCount);
        Assert.AreEqual(1, result.GuardMatchCount);
        Assert.AreEqual(0, result.ForeignGuardMatchCount);
        Assert.AreEqual(0, result.Pool2MatchCount);
        Assert.AreEqual(TextRva + 0x40, result.WatchSiteRva);
        Assert.AreEqual(TextRva + 0x40 + 7 + 0xD33478, result.GlobalRva);
        Assert.AreEqual(TextRva + 0x73, result.GuardSiteRva);
        Assert.AreEqual(TextRva + 0x73 + 13, result.ResumeRva);
        Assert.IsFalse(result.ForeignGuardPresent);
    }

    [TestMethod]
    public void Resolve_NothingMatches_ReturnsZeroCountsAndZeroRvas()
    {
        var result = SkeletonBufferSignature.Resolve(Text(), TextRva);

        Assert.AreEqual(0, result.WatchMatchCount);
        Assert.AreEqual(0, result.GuardMatchCount);
        Assert.AreEqual(0, result.WatchSiteRva);
        Assert.AreEqual(0, result.GlobalRva);
        Assert.AreEqual(0, result.GuardSiteRva);
        Assert.AreEqual(0, result.ResumeRva);
    }

    [TestMethod]
    public void Resolve_TwoWatchMatches_CountsTwoAndLeavesTheWatchRvasZero()
    {
        var text = Text((0x40, WatchBytes), (0x100, WatchBytes), (0x73, GuardBytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(2, result.WatchMatchCount);
        Assert.AreEqual(0, result.WatchSiteRva);
        Assert.AreEqual(0, result.GlobalRva);
        Assert.AreEqual(1, result.GuardMatchCount);
        Assert.AreEqual(TextRva + 0x73, result.GuardSiteRva);
    }

    [TestMethod]
    public void Resolve_TwoGuardMatches_CountsTwoAndLeavesTheGuardRvasZero()
    {
        var text = Text((0x40, WatchBytes), (0x73, GuardBytes), (0x200, GuardBytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(2, result.GuardMatchCount);
        Assert.AreEqual(0, result.GuardSiteRva);
        Assert.AreEqual(0, result.ResumeRva);
        Assert.AreEqual(1, result.WatchMatchCount);
    }

    [TestMethod]
    public void Resolve_AnotherModulesJumpPatch_ReportsTheForeignShapeAndNoGuardMatch()
    {
        // The site's 13 bytes are an E9 jump plus eight NOPs; the instructions after it are the original ones.
        var text = Text((0x40, WatchBytes), (0x73, ForeignBytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.IsTrue(result.ForeignGuardPresent);
        Assert.AreEqual(1, result.ForeignGuardMatchCount);
        Assert.AreEqual(0, result.GuardMatchCount);
    }

    [TestMethod]
    public void Resolve_WildcardsInTheWatchPattern_AcceptAnyGlobalDisplacement()
    {
        var other = (byte[])WatchBytes.Clone();
        other[3] = 0x00; other[4] = 0x00; other[5] = 0x00; other[6] = 0x00;

        var result = SkeletonBufferSignature.Resolve(Text((0x40, other)), TextRva);

        Assert.AreEqual(1, result.WatchMatchCount);
        Assert.AreEqual(TextRva + 0x40 + 7, result.GlobalRva);
    }

    [TestMethod]
    public void Resolve_NegativeDisplacement_ReturnsALowerGlobalRva()
    {
        var other = (byte[])WatchBytes.Clone();
        BitConverter.GetBytes(-0x20).CopyTo(other, 3);

        var result = SkeletonBufferSignature.Resolve(Text((0x40, other)), TextRva);

        Assert.AreEqual(TextRva + 0x40 + 7 - 0x20, result.GlobalRva);
    }

    [TestMethod]
    public void Resolve_GuardWithOneByteChanged_DoesNotMatch()
    {
        var changed = (byte[])GuardBytes.Clone();
        changed[8] = 0xF1;   // the lock prefix

        var result = SkeletonBufferSignature.Resolve(Text((0x40, WatchBytes), (0x73, changed)), TextRva);

        Assert.AreEqual(0, result.GuardMatchCount);
    }

    [TestMethod]
    public void Resolve_OnePool2Load_ReturnsItsRvaAndTheGlobalItReads()
    {
        var text = Text((0x40, WatchBytes), (0x73, GuardBytes), (0x1A0, Pool2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(1, result.Pool2MatchCount);
        Assert.AreEqual(TextRva + 0x1A0, result.Pool2LoadRva);
        Assert.AreEqual(TextRva + 0x1A0 + 7 + 0xD33684, result.Pool2GlobalRva);
    }

    [TestMethod]
    public void Resolve_Pool2LoadReadingTheSameGlobalAsTheWatchSite_GivesEqualGlobalRvas()
    {
        var watchGlobalDisp = 0xD33684 + (0x1A0 - 0x40);   // both loads end at the same RVA + displacement
        var watch = (byte[])WatchBytes.Clone();
        BitConverter.GetBytes(watchGlobalDisp).CopyTo(watch, 3);

        var result = SkeletonBufferSignature.Resolve(Text((0x40, watch), (0x1A0, Pool2Bytes)), TextRva);

        Assert.AreEqual(result.GlobalRva, result.Pool2GlobalRva);
    }

    [TestMethod]
    public void Resolve_NoPool2Load_CountsZeroAndLeavesItsRvasZero()
    {
        var result = SkeletonBufferSignature.Resolve(Text((0x40, WatchBytes), (0x73, GuardBytes)), TextRva);

        Assert.AreEqual(0, result.Pool2MatchCount);
        Assert.AreEqual(0, result.Pool2LoadRva);
        Assert.AreEqual(0, result.Pool2GlobalRva);
    }

    [TestMethod]
    public void Resolve_TwoPool2Loads_CountsTwoAndLeavesItsRvasZero()
    {
        var result = SkeletonBufferSignature.Resolve(Text((0x1A0, Pool2Bytes), (0x2C0, Pool2Bytes)), TextRva);

        Assert.AreEqual(2, result.Pool2MatchCount);
        Assert.AreEqual(0, result.Pool2LoadRva);
        Assert.AreEqual(0, result.Pool2GlobalRva);
    }

    [TestMethod]
    public void Resolve_Pool2WithOneByteChanged_DoesNotMatch()
    {
        var changed = (byte[])Pool2Bytes.Clone();
        changed[11] = 0x0D;   // the 0xC28 offset's second byte

        Assert.AreEqual(0, SkeletonBufferSignature.Resolve(Text((0x1A0, changed)), TextRva).Pool2MatchCount);
    }

    [TestMethod]
    public void Resolve_PatternsAreTheDocumentedLengths()
    {
        Assert.AreEqual(28, SkeletonBufferSignature.Parse(SkeletonBufferSignature.WatchPattern).Length);
        Assert.AreEqual(23, SkeletonBufferSignature.Parse(SkeletonBufferSignature.GuardPattern).Length);
        Assert.AreEqual(23, SkeletonBufferSignature.Parse(SkeletonBufferSignature.ForeignGuardPattern).Length);
        Assert.AreEqual(33, SkeletonBufferSignature.Parse(SkeletonBufferSignature.Pool2Pattern).Length);
        Assert.AreEqual(13, SkeletonBufferSignature.GuardSiteLength);
    }

    // ---- pool 2's guard site ----

    [TestMethod]
    public void Resolve_OnePool2GuardCalledFromTheLoad_ReturnsItsSiteResumeAndTheLink()
    {
        var text = Text((0x40, WatchBytes), (0x1A0, Pool2Bytes), (0x1C9, Pool2Call()), (0x240, Guard2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(1, result.Pool2GuardMatchCount);
        Assert.AreEqual(0, result.Pool2ForeignGuardMatchCount);
        Assert.AreEqual(TextRva + 0x240, result.Pool2GuardSiteRva);
        Assert.AreEqual(TextRva + 0x240 + 13, result.Pool2ResumeRva);
        Assert.IsTrue(result.Pool2GuardCalledFromLoad);
        Assert.IsFalse(result.Pool2ForeignGuardPresent);
    }

    [TestMethod]
    public void Resolve_Pool2GuardSiteLiesBeyondTheCalleeSpan_IsNotLinked()
    {
        // The call lands at 0x220; a site 0x81 bytes in is not in that function.
        var text = Text((0x1A0, Pool2Bytes), (0x1C9, Pool2Call()), (0x2A1, Guard2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(1, result.Pool2GuardMatchCount);
        Assert.IsFalse(result.Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_Pool2GuardSiteExactlyAtTheCalleeSpan_IsLinked()
    {
        var text = Text((0x1A0, Pool2Bytes), (0x1C9, Pool2Call()), (0x2A0, Guard2Bytes));

        Assert.IsTrue(SkeletonBufferSignature.Resolve(text, TextRva).Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_Pool2GuardSiteBeforeTheCalleeEntry_IsNotLinked()
    {
        var text = Text((0x1A0, Pool2Bytes), (0x1C9, Pool2Call()), (0x210, Guard2Bytes));

        Assert.IsFalse(SkeletonBufferSignature.Resolve(text, TextRva).Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_NoCallAfterThePool2Load_IsNotLinked()
    {
        var text = Text((0x1A0, Pool2Bytes), (0x240, Guard2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(1, result.Pool2GuardMatchCount);
        Assert.IsFalse(result.Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_CallBeyondTheWindowAfterThePool2Load_IsNotLinked()
    {
        var text = Text((0x1A0, Pool2Bytes), (0x1A0 + 0x80, Pool2Call(0x1A0 + 0x80)), (0x240, Guard2Bytes));

        Assert.IsFalse(SkeletonBufferSignature.Resolve(text, TextRva).Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_CallJustInsideTheWindowAfterThePool2Load_IsLinked()
    {
        var text = Text((0x1A0, Pool2Bytes), (0x1A0 + 0x7F, Pool2Call(0x1A0 + 0x7F)), (0x240, Guard2Bytes));

        Assert.IsTrue(SkeletonBufferSignature.Resolve(text, TextRva).Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_CallDisplacementWrapsInt32_IsNotLinked()
    {
        // E8 FF FF FF 7F at 0x1C9: in int arithmetic the target wraps to -2147479091 and passes the span check.
        var wrapping = new byte[] { 0xE8, 0xFF, 0xFF, 0xFF, 0x7F };
        var text = Text((0x1A0, Pool2Bytes), (0x1C9, wrapping), (0x240, Guard2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(1, result.Pool2GuardMatchCount);
        Assert.IsFalse(result.Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_Pool2GuardWithoutThePool2Load_IsNotLinked()
    {
        var text = Text((0x1C9, Pool2Call()), (0x240, Guard2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(1, result.Pool2GuardMatchCount);
        Assert.AreEqual(0, result.Pool2MatchCount);
        Assert.IsFalse(result.Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_TwoPool2GuardMatches_CountsTwoAndLeavesItsRvasZero()
    {
        var text = Text((0x1A0, Pool2Bytes), (0x1C9, Pool2Call()), (0x240, Guard2Bytes), (0x300, Guard2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.AreEqual(2, result.Pool2GuardMatchCount);
        Assert.AreEqual(0, result.Pool2GuardSiteRva);
        Assert.AreEqual(0, result.Pool2ResumeRva);
        Assert.IsFalse(result.Pool2GuardCalledFromLoad);
    }

    [TestMethod]
    public void Resolve_NoPool2Guard_CountsZero()
    {
        var result = SkeletonBufferSignature.Resolve(Text((0x40, WatchBytes), (0x73, GuardBytes)), TextRva);

        Assert.AreEqual(0, result.Pool2GuardMatchCount);
        Assert.AreEqual(0, result.Pool2ForeignGuardMatchCount);
        Assert.AreEqual(0, result.Pool2GuardSiteRva);
        Assert.AreEqual(0, result.Pool2ResumeRva);
    }

    [TestMethod]
    public void Resolve_AnotherModulesJumpAtPool2sSite_ReportsTheForeignShapeAndNoGuardMatch()
    {
        var text = Text((0x1A0, Pool2Bytes), (0x1C9, Pool2Call()), (0x240, Foreign2Bytes));

        var result = SkeletonBufferSignature.Resolve(text, TextRva);

        Assert.IsTrue(result.Pool2ForeignGuardPresent);
        Assert.AreEqual(1, result.Pool2ForeignGuardMatchCount);
        Assert.AreEqual(0, result.Pool2GuardMatchCount);
    }

    [TestMethod]
    public void Resolve_Pool2GuardWithOneByteChanged_DoesNotMatch()
    {
        var changed = (byte[])Guard2Bytes.Clone();
        changed[8] = 0xF1;   // the lock prefix

        Assert.AreEqual(0, SkeletonBufferSignature.Resolve(Text((0x240, changed)), TextRva).Pool2GuardMatchCount);
    }

    [TestMethod]
    public void Resolve_Pool2ResumeBytesChanged_DoesNotMatch()
    {
        var changed = (byte[])Guard2Bytes.Clone();
        changed[21] = 0x0C;   // shr eax, 0xd -> 0xc: not the instruction the cave jumps back to

        Assert.AreEqual(0, SkeletonBufferSignature.Resolve(Text((0x240, changed)), TextRva).Pool2GuardMatchCount);
    }

    [TestMethod]
    public void Resolve_PoolOnesAndPoolTwosGuardPatterns_NeverMatchEachOthersBytes()
    {
        var one = SkeletonBufferSignature.Resolve(Text((0x73, GuardBytes)), TextRva);
        var two = SkeletonBufferSignature.Resolve(Text((0x240, Guard2Bytes)), TextRva);

        Assert.AreEqual(1, one.GuardMatchCount);
        Assert.AreEqual(0, one.Pool2GuardMatchCount);
        Assert.AreEqual(0, two.GuardMatchCount);
        Assert.AreEqual(1, two.Pool2GuardMatchCount);
    }

    [TestMethod]
    public void Resolve_Pool2Patterns_AreTheDocumentedLengths()
    {
        Assert.AreEqual(22, SkeletonBufferSignature.Parse(SkeletonBufferSignature.Pool2GuardPattern).Length);
        Assert.AreEqual(22, SkeletonBufferSignature.Parse(SkeletonBufferSignature.Pool2ForeignGuardPattern).Length);
    }
}
