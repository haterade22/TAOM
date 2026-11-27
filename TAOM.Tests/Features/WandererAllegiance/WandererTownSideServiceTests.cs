using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.TroopProgression;
using TAOM.Features.WandererAllegiance;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>
/// The placement rule, pure: which town a new or swapped wanderer may stand in. The engine half
/// (reading towns, the move call) is the adapter's and is checked in game.
/// </summary>
[TestClass]
public class WandererTownSideServiceTests
{
    private static readonly SideIds FreeTown = new("kingdom_free", "gondor");
    private static readonly SideIds EvilTown = new("kingdom_evil", "mordor");
    private static readonly SideIds NeutralTown = new("kingdom_neutral", "khand");

    private IAlignmentService _alignment = null!;
    private IWandererAllegianceSettingsProvider _settings = null!;
    private IRandomProvider _random = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private WandererTownSideService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _alignment = Substitute.For<IAlignmentService>();
        _settings = Substitute.For<IWandererAllegianceSettingsProvider>();
        _random = Substitute.For<IRandomProvider>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _dedicated = Substitute.For<IDedicatedServerProvider>();

        _settings.PlacesWanderersBySide.Returns(true);
        _coop.IsSessionActive.Returns(false);
        _coop.ShouldDeferToHost.Returns(false);
        _coop.IsAuthority.Returns(true);
        _dedicated.IsDedicatedServer.Returns(false);

