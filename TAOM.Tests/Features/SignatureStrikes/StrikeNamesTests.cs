using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The config's direction and kind strings resolve by member NAME only. <c>Enum.TryParse</c> also
/// accepts "1" and "Left, Right", and <c>Enum.IsDefined</c> is true for any defined numeric value,
/// so a typo like "1" would have become a live Overhead row instead of a dropped one (Codex review
/// 114, F2).
/// </summary>
[TestClass]
public class StrikeNamesTests
{
    [TestMethod]
    public void TryParseDirection_MemberName_IsCaseInsensitive()
    {
        Assert.IsTrue(StrikeNames.TryParseDirection("overhead", out var direction));
        Assert.AreEqual(StrikeDirection.Overhead, direction);
    }

    [TestMethod]
    public void TryParseDirection_TrimsWhitespace()
    {
        Assert.IsTrue(StrikeNames.TryParseDirection(" Left ", out var direction));
        Assert.AreEqual(StrikeDirection.Left, direction);
    }

    [TestMethod]
    public void TryParseDirection_NumericString_IsRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseDirection("1", out var direction));
        Assert.AreEqual(StrikeDirection.None, direction);
    }

    [TestMethod]
    public void TryParseDirection_NegativeNumericString_IsRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseDirection("-1", out _));
    }

    [TestMethod]
    public void TryParseDirection_CommaComposedFlags_AreRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseDirection("Left, Right", out _));
    }

    [TestMethod]
    public void TryParseDirection_None_IsRejectedAsAConfigRow()
    {
        Assert.IsFalse(StrikeNames.TryParseDirection("None", out _));
    }

    [TestMethod]
    public void TryParseDirection_NullOrEmpty_IsRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseDirection(null, out _));
        Assert.IsFalse(StrikeNames.TryParseDirection("", out _));
        Assert.IsFalse(StrikeNames.TryParseDirection("   ", out _));
    }

    [TestMethod]
    public void TryParseKind_MemberName_IsCaseInsensitive()
    {
        Assert.IsTrue(StrikeNames.TryParseKind("sweep", out var kind));
        Assert.AreEqual(StrikeKind.Sweep, kind);
    }

    [TestMethod]
    public void TryParseKind_NumericString_IsRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseKind("1", out _));
        Assert.IsFalse(StrikeNames.TryParseKind("0", out _));
    }

    [TestMethod]
    public void TryParseKind_UnknownName_IsRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseKind("Nova", out _));
    }

    [TestMethod]
    public void TryParseKind_Scream_Parses()
    {
        Assert.IsTrue(StrikeNames.TryParseKind("scream", out var kind));
        Assert.AreEqual(StrikeKind.Scream, kind);
    }

    [TestMethod]
    public void TryParseOrigin_MemberName_IsCaseInsensitive()
    {
        Assert.IsTrue(StrikeNames.TryParseOrigin("self", out var origin));
        Assert.AreEqual(StrikeOrigin.Self, origin);
        Assert.IsTrue(StrikeNames.TryParseOrigin(" Impact ", out origin));
        Assert.AreEqual(StrikeOrigin.Impact, origin);
    }

    [TestMethod]
    public void TryParseOrigin_NumericString_IsRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseOrigin("1", out _));
    }

    [TestMethod]
    public void TryParseOrigin_UnknownOrEmpty_IsRejected()
    {
        Assert.IsFalse(StrikeNames.TryParseOrigin("Sky", out _));
        Assert.IsFalse(StrikeNames.TryParseOrigin(null, out _));
        Assert.IsFalse(StrikeNames.TryParseOrigin("  ", out _));
    }
}
