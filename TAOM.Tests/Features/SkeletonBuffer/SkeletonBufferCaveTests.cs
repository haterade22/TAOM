// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SkeletonBuffer;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>The two code blocks and the site jumps, pinned byte for byte at fixed addresses (module at 0x7FF600000000).</summary>
[TestClass]
public class SkeletonBufferCaveTests
{
    private const long Cave = 0x7FF5FFF00000;
    private const long Counter = 0x7FF5FFF01000;   // the second page
    private const long Site = 0x7FF600069D14;
    private const long Resume = 0x7FF600069D21;

    private static readonly byte[] GoldenCave =
    {
        0x41, 0x8B, 0xF4,                               // 00 mov  esi, r12d
        0x4C, 0x89, 0x7C, 0x24, 0x20,                   // 03 mov  [rsp+20h], r15
        0xF0, 0x0F, 0xC1, 0x75, 0x00,                   // 08 lock xadd [rbp], esi
        0x42, 0x8D, 0x04, 0x26,                         // 0D lea  eax, [rsi+r12]
        0x3D, 0x00, 0x00, 0x01, 0x00,                   // 11 cmp  eax, 10000h
        0x76, 0x0E,                                     // 16 jbe  26h
        0xF0, 0x44, 0x29, 0x65, 0x00,                   // 18 lock sub [rbp], r12d
        0xF0, 0xFF, 0x05, 0xDC, 0x0F, 0x00, 0x00,       // 1D lock inc dword [rip+0xFDC]
        0x33, 0xF6,                                     // 24 xor  esi, esi
        0xE9, 0xF6, 0x9C, 0x16, 0x00,                   // 26 jmp  resume
    };

    // Pool 2 (TAOM's own design, mirroring pool 1): cave 2 sits 0x40 into the code page, its counter 0x40 into the data page.
    private const long Cave2 = 0x7FF5FFF00040;
    private const long Counter2 = 0x7FF5FFF01040;
    private const long Site2 = 0x7FF60006AF50;
    private const long Resume2 = 0x7FF60006AF5D;

    private static readonly byte[] GoldenCave2 =
    {
        0x4C, 0x89, 0x7C, 0x24, 0x20,                   // 00 mov  [rsp+20h], r15
        0x44, 0x8B, 0xFA,                               // 05 mov  r15d, edx
        0xF0, 0x44, 0x0F, 0xC1, 0x39,                   // 08 lock xadd [rcx], r15d
        0x42, 0x8D, 0x04, 0x3A,                         // 0D lea  eax, [rdx+r15]
        0x3D, 0x00, 0x00, 0x04, 0x00,                   // 11 cmp  eax, 40000h
        0x76, 0x0D,                                     // 16 jbe  25h
        0xF0, 0x29, 0x11,                               // 18 lock sub [rcx], edx
        0xF0, 0xFF, 0x05, 0xDE, 0x0F, 0x00, 0x00,       // 1B lock inc dword [rip+0xFDE]
        0x45, 0x33, 0xFF,                               // 22 xor  r15d, r15d
        0xE9, 0xF3, 0xAE, 0x16, 0x00,                   // 25 jmp  resume
    };

    private static readonly byte[] GoldenSite2 =
    {
        0xE9, 0xEB, 0x50, 0xE9, 0xFF,                   // jmp cave 2 (-0x16AF15)
        0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
    };

    private static readonly byte[] GoldenSite =
    {
        0xE9, 0xE7, 0x62, 0xE9, 0xFF,                   // jmp cave (-0x169D19)
        0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
    };

    [TestMethod]
    public void BuildCave_FixedAddresses_MatchesTheGoldenBytes()
    {
        var bytes = SkeletonBufferCave.BuildCave(Cave, Counter, Resume);

        CollectionAssert.AreEqual(GoldenCave, bytes);
    }