        // NSubstitute answers default(FactionSide) == Free for anything unset, so every id is set.
        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);
        _alignment.GetCultureSide("orc").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("khand").Returns(FactionSide.Neutral);
        _alignment.ResolveSide("kingdom_free", "gondor").Returns(FactionSide.Free);
        _alignment.ResolveSide("kingdom_evil", "mordor").Returns(FactionSide.Evil);
        _alignment.ResolveSide("kingdom_neutral", "khand").Returns(FactionSide.Neutral);
        _alignment.ResolveSide("", "gondor").Returns(FactionSide.Free);
        _alignment.ResolveSide("", "mordor").Returns(FactionSide.Evil);

        _random.Next(Arg.Any<int>()).Returns(0);
        _sut = new WandererTownSideService(_alignment, _settings, _random, _coop, _dedicated);
    }

    private static WandererTownOption Option(SideIds side, bool usable = true, bool hasWanderer = false) =>
        new(side, usable, hasWanderer);

    // ---- Fits: the nine combinations ----------------------------------------------------

    [DataTestMethod]
    [DataRow("gondor", "free", true)]
    [DataRow("gondor", "evil", false)]
    [DataRow("gondor", "neutral", true)]
    [DataRow("orc", "free", false)]
    [DataRow("orc", "evil", true)]
    [DataRow("orc", "neutral", true)]
    [DataRow("khand", "free", true)]
    [DataRow("khand", "evil", true)]
    [DataRow("khand", "neutral", true)]
    public void Fits_IsFalseOnlyForFreeAgainstEvil(string culture, string town, bool expected)
    {
        var side = town == "free" ? FreeTown : town == "evil" ? EvilTown : NeutralTown;
        Assert.AreEqual(expected, _sut.Fits(culture, side));
    }

    [TestMethod]
    public void Fits_WithoutACulture_FailsOpen()
    {
        Assert.IsTrue(_sut.Fits(null, EvilTown));
        Assert.IsTrue(_sut.Fits("", FreeTown));
    }

    [TestMethod]
    public void Fits_ReadsTheTownThroughResolveSide_NotTheCultureTable()
    {
        _sut.Fits("orc", FreeTown);
        _alignment.Received().ResolveSide("kingdom_free", "gondor");
        _alignment.DidNotReceive().GetCultureSide("gondor");
    }

    [TestMethod]
    public void Fits_NeverAsksTheEnemyPredicate_WhoseNeutralReadingIsInverted()
    {
        _sut.Fits("orc", NeutralTown);
        _alignment.DidNotReceiveWithAnyArgs().AreEnemyAlignments(default(FactionSide), default(FactionSide));
    }

    [TestMethod]
    public void Fits_ForAnOwnerWithNoKingdom_ReadsTheCultureFallback()
    {
        // FactionId empty: the owner's own culture decides, through ResolveSide's fallback.
        Assert.IsTrue(_sut.Fits("gondor", new SideIds("", "gondor")));
        Assert.IsFalse(_sut.Fits("gondor", new SideIds("", "mordor")));
    }

    // ---- Choose ------------------------------------------------------------------------

    [TestMethod]
    public void Choose_KeepsVanillasTown_WhenTheWandererFitsThere()
    {
        var options = new[] { Option(FreeTown), Option(EvilTown) };
        Assert.AreEqual(-1, _sut.Choose("gondor", FreeTown, options));
        Assert.AreEqual(-1, _sut.Choose("orc", NeutralTown, options));
    }

    [TestMethod]
    public void Choose_MovesAnOrcOutOfAFreeTown_ToAnEvilOrNeutralOne()
    {
        var options = new[] { Option(FreeTown), Option(EvilTown), Option(NeutralTown) };
        var picked = _sut.Choose("orc", FreeTown, options);
        Assert.IsTrue(picked == 1 || picked == 2, "picked " + picked);
    }

    [TestMethod]
    public void Choose_MovesAFreeWandererOutOfAnEvilTown()
    {
        var options = new[] { Option(EvilTown), Option(FreeTown) };
        Assert.AreEqual(1, _sut.Choose("gondor", EvilTown, options));
    }

    [TestMethod]
    public void Choose_KeepsVanillasPick_WhenNoTownOfTheSideExists()
    {
        var options = new[] { Option(FreeTown), Option(FreeTown) };
        Assert.AreEqual(-1, _sut.Choose("orc", FreeTown, options));
    }

    [TestMethod]
    public void Choose_KeepsVanillasPick_WhenTheOnlyFittingTownsAreUnusable()
    {
        var options = new[] { Option(FreeTown), Option(EvilTown, usable: false) };
        Assert.AreEqual(-1, _sut.Choose("orc", FreeTown, options));
    }

    [TestMethod]
    public void Choose_KeepsVanillasPick_ForAnEmptyList()
    {
        Assert.AreEqual(-1, _sut.Choose("orc", FreeTown, new List<WandererTownOption>()));
    }

    [TestMethod]
    public void Choose_PrefersATownWithNoWandererYet()
    {
        var options = new[] { Option(EvilTown, hasWanderer: true), Option(EvilTown), Option(EvilTown, hasWanderer: true) };
        Assert.AreEqual(1, _sut.Choose("orc", FreeTown, options));
    }

    [TestMethod]
    public void Choose_FallsBackToAnOccupiedTown_WhenNoFittingTownIsEmpty()
    {
        var options = new[] { Option(FreeTown), Option(EvilTown, hasWanderer: true) };
        Assert.AreEqual(1, _sut.Choose("orc", FreeTown, options));
    }

    [TestMethod]
    public void Choose_DrawsOnlyAmongTheFittingTowns()
    {
        var options = new[] { Option(FreeTown), Option(EvilTown), Option(FreeTown), Option(EvilTown) };
        _random.Next(2).Returns(1);
        Assert.AreEqual(3, _sut.Choose("orc", FreeTown, options));
    }

    [TestMethod]
    public void Choose_MovesAFittingWandererToAnEmptyTown_WhenTheFittingDestinationIsTaken()
    {
        // The new-game fill gives each wanderer its own town from a shuffled list; a redirected one may have taken this slot.
        var options = new[] { Option(FreeTown, hasWanderer: true), Option(EvilTown), Option(NeutralTown), Option(FreeTown) };
        Assert.AreEqual(2, _sut.Choose("gondor", FreeTown, options, destinationHasWanderer: true));
    }

    [TestMethod]
    public void Choose_KeepsATakenFittingDestination_WhenNoEmptyTownOfTheSideExists()
    {
        var options = new[] { Option(FreeTown, hasWanderer: true), Option(EvilTown), Option(FreeTown, hasWanderer: true), Option(FreeTown, usable: false) };
        Assert.AreEqual(-1, _sut.Choose("gondor", FreeTown, options, destinationHasWanderer: true));
    }

    [TestMethod]
    public void Choose_KeepsAFreeFittingDestination_WhenItHoldsNoOtherWanderer()
    {
        var options = new[] { Option(FreeTown), Option(FreeTown) };
        Assert.AreEqual(-1, _sut.Choose("gondor", FreeTown, options, destinationHasWanderer: false));
    }

    // ---- Applies -----------------------------------------------------------------------

    [TestMethod]
    public void Applies_IsTrueInSinglePlayerWithTheSwitchOn() => Assert.IsTrue(_sut.Applies());

    [TestMethod]
    public void Applies_IsFalseWhenTheSwitchIsOff()
    {
        _settings.PlacesWanderersBySide.Returns(false);
        Assert.IsFalse(_sut.Applies());
    }

    [TestMethod]
    public void Applies_IsFalseInACoopSession()
    {
        _coop.IsSessionActive.Returns(true);
        Assert.IsFalse(_sut.Applies());
    }

    [TestMethod]
    public void Applies_IsFalseWhenTheHostDecides()
    {
        _coop.ShouldDeferToHost.Returns(true);
        Assert.IsFalse(_sut.Applies());
    }

    [TestMethod]
    public void Applies_IsFalseOnADedicatedServer()
    {
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.IsFalse(_sut.Applies());
    }
}
