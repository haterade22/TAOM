using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.InputSystem;
using TAOM.Core.Logging;
using TAOM.Features.MixedFormations;

namespace TAOM.Tests.Features.MixedFormations;

/// <summary>
/// The cycle hotkey as production builds it (<see cref="CycleHotkeyParser.Create"/>): the setting must
/// be the name of one <see cref="InputKey"/> member, and not Invalid. <see cref="CachedEnumParseTests"/>
/// proves the cache on a stand-in enum; this pins the examples that decided the rule against the
/// installed engine's member values, which a stand-in cannot: "3" is the key D2 (so are "+3" and
/// "003"), "L, K" is 38 | 37 = 39, the key SemiColon, and Invalid is -1, a defined member, so neither
/// Enum.IsDefined nor a plain name lookup turns all of these off. Each premise is asserted, so an
/// engine bump that adds or moves a member fails here with the number to re-pick.
/// </summary>
[TestClass]
public class CycleHotkeyInputKeyTests
{
    private const string Label = "[MixedFormations] cycle hotkey";

    private IModLogger _logger = null!;
    private CachedEnumParse<InputKey> _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = CycleHotkeyParser.Create(_logger);
    }

    private static string NotRecognised(string raw)
        => $"{Label} '{raw}' is not a recognised InputKey name; it is ignored until the setting changes";

    // Off (false, default key) on every read, and exactly one warning however often it is read.
    private void AssertOffWithOneWarning(string raw)
    {
        Assert.IsFalse(_sut.TryGet(raw, out var first));
        Assert.IsFalse(_sut.TryGet(raw, out var second));

        Assert.AreEqual(default(InputKey), first);
        Assert.AreEqual(default(InputKey), second);
        _logger.Received(1).LogWarning(NotRecognised(raw));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("L", InputKey.L)]
    [DataRow(" f5 ", InputKey.F5)]
    [DataRow("semicolon", InputKey.SemiColon)]
    [DataRow("D2", InputKey.D2)]
    public void TryGet_KeyName_ReturnsThatKeyWithoutAWarning(string raw, InputKey expected)
    {
        Assert.IsTrue(_sut.TryGet(raw, out var key));

        Assert.AreEqual(expected, key);
        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    // "3" is the key D2 to Enum.TryParse, and Enum.IsDefined is true for it, so a player who types 3
    // would silently get the 2 key. A number is never a key name, in any spelling: Int32 parsing takes
    // a sign and leading zeros, so "+3" and "003" are D2 too.
    [DataTestMethod]
    [DataRow("3")]
    [DataRow("+3")]
    [DataRow("003")]
    [DataRow(" 3 ")]
    public void TryGet_NumberThatIsAKeysValue_IsOffAndWarnsOnce(string raw)
    {
        Assert.IsTrue(Enum.TryParse(raw.Trim(), true, out InputKey parsed) && parsed == InputKey.D2,
            "premise: Enum.TryParse turns this number into the key D2");
        Assert.IsTrue(Enum.IsDefined(typeof(InputKey), 3), "premise: 3 is a defined InputKey value");

        AssertOffWithOneWarning(raw);
    }

    [DataTestMethod]
    [DataRow("0", 0)]
    [DataRow("216", 216)]
    [DataRow("999", 999)]
    [DataRow("-2", -2)]
    public void TryGet_NumberOfNoKey_IsOffAndWarnsOnce(string raw, int number)
    {
        Assert.IsFalse(Enum.IsDefined(typeof(InputKey), number), $"premise: {number} is not an InputKey value");

        AssertOffWithOneWarning(raw);
    }

    // InputKey.Invalid (-1) is a defined member, so Enum.IsDefined and a name lookup both accept it,
    // but it is the engine's "no key" value, never a key the player can press. Refused by its number
    // and by its name.
    [DataTestMethod]
    [DataRow("-1")]
    [DataRow("Invalid")]
    [DataRow("invalid")]
    [DataRow(" INVALID ")]
    public void TryGet_TheInvalidSentinel_IsOffAndWarnsOnce(string raw)
    {
        Assert.IsTrue(Enum.IsDefined(typeof(InputKey), -1), "premise: -1 is a defined InputKey value");
        Assert.AreEqual(-1, (int)InputKey.Invalid, "premise: Invalid is -1");

        AssertOffWithOneWarning(raw);
    }

    // "L, K" is the review's example, but Enum.TryParse ORs it to 38 | 37 = 39, which is the key
    // SemiColon, so Enum.IsDefined alone keeps it. Only a by-name parse turns it off: no member is
    // named "L, K", and a setting names one key.
    [TestMethod]
    public void TryGet_CommaListThatOrsToAKey_IsOffAndWarnsOnce()
    {
        Assert.IsTrue(Enum.TryParse("L, K", true, out InputKey ored) && ored == InputKey.SemiColon,
            "premise: Enum.TryParse turns L, K into the key SemiColon");

        AssertOffWithOneWarning("L, K");
    }

    // "Up, Down" ORs to 200 | 208 = 216, which is no key: Enum.IsDefined would refuse it too.
    [TestMethod]
    public void TryGet_CommaListThatOrsToNoKey_IsOffAndWarnsOnce()
    {
        Assert.IsTrue(Enum.TryParse("Up, Down", true, out InputKey ored) && !Enum.IsDefined(typeof(InputKey), ored),
            "premise: Enum.TryParse turns Up, Down into a value no key has");

        AssertOffWithOneWarning("Up, Down");
    }

    // The example in docs/features/mixed-formations.md: a chord is not a key name.
    [TestMethod]
    public void TryGet_UnknownName_WarningSaysTheKeyNameWasNotRecognised()
    {
        Assert.IsFalse(_sut.TryGet("Ctrl+L", out _));

        _logger.Received(1).LogWarning(
            "[MixedFormations] cycle hotkey 'Ctrl+L' is not a recognised InputKey name; it is ignored until the setting changes");
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }
}