    [TestMethod]
    public void BuildCave_Result_IsTheDocumentedLengthAndTheOverflowBranchLandsOnTheXor()
    {
        var bytes = SkeletonBufferCave.BuildCave(Cave, Counter, Resume)!;

        Assert.AreEqual(SkeletonBufferCave.CaveLength, bytes.Length);
        Assert.AreEqual(0x2B, bytes.Length);
        // jbe at 0x16 is 76 0E: the next instruction is 0x18, and 0x18 + 0x0E is the final jmp at 0x26.
        Assert.AreEqual(0x76, bytes[0x16]);
        Assert.AreEqual(0x26, 0x18 + bytes[0x17]);
        Assert.AreEqual(0xE9, bytes[0x26]);
    }

    [TestMethod]
    public void BuildCave_CounterOnTheSecondPage_DisplacementIsFromTheEndOfTheIncrement()
    {
        var bytes = SkeletonBufferCave.BuildCave(Cave, Counter, Resume)!;

        var disp = System.BitConverter.ToInt32(bytes, 0x20);
        Assert.AreEqual(Counter, Cave + 0x24 + disp);
    }

    [TestMethod]
    public void BuildCave_ResumeCodeBelowTheCave_UsesANegativeJumpDisplacement()
    {
        var bytes = SkeletonBufferCave.BuildCave(0x7FF600200000, 0x7FF600201000, 0x7FF600069D21)!;

        Assert.AreEqual(0x7FF600069D21, 0x7FF600200000 + 0x2B + System.BitConverter.ToInt32(bytes, 0x27));
    }

    [TestMethod]
    public void BuildCave_ResumeMoreThan2GbAway_ReturnsNull()
    {
        Assert.IsNull(SkeletonBufferCave.BuildCave(Cave, Counter, Cave + 0x90000000L));
    }

    [TestMethod]
    public void BuildCave_CounterMoreThan2GbAway_ReturnsNull()
    {
        Assert.IsNull(SkeletonBufferCave.BuildCave(Cave, Cave + 0x90000000L, Resume));
    }

    [TestMethod]
    public void BuildCave_JustInsideTheJumpRange_StillBuilds()
    {
        // The jmp's rel32 is measured from cave+0x2B; the largest forward reach is int.MaxValue.
        var resume = Cave + 0x2B + int.MaxValue;

        Assert.IsNotNull(SkeletonBufferCave.BuildCave(Cave, Counter, resume));
        Assert.IsNull(SkeletonBufferCave.BuildCave(Cave, Counter, resume + 1));
    }

    [TestMethod]
    public void BuildSiteJump_FixedAddresses_MatchesTheGoldenBytes()
    {
        CollectionAssert.AreEqual(GoldenSite, SkeletonBufferCave.BuildSiteJump(Site, Cave));
    }

    [TestMethod]
    public void BuildSiteJump_Result_IsThirteenBytesJumpThenEightNops()
    {
        var bytes = SkeletonBufferCave.BuildSiteJump(Site, Cave)!;

        Assert.AreEqual(SkeletonBufferSignature.GuardSiteLength, bytes.Length);
        Assert.AreEqual(0xE9, bytes[0]);
        for (var i = 5; i < 13; i++) Assert.AreEqual(0x90, bytes[i], "byte " + i);
    }

    [TestMethod]
    public void BuildSiteJump_CaveMoreThan2GbAway_ReturnsNull()
    {
        Assert.IsNull(SkeletonBufferCave.BuildSiteJump(Site, Site - 0x80000000L));
    }

    [TestMethod]
    public void CounterAddress_IsTheSecondPageOfTheCave()
    {
        Assert.AreEqual(Counter, SkeletonBufferCave.CounterAddress(Cave));
        Assert.AreEqual(0x1000, SkeletonBufferCave.PageSize);
        Assert.AreEqual(0x2000, SkeletonBufferCave.AllocationSize);
    }

    // ---- pool 2 ----

    [TestMethod]
    public void BuildPool2Cave_FixedAddresses_MatchesTheGoldenBytes()
    {
        CollectionAssert.AreEqual(GoldenCave2, SkeletonBufferCave.BuildPool2Cave(Cave2, Counter2, Resume2));
    }

