using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.MixedFormations;

namespace TAOM.Tests.Features.MixedFormations;

/// <summary>
/// The Mixed Formations cycle hotkey is read every frame; <see cref="CachedEnumParse{TEnum}"/> parses
/// the setting string only when it changes instead of calling Enum.TryParse every frame. It returns
/// what the old trim-then-<c>Enum.TryParse(ignoreCase: true)</c> returned, except that a comma list
/// and any number are refused like any other unusable value: the parse rule is by member name only
/// (<see cref="EnumNames"/>). <see cref="DayOfWeek"/> stands in for the engine's InputKey;
/// <see cref="CycleHotkeyInputKeyTests"/> pins the same rules on the real enum, built the way production
/// builds it.
/// </summary>
[TestClass]
public class CachedEnumParseTests
{
    private int _calls;

    private CachedEnumParse<DayOfWeek> CountingParser(IModLogger? logger = null, string? label = null)
        => new CachedEnumParse<DayOfWeek>((string s, out DayOfWeek d) =>
        {
            _calls++;
            return Enum.TryParse(s, true, out d);
        }, logger, label);

    // The parse rule production composes (CycleHotkeyParser): a member NAME only, through EnumNames.
    private static CachedEnumParse<DayOfWeek> NameParser(IModLogger? logger = null)
        => new CachedEnumParse<DayOfWeek>(
            EnumNames.TryParse<DayOfWeek>, logger ?? Substitute.For<IModLogger>(), "[Test] cycle day");

    [TestInitialize]
    public void Setup() => _calls = 0;

    [TestMethod]
    public void TryGet_SameString_ParsesOnce()
    {
        var sut = CountingParser();

        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(sut.TryGet("Monday", out var day));
            Assert.AreEqual(DayOfWeek.Monday, day);
        }

