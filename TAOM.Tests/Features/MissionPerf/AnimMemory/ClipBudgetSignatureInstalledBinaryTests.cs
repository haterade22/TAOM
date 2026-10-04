using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf.AnimMemory;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// Runs the eviction-pass signature against the installed <c>TaleWorlds.Native.dll</c> on disk. A
/// failure here after an engine bump is the prompt to re-derive the pattern with
/// <c>tools/native_sig_author.py</c>; the probe itself would disable with one reason line.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class ClipBudgetSignatureInstalledBinaryTests
{
    private const long V153FileLength = 14209376;

    [TestMethod]
    public void InstalledNativeDll_Signature_MatchesOnceWithTargetsInDataAndRdataAndA12MiBBudget()
    {
        var image = LoadInstalled(out _);
        var match = Resolve(image, out var rdata, out var data);

        Assert.AreEqual(1, match.MatchCount);
        Assert.IsTrue(data.Contains(match.CounterRva, 4), "the counter target must lie inside .data");
        Assert.IsTrue(rdata.Contains(match.BudgetRva, 4), "the budget target must lie inside .rdata");
        Assert.AreEqual(0, match.CounterRva % 4);
        Assert.AreEqual(0, match.BudgetRva % 4);
        var budget = BitConverter.ToSingle(image, rdata.PointerToRawData + (match.BudgetRva - rdata.VirtualAddress));
        Assert.AreEqual(12582912f, budget);
    }

    [TestMethod]
    public void InstalledNativeDll_V153Binary_TargetsAreTheVerifiedRvas()
    {
        var image = LoadInstalled(out var length);
        if (length != V153FileLength)
            Assert.Inconclusive($"TaleWorlds.Native.dll is {length} bytes, not the v1.5.3 build's {V153FileLength}; the pinned RVAs do not apply.");

        var match = Resolve(image, out _, out _);

        Assert.AreEqual(0x21E00F, match.LoadSiteRva);
        Assert.AreEqual(0x21E034, match.BudgetSiteRva);
        Assert.AreEqual(0xDABE40, match.CounterRva);
        Assert.AreEqual(0xB2E2DC, match.BudgetRva);
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

    private static SignatureMatch Resolve(byte[] image, out PeSection rdata, out PeSection data)
    {
        var headers = new byte[4096];
        Array.Copy(image, headers, Math.Min(headers.Length, image.Length));
        var sections = PeSectionTable.Parse(headers);
        Assert.IsNotNull(sections, "the installed DLL's PE headers did not parse");
        var text = PeSectionTable.Find(sections!, ".text");
        rdata = PeSectionTable.Find(sections!, ".rdata")!;
        data = PeSectionTable.Find(sections!, ".data")!;
        Assert.IsNotNull(text);
        Assert.IsNotNull(rdata);
        Assert.IsNotNull(data);

        var rawLength = Math.Min(text!.VirtualSize, text.SizeOfRawData);
        var rawText = new byte[rawLength];
        Array.Copy(image, text.PointerToRawData, rawText, 0, rawLength);
        return ClipBudgetSignature.Resolve(rawText, text.VirtualAddress);
    }
}