    [TestMethod]
    public void BuildPool2Cave_Result_IsTheDocumentedLengthAndTheOverflowBranchLandsOnTheFinalJump()
    {
        var bytes = SkeletonBufferCave.BuildPool2Cave(Cave2, Counter2, Resume2)!;

        Assert.AreEqual(SkeletonBufferCave.Pool2CaveLength, bytes.Length);
        Assert.AreEqual(0x2A, bytes.Length);
        // jbe at 0x16 is 76 0D: the next instruction is 0x18, and 0x18 + 0x0D is the final jmp at 0x25.
        Assert.AreEqual(0x76, bytes[0x16]);
        Assert.AreEqual(0x25, 0x18 + bytes[0x17]);
        Assert.AreEqual(0xE9, bytes[0x25]);
        // The capacity compared against is 32 blocks of 8,192 entries.
        Assert.AreEqual(262144, System.BitConverter.ToInt32(bytes, 0x12));
    }

    [TestMethod]
    public void BuildPool2Cave_CounterOnTheDataPage_DisplacementIsFromTheEndOfTheIncrement()
    {
        var bytes = SkeletonBufferCave.BuildPool2Cave(Cave2, Counter2, Resume2)!;

        Assert.AreEqual(Counter2, Cave2 + 0x22 + System.BitConverter.ToInt32(bytes, 0x1E));
    }

    [TestMethod]
    public void BuildPool2Cave_ResumeCodeBelowTheCave_UsesANegativeJumpDisplacement()
    {
        var bytes = SkeletonBufferCave.BuildPool2Cave(0x7FF600200040, 0x7FF600201040, 0x7FF60006AF5D)!;

        Assert.AreEqual(0x7FF60006AF5D, 0x7FF600200040 + 0x2A + System.BitConverter.ToInt32(bytes, 0x26));
    }

    [TestMethod]
    public void BuildPool2Cave_JustInsideTheJumpRangeAndCounterRange_StillBuildsAndOneMoreIsNull()
    {
        var resume = Cave2 + 0x2A + int.MaxValue;
        Assert.IsNotNull(SkeletonBufferCave.BuildPool2Cave(Cave2, Counter2, resume));
        Assert.IsNull(SkeletonBufferCave.BuildPool2Cave(Cave2, Counter2, resume + 1));

        var counter = Cave2 + 0x22 + int.MaxValue;
        Assert.IsNotNull(SkeletonBufferCave.BuildPool2Cave(Cave2, counter, Resume2));
        Assert.IsNull(SkeletonBufferCave.BuildPool2Cave(Cave2, counter + 1, Resume2));
    }

    [TestMethod]
    public void BuildPool2Cave_ResumeOrCounterMoreThan2GbBelow_ReturnsNull()
    {
        Assert.IsNull(SkeletonBufferCave.BuildPool2Cave(Cave2, Counter2, Cave2 - 0x90000000L));
        Assert.IsNull(SkeletonBufferCave.BuildPool2Cave(Cave2, Cave2 - 0x90000000L, Resume2));
    }

    [TestMethod]
    public void BuildSiteJump_PoolTwoSite_MatchesTheGoldenBytes()
    {
        CollectionAssert.AreEqual(GoldenSite2, SkeletonBufferCave.BuildSiteJump(Site2, Cave2));
    }

    [TestMethod]
    public void Layout_BothCavesAndBothCountersFitTheirOwnSlotsOnTheTwoPages()
    {
        Assert.AreEqual(Cave2, SkeletonBufferCave.Pool2CaveAddress(Cave));
        Assert.AreEqual(Counter2, SkeletonBufferCave.Pool2CounterAddress(Cave));
        Assert.AreEqual(0x40, SkeletonBufferCave.Pool2CaveOffset);
        Assert.IsTrue(SkeletonBufferCave.CaveLength <= SkeletonBufferCave.Pool2CaveOffset, "cave 1 ends before cave 2 starts");
        Assert.IsTrue(SkeletonBufferCave.Pool2CaveOffset + SkeletonBufferCave.Pool2CaveLength <= SkeletonBufferCave.PageSize, "cave 2 stays on the code page");
        Assert.AreEqual(Counter + 0x40, Counter2, "counter 2 is 0x40 after counter 1, on the data page");
    }
}
