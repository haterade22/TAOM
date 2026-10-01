using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleCorpses;

namespace TAOM.Tests.Features.BattleCorpses;

/// <summary>
/// The per-battle corpse limits (#701). Native's mission reset puts the fade time back to its
/// 3,600 s default and clears the corpse override every mission, and the override REPLACES the
/// player's own cap, so the policy has to pick the lower of the two itself.
/// </summary>
[TestClass]
public class BattleCorpsePolicyTests
{
    private IBattleCorpseSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private BattleCorpsePolicy _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBattleCorpseSettingsProvider>();
        _logger = Substitute.For<IModLogger>();
        _settings.IsCleanupEnabled.Returns(true);
        _settings.FadeSeconds.Returns(60f);
        _settings.CorpseCap.Returns(25);
        _sut = new BattleCorpsePolicy(_settings, _logger);
    }

    // -------- The player's option, as native maps it --------

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 25)]
    [DataRow(2, 75)]
    [DataRow(3, 125)]
    [DataRow(4, 250)]
    [DataRow(5, 1021)]
    public void CorpseCapForOption_MatchesNativeTable(int option, int expected)
    {
        Assert.AreEqual(expected, BattleCorpsePolicy.CorpseCapForOption(option));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(6)]
    [DataRow(int.MinValue)]
    public void CorpseCapForOption_UnknownOption_IsUnknown(int option)
    {
        Assert.AreEqual(BattleCorpsePolicy.UnknownCap, BattleCorpsePolicy.CorpseCapForOption(option));
    }

    // -------- Resolve --------

    [TestMethod]
    public void Resolve_Unlimited_UsesTaomCapAndFade()
    {
        var limits = _sut.Resolve(playerCorpseOption: 5);

        Assert.IsTrue(limits.IsOverridden);
        Assert.AreEqual(60f, limits.FadeSeconds);
        Assert.AreEqual(25, limits.CorpseCap);
    }

    [TestMethod]
    public void Resolve_PlayerBelowTaomCap_KeepsThePlayersLowerCap()
    {
        // The override replaces the player's cap, so passing TAOM's 25 to a player on None would
        // RAISE their corpse count from 0 to 25.
        var limits = _sut.Resolve(playerCorpseOption: 0);

        Assert.AreEqual(0, limits.CorpseCap);
    }

    [TestMethod]
    public void Resolve_UnknownPlayerOption_UsesTaomCap()
    {
        Assert.AreEqual(25, _sut.Resolve(playerCorpseOption: 9).CorpseCap);
    }

    [TestMethod]
    public void Resolve_Disabled_RestoresEngineDefaults()
    {
        // A mid-battle toggle-off must hand the mission back to native's own behaviour, which is
        // what -1 means to both setters.
        _settings.IsCleanupEnabled.Returns(false);

        var limits = _sut.Resolve(playerCorpseOption: 5);

        Assert.IsFalse(limits.IsOverridden);
        Assert.AreEqual(-1f, limits.FadeSeconds);
        Assert.AreEqual(-1, limits.CorpseCap);
        Assert.AreEqual(BattleCorpseLimits.EngineDefaults, limits);
    }

    // -------- Validation --------

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(9.9f)]
    [DataRow(300.1f)]
    [DataRow(-1f)]
    public void Resolve_BadFadeSeconds_FallsBackToDefaultAndWarns(float raw)
    {
        _settings.FadeSeconds.Returns(raw);

        var limits = _sut.Resolve(playerCorpseOption: 5);

        Assert.AreEqual(BattleCorpsePolicy.DefaultFadeSeconds, limits.FadeSeconds);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("BattleCorpses")));
    }

    [DataTestMethod]
    [DataRow(10f)]
    [DataRow(300f)]
    public void Resolve_FadeSecondsAtBounds_IsKept(float raw)
    {
        _settings.FadeSeconds.Returns(raw);

        Assert.AreEqual(raw, _sut.Resolve(playerCorpseOption: 5).FadeSeconds);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(251)]
    [DataRow(int.MaxValue)]
    public void Resolve_BadCorpseCap_FallsBackToDefaultAndWarns(int raw)
    {
        _settings.CorpseCap.Returns(raw);

        var limits = _sut.Resolve(playerCorpseOption: 5);

        Assert.AreEqual(BattleCorpsePolicy.DefaultCorpseCap, limits.CorpseCap);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("BattleCorpses")));
    }

    [TestMethod]
    public void Resolve_SameBadValueTwice_WarnsOnce()
    {
        // The mission behavior resolves once a second; a bad slider must not flood the log.
        _settings.FadeSeconds.Returns(float.NaN);

        _sut.Resolve(playerCorpseOption: 5);
        _sut.Resolve(playerCorpseOption: 5);

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Resolve_BadValueFixedThenBrokenAgain_WarnsAgain()
    {
        _settings.FadeSeconds.Returns(float.NaN);
        _sut.Resolve(playerCorpseOption: 5);
        _settings.FadeSeconds.Returns(60f);
        _sut.Resolve(playerCorpseOption: 5);
        _settings.FadeSeconds.Returns(float.NaN);
        _sut.Resolve(playerCorpseOption: 5);

        _logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Resolve_CapAtUpperBound_IsKept()
    {
        _settings.CorpseCap.Returns(250);

        Assert.AreEqual(250, _sut.Resolve(playerCorpseOption: 5).CorpseCap);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Resolve_SameBadCapTwice_WarnsOnce()
    {
        _settings.CorpseCap.Returns(-5);

        _sut.Resolve(playerCorpseOption: 5);
        _sut.Resolve(playerCorpseOption: 5);

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Resolve_BadCapFixedThenBrokenAgain_WarnsAgain()
    {
        _settings.CorpseCap.Returns(-5);
        _sut.Resolve(playerCorpseOption: 5);
        _settings.CorpseCap.Returns(25);
        _sut.Resolve(playerCorpseOption: 5);
        _settings.CorpseCap.Returns(-5);
        _sut.Resolve(playerCorpseOption: 5);

        _logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Limits_EqualityComparesEveryValue()
    {
        // The mission behavior re-applies only when the limits change, so equality is what drives it.
        var a = new BattleCorpseLimits(true, 60f, 25);

        Assert.AreEqual(a, new BattleCorpseLimits(true, 60f, 25));
        Assert.AreNotEqual(a, new BattleCorpseLimits(true, 61f, 25));
        Assert.AreNotEqual(a, new BattleCorpseLimits(true, 60f, 24));
        Assert.AreNotEqual(a, new BattleCorpseLimits(false, 60f, 25));
    }

    [TestMethod]
    public void Resolve_ZeroCap_IsAllowed()
    {
        _settings.CorpseCap.Returns(0);

        Assert.AreEqual(0, _sut.Resolve(playerCorpseOption: 5).CorpseCap);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // -------- The log line --------

    [TestMethod]
    public void DescribeLine_NamesEveryValue()
    {
        var line = BattleCorpsePolicy.DescribeLine(ragdollOption: 5, corpseOption: 5, battleSizeOption: 6,
            new BattleCorpseLimits(true, 60f, 25));

        Assert.AreEqual(
            "[BattleSettings] ragdollOption=5 corpseOption=5 battleSizeOption=6 taomOverride=on fadeSeconds=60 corpseCap=25",
            line);
    }

    [TestMethod]
    public void DescribeLine_Disabled_SaysOff()
    {
        var line = BattleCorpsePolicy.DescribeLine(3, 1, 2, BattleCorpseLimits.EngineDefaults);

        StringAssert.EndsWith(line, "taomOverride=off");
    }
}
