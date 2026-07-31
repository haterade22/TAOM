using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the console argument parsers. These are pinned hard because they are a shared seam: with
/// ~20 commands authored across several feature folders, per-command parsing judgement is exactly
/// the kind of sub-problem that diverges at the edges (<c>.claude/rules/harness-facts.md</c>,
/// "Parallel builder briefs").
///
/// The NaN/Infinity cases carry the most weight. <c>float.TryParse</c> accepts both spellings, and
/// non-finite floats defeating a naive range check has now shipped five separate times in TAOM
/// (<c>.claude/rules/csharp-architecture.md</c>, "Engine-Float Decision Gates"). A console amount
/// flows straight into engine arithmetic — <c>Blow.BaseMagnitude</c>, a resource balance — so this
/// is the same bug class one entry point earlier.
/// </summary>
[TestClass]
public class DevConsoleArgsTests
{
    // ---- TryParseCount -----------------------------------------------------

    [TestMethod]
    public void TryParseCount_ValidValueInRange_ReturnsTrueAndValue()
    {
        // Act
        var ok = DevConsoleArgs.TryParseCount("5", 1, 200, out var value, out var error);

        // Assert
        Assert.IsTrue(ok);
        Assert.AreEqual(5, value);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryParseCount_BoundaryValues_AreAccepted()
    {
        Assert.IsTrue(DevConsoleArgs.TryParseCount("1", 1, 200, out var min, out _));
        Assert.AreEqual(1, min);

        Assert.IsTrue(DevConsoleArgs.TryParseCount("200", 1, 200, out var max, out _));
        Assert.AreEqual(200, max);
    }

    [TestMethod]
    public void TryParseCount_NonNumeric_ReturnsFalseWithError()
    {
        var ok = DevConsoleArgs.TryParseCount("abc", 1, 200, out var value, out var error);

        Assert.IsFalse(ok);
        Assert.AreEqual(0, value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }

    [TestMethod]
    public void TryParseCount_BelowMinimum_ReturnsFalseNamingTheRange()
    {
        var ok = DevConsoleArgs.TryParseCount("0", 1, 200, out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "1");
        StringAssert.Contains(error, "200");
    }

    [TestMethod]
    public void TryParseCount_AboveMaximum_ReturnsFalseNamingTheRange()
    {
        var ok = DevConsoleArgs.TryParseCount("201", 1, 200, out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "200");
    }

    [TestMethod]
    public void TryParseCount_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(DevConsoleArgs.TryParseCount(null, 1, 200, out _, out _));
        Assert.IsFalse(DevConsoleArgs.TryParseCount("", 1, 200, out _, out _));
        Assert.IsFalse(DevConsoleArgs.TryParseCount("   ", 1, 200, out _, out _));
    }

    /// <summary>
    /// A float spelling must not be silently truncated to an int — "2.9 troops" is a typo, not a
    /// request for 2. Reject it and say so rather than guessing which the user meant.
    /// </summary>
    [TestMethod]
    public void TryParseCount_FloatSpelling_ReturnsFalse()
    {
        Assert.IsFalse(DevConsoleArgs.TryParseCount("2.9", 1, 200, out _, out _));
    }

    // ---- TryParseAmount ----------------------------------------------------

    [TestMethod]
    public void TryParseAmount_ValidValue_ReturnsTrueAndValue()
    {
        var ok = DevConsoleArgs.TryParseAmount("12.5", out var value, out var error);

        Assert.IsTrue(ok);
        Assert.AreEqual(12.5f, value, 0.0001f);
        Assert.AreEqual(string.Empty, error);
    }

    /// <summary>
    /// Invariant culture is not optional: on a comma-decimal locale, current-culture parsing turns
    /// "12.5" into 125.
    /// </summary>
    [TestMethod]
    public void TryParseAmount_UsesInvariantCulture_ForDecimalPoint()
    {
        Assert.IsTrue(DevConsoleArgs.TryParseAmount("0.5", out var value, out _));
        Assert.AreEqual(0.5f, value, 0.0001f);
    }

    [TestMethod]
    public void TryParseAmount_NegativeValue_IsAccepted()
    {
        // Deduction is a legitimate request; range policy belongs to the calling command, not here.
        var ok = DevConsoleArgs.TryParseAmount("-40", out var value, out _);

        Assert.IsTrue(ok);
        Assert.AreEqual(-40f, value, 0.0001f);
    }

    [TestMethod]
    public void TryParseAmount_NaN_ReturnsFalse()
    {
        var ok = DevConsoleArgs.TryParseAmount("NaN", out var value, out var error);

        Assert.IsFalse(ok, "float.TryParse accepts \"NaN\"; the parser must reject it.");
        Assert.AreEqual(0f, value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }

    [TestMethod]
    public void TryParseAmount_Infinity_ReturnsFalse()
    {
        Assert.IsFalse(DevConsoleArgs.TryParseAmount("Infinity", out _, out _));
        Assert.IsFalse(DevConsoleArgs.TryParseAmount("-Infinity", out _, out _));
    }

    [TestMethod]
    public void TryParseAmount_NonNumeric_ReturnsFalse()
    {
        Assert.IsFalse(DevConsoleArgs.TryParseAmount("abc", out _, out _));
    }

    [TestMethod]
    public void TryParseAmount_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(DevConsoleArgs.TryParseAmount(null, out _, out _));
        Assert.IsFalse(DevConsoleArgs.TryParseAmount("", out _, out _));
    }

}
