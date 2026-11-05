using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LocalizationOverride;

namespace TAOM.Tests.Features.LocalizationOverride;

/// <summary>
/// The override table's key: a (string, start, length) slice that must compare and hash exactly as the
/// ordinal string <c>Substring(start, length)</c> key it replaced, so the per-call probe can read the id
/// in place.
/// </summary>
[TestClass]
public class IdSliceTests
{
    [TestMethod]
    public void EqualSlicesOfDifferentStrings_AreEqualAndHashAlike()
    {
        var probe = new IdSlice("{=abc}text", 2, 3);
        var key = new IdSlice("abc", 0, 3);

        Assert.IsTrue(probe.Equals(key));
        Assert.IsTrue(key.Equals((object)probe));
        Assert.AreEqual(key.GetHashCode(), probe.GetHashCode());
    }

    [TestMethod]
    public void APrefixOfTheOtherSlice_IsNotEqual()
    {
        Assert.IsFalse(new IdSlice("abcd", 0, 4).Equals(new IdSlice("{=abc}", 2, 3)));
        Assert.IsFalse(new IdSlice("abc", 0, 3).Equals(new IdSlice("{=abcd}", 2, 4)));
    }

    [TestMethod]
    public void CaseDiffers_IsNotEqual()
    {
        Assert.IsFalse(new IdSlice("ABC", 0, 3).Equals(new IdSlice("{=abc}", 2, 3)));
    }

    [TestMethod]
    public void EmptySlices_AreEqualAndHashAlike()
    {
        var probe = new IdSlice("{=}text", 2, 0);
        var key = new IdSlice(string.Empty, 0, 0);

        Assert.IsTrue(probe.Equals(key));
        Assert.AreEqual(key.GetHashCode(), probe.GetHashCode());
    }

    [TestMethod]
    public void NotEqualToABoxedStringOrNull()
    {
        var key = new IdSlice("abc", 0, 3);

        Assert.IsFalse(key.Equals((object)"abc"));
        Assert.IsFalse(key.Equals((object?)null));
    }
}
