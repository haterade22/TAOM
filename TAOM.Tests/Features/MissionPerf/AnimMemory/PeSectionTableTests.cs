using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf.AnimMemory;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// The PE32+ section table parser the clip memory probe uses to find the code range it copies and to
/// check its two target addresses against the module's own section table.
/// </summary>
[TestClass]
public class PeSectionTableTests
{
    [TestMethod]
    public void Parse_SyntheticHeaders_ReturnsEachSection()
    {
        var sections = PeSectionTable.Parse(SyntheticNativeImage.Headers(SyntheticNativeImage.Standard));

        Assert.IsNotNull(sections);
        Assert.AreEqual(3, sections!.Count);
        Assert.AreEqual(".text", sections[0].Name);
        Assert.AreEqual(0x1000, sections[0].VirtualAddress);
        Assert.AreEqual(0x200, sections[0].VirtualSize);
        Assert.AreEqual(0x200, sections[0].SizeOfRawData);
        Assert.AreEqual(0x1000, sections[0].PointerToRawData);
        Assert.AreEqual(".rdata", sections[1].Name);
        Assert.AreEqual(0x2000, sections[1].VirtualAddress);
        Assert.AreEqual(0x100, sections[1].VirtualSize);
        Assert.AreEqual(".data", sections[2].Name);
        Assert.AreEqual(0x3000, sections[2].VirtualAddress);
        Assert.AreEqual(0x3000, sections[2].PointerToRawData);
    }

    [TestMethod]
    public void Parse_NullOrShorterThanDosHeader_ReturnsNull()
    {
        Assert.IsNull(PeSectionTable.Parse(null!));
        var shortBuffer = new byte[0x3F];
        shortBuffer[0] = (byte)'M';
        shortBuffer[1] = (byte)'Z';
        Assert.IsNull(PeSectionTable.Parse(shortBuffer));
    }

    [TestMethod]
    public void Parse_NegativeELfanew_ReturnsNull()
    {
        var h = SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);
        BitConverter.GetBytes(-4).CopyTo(h, 0x3C);

        Assert.IsNull(PeSectionTable.Parse(h));
    }

    [TestMethod]
    public void Parse_NoMzSignature_ReturnsNull()
    {
        var h = SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);
        h[0] = 0;

        Assert.IsNull(PeSectionTable.Parse(h));
    }

    [TestMethod]
    public void Parse_ELfanewOutsideBuffer_ReturnsNull()
    {
        var h = SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);
        SyntheticNativeImage.PutInt32(h, 0x3C, 0x1000);

        Assert.IsNull(PeSectionTable.Parse(h));
    }

    [TestMethod]
    public void Parse_NoPeSignature_ReturnsNull()
    {
        var h = SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);
        h[0x81] = 0;

        Assert.IsNull(PeSectionTable.Parse(h));
    }

    [TestMethod]
    public void Parse_SectionTablePastBuffer_ReturnsNull()
    {
        var h = SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);
        SyntheticNativeImage.PutUInt16(h, 0x86, 200);

        Assert.IsNull(PeSectionTable.Parse(h));
    }

    [TestMethod]
    public void Parse_MachineNotX64_ReturnsNull()
    {
        var h = SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);
        SyntheticNativeImage.PutUInt16(h, 0x84, 0x014C);

        Assert.IsNull(PeSectionTable.Parse(h));
    }

    [TestMethod]
    public void Parse_MagicNotPe32Plus_ReturnsNull()
    {
        var h = SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);
        SyntheticNativeImage.PutUInt16(h, 0x98, 0x010B);

        Assert.IsNull(PeSectionTable.Parse(h));
    }

    [TestMethod]
    public void Find_MissingName_ReturnsNull()
    {
        var sections = PeSectionTable.Parse(SyntheticNativeImage.Headers(SyntheticNativeImage.Standard))!;

        Assert.IsNull(PeSectionTable.Find(sections, ".pdata"));
        Assert.IsNotNull(PeSectionTable.Find(sections, ".data"));
    }

    [TestMethod]
    public void Contains_FirstAndLastAlignedInt_True_OnePastEnd_False()
    {
        var sections = PeSectionTable.Parse(SyntheticNativeImage.Headers(SyntheticNativeImage.Standard))!;
        var data = PeSectionTable.Find(sections, ".data")!;

        Assert.IsTrue(data.Contains(0x3000, 4));
        Assert.IsTrue(data.Contains(0x30FC, 4));
        Assert.IsFalse(data.Contains(0x30FD, 4));
        Assert.IsFalse(data.Contains(0x2FFC, 4));
    }
}