        Assert.AreEqual(1, _calls);
    }

    [TestMethod]
    public void TryGet_EqualStringNewInstance_ParsesOnce()
    {
        var sut = CountingParser();

        sut.TryGet("Monday", out _);
        sut.TryGet(new string("Monday".ToCharArray()), out _);

        Assert.AreEqual(1, _calls);
    }

    [TestMethod]
    public void TryGet_ChangedString_ParsesAgain()
    {
        var sut = CountingParser();

        sut.TryGet("Monday", out _);
        Assert.IsTrue(sut.TryGet("Friday", out var day));

        Assert.AreEqual(DayOfWeek.Friday, day);
        Assert.AreEqual(2, _calls);
    }

    [TestMethod]
    public void TryGet_NullEmptyOrWhitespace_FalseWithoutParsing()
    {
        var sut = CountingParser();

        Assert.IsFalse(sut.TryGet(null, out var a));
        Assert.IsFalse(sut.TryGet("", out var b));
        Assert.IsFalse(sut.TryGet("   ", out var c));

        Assert.AreEqual(default(DayOfWeek), a);
        Assert.AreEqual(default(DayOfWeek), b);
        Assert.AreEqual(default(DayOfWeek), c);
        Assert.AreEqual(0, _calls);
    }

    [TestMethod]
    public void TryGet_UnknownName_FalseAndCachesTheFailure()
    {
        var sut = CountingParser();

        Assert.IsFalse(sut.TryGet("Blursday", out var first));
        Assert.IsFalse(sut.TryGet("Blursday", out var second));

        Assert.AreEqual(default(DayOfWeek), first);
        Assert.AreEqual(default(DayOfWeek), second);
        Assert.AreEqual(1, _calls);
    }

    [TestMethod]
    public void TryGet_PaddedMixedCase_MatchesTrimmedIgnoreCaseParse()
    {
        var sut = NameParser();

        Assert.IsTrue(sut.TryGet(" friday ", out var day));
        Assert.AreEqual(DayOfWeek.Friday, day);
    }

    // A setting names a member; it never gives a number. Enum.TryParse takes a number in several
    // spellings: "3" is Wednesday and "+3" and "003" are too, and Enum.IsDefined is true for each, so a
    // defined-number rule would keep them. "7", "99" and "-1" become DayOfWeek values no member has.
    // The setting is off and the log says so once, like any other unusable value.
    [DataTestMethod]
    [DataRow("3")]
    [DataRow("+3")]
    [DataRow("003")]
    [DataRow(" 3 ")]
    [DataRow("7")]
    [DataRow("99")]
    [DataRow("-1")]
    public void TryGet_Number_IsOffAndWarnsOnce(string raw)
    {
        var logger = Substitute.For<IModLogger>();
        var sut = NameParser(logger);

        Assert.IsFalse(sut.TryGet(raw, out var first));
        Assert.IsFalse(sut.TryGet(raw, out var second));

        Assert.AreEqual(default(DayOfWeek), first);
        Assert.AreEqual(default(DayOfWeek), second);
        logger.Received(1).LogWarning(
            $"[Test] cycle day '{raw}' is not a recognised DayOfWeek name; it is ignored until the setting changes");
        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    // Enum.TryParse ORs a comma list into one value. "Monday, Friday" is 1 | 5 = 5, which is a member
    // (Friday), so Enum.IsDefined alone would keep it; "Monday,Tuesday" is 1 | 2 = 3, Wednesday; and
    // "Wednesday, Thursday" is 3 | 4 = 7, no member. A setting names one key, so a comma is refused.
    [DataTestMethod]
    [DataRow("Monday, Friday")]
    [DataRow("Monday,Tuesday")]
    [DataRow("Wednesday, Thursday")]
    public void TryGet_CommaList_IsOffAndWarnsOnce(string raw)
    {
        var logger = Substitute.For<IModLogger>();
        var sut = NameParser(logger);

        Assert.IsFalse(sut.TryGet(raw, out var first));
        Assert.IsFalse(sut.TryGet(raw, out var second));

        Assert.AreEqual(default(DayOfWeek), first);
        Assert.AreEqual(default(DayOfWeek), second);
        logger.Received(1).LogWarning(
            $"[Test] cycle day '{raw}' is not a recognised DayOfWeek name; it is ignored until the setting changes");
        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    // The other half of the one-warning-per-change contract for the refusals: a different unusable
    // value warns again, a name that works in between does not.
    [TestMethod]
    public void TryGet_Refusals_WarnOncePerChangeAndRecoverOnAName()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = NameParser(logger);

        Assert.IsFalse(sut.TryGet("99", out _));
        Assert.IsFalse(sut.TryGet("Monday, Friday", out _));
        Assert.IsTrue(sut.TryGet("Friday", out var day));

        Assert.AreEqual(DayOfWeek.Friday, day);
        logger.Received(1).LogWarning(
            "[Test] cycle day '99' is not a recognised DayOfWeek name; it is ignored until the setting changes");
        logger.Received(1).LogWarning(
            "[Test] cycle day 'Monday, Friday' is not a recognised DayOfWeek name; it is ignored until the setting changes");
        logger.Received(2).LogWarning(Arg.Any<string>());
    }

    // The rules change the answer for a comma list and for any number. For every other input (a name,
    // padded or in any case, or text that is no member) the result is exactly the old
    // trim-then-Enum.TryParse(ignoreCase: true).
    [TestMethod]
    public void TryGet_ResultEqualsTheOldTrimThenTryParse_ForNamesAndNonNumbers()
    {
        var inputs = new[] { null, "", " ", "L", " friday ", "FRIDAY", "x" };
        var sut = NameParser();

        foreach (var raw in inputs)
        {
            var t = raw?.Trim();
            DayOfWeek oldValue = default;
            bool expected = !string.IsNullOrEmpty(t) && Enum.TryParse(t, true, out oldValue);

            bool actual = sut.TryGet(raw, out var value);

            Assert.AreEqual(expected, actual, "result for '" + raw + "'");
            if (expected)
                Assert.AreEqual(oldValue, value, "value for '" + raw + "'");
        }
    }

    // A failure that follows a success must not hand back the earlier key: the out value resets to
    // default whenever the result is false.
    [TestMethod]
    public void TryGet_FailureAfterASuccess_ReturnsDefault()
    {
        var sut = CountingParser();

        Assert.IsTrue(sut.TryGet("Monday", out _));
        Assert.IsFalse(sut.TryGet("", out var empty));
        Assert.IsTrue(sut.TryGet("Friday", out _));
        Assert.IsFalse(sut.TryGet("x", out var unknown));

        Assert.AreEqual(default(DayOfWeek), empty);
        Assert.AreEqual(default(DayOfWeek), unknown);
    }

    // An unusable setting turns the feature's hotkey off. That fallback is logged at WARNING (durable)
    // each time the setting changes to a value that is not a recognised name, never per frame, and the
    // line names the value, says it is not a recognised name and says what follows. One remembered
    // value, not a set: re-entering a bad value after a good one logs it again.
    [TestMethod]
    public void TryGet_UnusableValue_LogsOnceEachTimeTheSettingChangesToIt()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = CountingParser(logger, "[Test] cycle day");

        sut.TryGet("Blursday", out _);
        sut.TryGet("Blursday", out _);
        sut.TryGet("  ", out _);
        sut.TryGet("Monday", out _);
        sut.TryGet("Blursday", out _);

        logger.Received(2).LogWarning(
            "[Test] cycle day 'Blursday' is not a recognised DayOfWeek name; it is ignored until the setting changes");
        logger.Received(1).LogWarning(
            "[Test] cycle day '  ' is not a recognised DayOfWeek name; it is ignored until the setting changes");
        logger.Received(3).LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogInfo(Arg.Any<string>());
        logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void TryGet_NullValue_LogsTheEmptyQuotes()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = NameParser(logger);

        sut.TryGet(null, out _);

        logger.Received(1).LogWarning(
            "[Test] cycle day '' is not a recognised DayOfWeek name; it is ignored until the setting changes");
    }

    [TestMethod]
    public void TryGet_ParsableValue_LogsNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = NameParser(logger);

        sut.TryGet("Monday", out _);
        sut.TryGet(" friday ", out _);

        Assert.AreEqual(0, logger.ReceivedCalls().Count());
    }
}
