// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf.AnimMemory;
using TAOM.Features.SkeletonBuffer;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>
/// Runs the six skeleton-buffer patterns against the installed <c>TaleWorlds.Native.dll</c> FILE (never process memory).
/// A failure after an engine bump is the prompt to re-derive the patterns (docs/features/skeleton-buffer-guard.md,
/// "After an engine bump"); in the game the guard would install nothing and write one reason line.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class SkeletonBufferInstalledBinaryTests
{
    /// <summary>Verified targets per client build, keyed by TaleWorlds.Native.dll file length. Add a row
    /// at every engine bump; an unlisted build is Inconclusive rather than a silent pass.</summary>
    private static readonly Dictionary<long, (string Build, int WatchSite, int Global, int GuardSite, int Pool2Load, int Pool2GuardSite)> KnownBuilds =
        new Dictionary<long, (string, int, int, int, int, int)>
        {
            [14209888] = ("v1.5.4", 0x69CE1, 0xD9D160, 0x69D14, 0x69AD5, 0x6AF50),
        };

    private static readonly byte[] OriginalSite =
    {
        0x41, 0x8B, 0xF4, 0x4C, 0x89, 0x7C, 0x24, 0x20, 0xF0, 0x0F, 0xC1, 0x75, 0x00,
    };

    private static readonly byte[] OriginalSite2 =
    {
        0x4C, 0x89, 0x7C, 0x24, 0x20, 0x44, 0x8B, 0xFA, 0xF0, 0x44, 0x0F, 0xC1, 0x39,
    };

    [TestMethod]
    public void InstalledNativeDll_Patterns_EachMatchOnceAndNeverInTheForeignShape()
    {
        var image = LoadInstalled(out _);
        var result = Resolve(image, out _, out var data, out _);

        Assert.AreEqual(1, result.WatchMatchCount, "watch pattern");
        Assert.AreEqual(1, result.GuardMatchCount, "guard pattern");
        Assert.AreEqual(0, result.ForeignGuardMatchCount, "the shape another module's patch leaves");
        Assert.AreEqual(1, result.Pool2MatchCount, "the second pool's load must be unique too");
        Assert.AreEqual(result.GlobalRva, result.Pool2GlobalRva, "the second pool reads the same global as the watch site");
        Assert.IsTrue(data.Contains(result.GlobalRva, 8), "the global must lie inside .data");
        Assert.AreEqual(0, result.GlobalRva % 8);
        var apart = result.GuardSiteRva - result.WatchSiteRva;
        Assert.IsTrue(apart > 0 && apart <= SkeletonBufferGuardService.MaxSiteDistance, "both sites in the same function, got " + apart);
        Assert.AreEqual(result.GuardSiteRva + 13, result.ResumeRva);
        Assert.AreEqual(1, result.Pool2GuardMatchCount, "pool 2's guard site must be unique too");
        Assert.AreEqual(0, result.Pool2ForeignGuardMatchCount, "the shape another module's patch at pool 2's site leaves");
        Assert.IsTrue(result.Pool2GuardCalledFromLoad, "pool 2's site lies in the function the pool 2 load calls");
        Assert.AreEqual(result.Pool2GuardSiteRva + 13, result.Pool2ResumeRva);
    }

    [TestMethod]
    public void InstalledNativeDll_KnownBuild_TargetsAreTheVerifiedRvas()
    {
        var image = LoadInstalled(out var length);
        if (!KnownBuilds.TryGetValue(length, out var expected))
            Assert.Inconclusive($"TaleWorlds.Native.dll is {length} bytes, which matches no verified build; add its row to KnownBuilds at the engine bump.");

        var result = Resolve(image, out _, out _, out var rawText);

        Assert.AreEqual(expected.WatchSite, result.WatchSiteRva, expected.Build);
        Assert.AreEqual(expected.Global, result.GlobalRva, expected.Build);
        Assert.AreEqual(expected.GuardSite, result.GuardSiteRva, expected.Build);
        Assert.AreEqual(expected.Pool2Load, result.Pool2LoadRva, expected.Build);
        Assert.AreEqual(expected.Global, result.Pool2GlobalRva, expected.Build);
        Assert.AreEqual(expected.GuardSite + 13, result.ResumeRva, expected.Build);
        // The 13 bytes the guard replaces are the original instructions at the site, byte for byte.
        var at = result.GuardSiteRva - rawText.VirtualAddress;
        var actual = new byte[13];
        Array.Copy(rawText.Bytes, at, actual, 0, 13);
        CollectionAssert.AreEqual(OriginalSite, actual);

        Assert.AreEqual(expected.Pool2GuardSite, result.Pool2GuardSiteRva, expected.Build);
        Assert.AreEqual(expected.Pool2GuardSite + 13, result.Pool2ResumeRva, expected.Build);
        var at2 = result.Pool2GuardSiteRva - rawText.VirtualAddress;
        var actual2 = new byte[13];
        Array.Copy(rawText.Bytes, at2, actual2, 0, 13);
        CollectionAssert.AreEqual(OriginalSite2, actual2);
    }

    private static byte[] LoadInstalled(out long length)
    {
        var dir = GameAssemblies.ResolveGameDir(
            Environment.GetEnvironmentVariable("BANNERLORD_OVERRIDE_DIR"),
            Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"),
            GameAssemblies.BuiltGameFolder);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            Assert.Inconclusive("No Bannerlord install folder resolved (BANNERLORD_OVERRIDE_DIR, BANNERLORD_GAME_DIR, built GameFolder).");
        var path = Path.Combine(dir!, "bin", "Win64_Shipping_Client", "TaleWorlds.Native.dll");
        if (!File.Exists(path))
            Assert.Inconclusive($"TaleWorlds.Native.dll not found at {path}.");
        var bytes = File.ReadAllBytes(path);
        length = bytes.LongLength;
        return bytes;
    }

    private sealed class RawText
    {
        public int VirtualAddress;
        public byte[] Bytes = Array.Empty<byte>();
    }

    private static SkeletonBufferSignatureResult Resolve(byte[] image, out PeSection text, out PeSection data, out RawText raw)
    {
        var headers = new byte[4096];
        Array.Copy(image, headers, Math.Min(headers.Length, image.Length));
        var sections = PeSectionTable.Parse(headers);
        Assert.IsNotNull(sections, "the installed DLL's PE headers did not parse");
        text = PeSectionTable.Find(sections!, ".text")!;
        data = PeSectionTable.Find(sections!, ".data")!;
        Assert.IsNotNull(text);
        Assert.IsNotNull(data);

        var rawLength = Math.Min(text.VirtualSize, text.SizeOfRawData);
        var rawText = new byte[rawLength];
        Array.Copy(image, text.PointerToRawData, rawText, 0, rawLength);
        raw = new RawText { VirtualAddress = text.VirtualAddress, Bytes = rawText };
        return SkeletonBufferSignature.Resolve(rawText, text.VirtualAddress);
    }
}
