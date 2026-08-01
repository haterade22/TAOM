using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;
using TAOM.Features.TimeAcceleration.UI;

namespace TAOM.Tests.Features.CoopInterop;

[TestClass]
public class CoopUiRegistrationPolicyTests
{
    [CoopSuppressedUi("test")]
    private class SuppressedType { }

    private class PlainType { }

    [TestMethod]
    public void Filter_CoopInactive_KeepsSuppressedTypes()
    {
        // Arrange
        var candidates = new[] { typeof(SuppressedType), typeof(PlainType) };

        // Act
        var result = CoopUiRegistrationPolicy.Filter(candidates, coopActive: false);

        // Assert — solo play is unaffected; this is the overwhelmingly common case.
        CollectionAssert.AreEquivalent(candidates, result.ToArray());
    }

    [TestMethod]
    public void Filter_CoopActive_DropsSuppressedTypes()
    {
        // Arrange
        var candidates = new[] { typeof(SuppressedType), typeof(PlainType) };

        // Act
        var result = CoopUiRegistrationPolicy.Filter(candidates, coopActive: true);

        // Assert
        CollectionAssert.AreEqual(new[] { typeof(PlainType) }, result.ToArray());
    }

    [TestMethod]
    public void Filter_CoopActive_KeepsUnmarkedTypes()
    {
        var result = CoopUiRegistrationPolicy.Filter(new[] { typeof(PlainType) }, coopActive: true);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Filter_NullCandidates_ReturnsEmpty()
    {
        Assert.AreEqual(0, CoopUiRegistrationPolicy.Filter(null, coopActive: true).Count);
    }

    [TestMethod]
    public void Filter_NullEntries_AreSkipped()
    {
        var result = CoopUiRegistrationPolicy.Filter(
            new[] { typeof(PlainType), null }, coopActive: false);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Suppressed_ReportsMarkedTypesRegardlessOfCoopState()
    {
        var result = CoopUiRegistrationPolicy.Suppressed(
            new[] { typeof(SuppressedType), typeof(PlainType) });

        CollectionAssert.AreEqual(new[] { typeof(SuppressedType) }, result.ToArray());
    }

    // --- Invariant pin (#370) --------------------------------------------------------------
    // These are the real production types the gate exists for. BannerlordTogether's
    // TimeControlModePatch prefixes the Campaign.TimeControlMode setter and overwrites the value
    // whenever a co-op session is active, so every one of these presents a dead control. If
    // someone removes the attribute, this test fails rather than the button silently returning.

    [TestMethod]
    public void TimeAccelerationUi_IsMarkedCoopSuppressed()
    {
        var uiTypes = CoopUiRegistrationPolicy
            .CollectUiExtensionTypes(typeof(TimeAccelerationMixin).Assembly)
            .Where(t => t.Namespace == "TAOM.Features.TimeAcceleration.UI")
            .ToList();

        Assert.AreNotEqual(0, uiTypes.Count, "expected TimeAcceleration UI extension types");

        var unmarked = uiTypes
            .Where(t => !Attribute.IsDefined(t, typeof(CoopSuppressedUiAttribute)))
            .Select(t => t.Name)
            .ToList();

        Assert.AreEqual(
            0, unmarked.Count,
            "TimeAcceleration UI types missing [CoopSuppressedUi]: " + string.Join(", ", unmarked));
    }
}
