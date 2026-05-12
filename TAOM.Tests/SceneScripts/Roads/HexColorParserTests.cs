// Tests for TAOM's clean-room reimplementation; behavioural inspiration: Alliance
// mod (GPL v3). See docs/scene-scripts/ATTRIBUTION.md.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.SceneScripts.Roads;

namespace TAOM.Tests.SceneScripts.Roads;

[TestClass]
public class HexColorParserTests
{
    [TestMethod]
    public void TryParse_NineCharOpaqueWhite_ReturnsPackedArgbWhite()
    {
        bool ok = HexColorParser.TryParse("#ffffffff", out uint result);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xFFFFFFFFu, result);
    }

    [TestMethod]
    public void TryParse_NineCharOpaqueRed_ReturnsPackedArgbRed()
    {
        bool ok = HexColorParser.TryParse("#ff0000ff", out uint result);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xFFFF0000u, result, "Packed ARGB for opaque red: (A=FF<<24)|(R=FF<<16)|(G=00<<8)|B=00");
    }

    [TestMethod]
    public void TryParse_NineCharHalfTransparentBlue_PreservesAlphaChannel()
    {
        bool ok = HexColorParser.TryParse("#0000ff80", out uint result);

        Assert.IsTrue(ok);
        Assert.AreEqual(0x800000FFu, result, "Packed ARGB: (A=80<<24)|(R=00<<16)|(G=00<<8)|B=FF");
    }

    [TestMethod]
    public void TryParse_SevenCharNoAlpha_DefaultsAlphaToOpaque()
    {
        bool ok = HexColorParser.TryParse("#ffffff", out uint result);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xFFFFFFFFu, result);
    }

    [TestMethod]
    public void TryParse_SevenCharRed_DefaultsAlphaToOpaque()
    {
        bool ok = HexColorParser.TryParse("#ff0000", out uint result);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xFFFF0000u, result);
    }

    [TestMethod]
    public void TryParse_UppercaseHex_ParsesIdenticallyToLowercase()
    {
        HexColorParser.TryParse("#AaBbCcDd", out uint upper);
        HexColorParser.TryParse("#aabbccdd", out uint lower);

        Assert.AreEqual(upper, lower);
    }

    [TestMethod]
    public void TryParse_MissingHashPrefix_Fails()
    {
        bool ok = HexColorParser.TryParse("ffffffff", out uint result);

        Assert.IsFalse(ok);
        Assert.AreEqual(0u, result);
    }

    [TestMethod]
    public void TryParse_WrongLength_Fails()
    {
        Assert.IsFalse(HexColorParser.TryParse("#abc", out _));
        Assert.IsFalse(HexColorParser.TryParse("#abcde", out _));
        Assert.IsFalse(HexColorParser.TryParse("#abcdefg", out _));
        Assert.IsFalse(HexColorParser.TryParse("#abcdefghi", out _));
    }

    [TestMethod]
    public void TryParse_NonHexCharacters_Fails()
    {
        Assert.IsFalse(HexColorParser.TryParse("#gggggg", out _));
        Assert.IsFalse(HexColorParser.TryParse("#ffxxff", out _));
    }

    [TestMethod]
    public void TryParse_NullInput_Fails()
    {
        bool ok = HexColorParser.TryParse(null, out uint result);

        Assert.IsFalse(ok);
        Assert.AreEqual(0u, result);
    }

    [TestMethod]
    public void TryParse_EmptyString_Fails()
    {
        bool ok = HexColorParser.TryParse("", out _);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void ToPackedArgb_InvalidInput_ReturnsProvidedFallback()
    {
        uint result = HexColorParser.ToPackedArgb("not a color", fallback: 0xDEADBEEFu);

        Assert.AreEqual(0xDEADBEEFu, result);
    }

    [TestMethod]
    public void ToPackedArgb_InvalidInputDefaultFallback_ReturnsOpaqueWhite()
    {
        uint result = HexColorParser.ToPackedArgb("nope");

        Assert.AreEqual(HexColorParser.OpaqueWhitePackedArgb, result);
    }

    [TestMethod]
    public void ToPackedArgb_ValidInput_ReturnsParsedValue()
    {
        uint result = HexColorParser.ToPackedArgb("#0000ffff");

        Assert.AreEqual(0xFF0000FFu, result, "Packed ARGB: (A=FF<<24)|(R=00<<16)|(G=00<<8)|B=FF");
    }
}
