using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class BasicTableauRaceGuardTests
{
    // Race ids are skins.xml merge-order indices — arbitrary values here; the guard must key on
    // the NAME the race manager resolves, never on the int itself.
    private const int UrukRaceId = 5;
    private const int DwarfRaceId = 1;
    private const int ElfRaceId = 2;

    private IRaceManager _raceManager;
    private BasicTableauRaceGuard _sut;

    [TestInitialize]
    public void Setup()
    {
        _raceManager = Substitute.For<IRaceManager>();
        _sut = new BasicTableauRaceGuard(_raceManager);
    }

    [TestMethod]
    public void ResolveSafeRace_HumanBaseRace_ReturnsHumanWithoutConsultingRaceManager()
    {
        var result = _sut.ResolveSafeRace(BasicTableauRaceGuard.HumanBaseRace);

        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, result);
        // Human must stay renderable even if race resolution is unavailable.
        _raceManager.DidNotReceiveWithAnyArgs().IsValidRaceId(default);
        _raceManager.DidNotReceiveWithAnyArgs().GetRaceNameFromId(default);
    }

    [TestMethod]
    public void ResolveSafeRace_VerifiedSafeRaceUruk_ReturnsRaceUnchanged()
    {
        _raceManager.IsValidRaceId(UrukRaceId).Returns(true);
        _raceManager.GetRaceNameFromId(UrukRaceId).Returns("uruk");

        Assert.AreEqual(UrukRaceId, _sut.ResolveSafeRace(UrukRaceId));
    }

    [TestMethod]
    public void ResolveSafeRace_SafeRaceNameDifferentCasing_ReturnsRaceUnchanged()
    {
        _raceManager.IsValidRaceId(UrukRaceId).Returns(true);
        _raceManager.GetRaceNameFromId(UrukRaceId).Returns("Uruk");

        Assert.AreEqual(UrukRaceId, _sut.ResolveSafeRace(UrukRaceId));
    }

    [TestMethod]
    public void ResolveSafeRace_DwarfRace_CoercesToHuman()
    {
        // Dwarf heads are verified UNSAFE in the agentless static-morph build (issue #295:
        // head.eye = 0 morph targets -> native AV). Must coerce even though the id is valid.
        _raceManager.IsValidRaceId(DwarfRaceId).Returns(true);
        _raceManager.GetRaceNameFromId(DwarfRaceId).Returns("dwarf");

        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(DwarfRaceId));
    }

    [TestMethod]
    public void ResolveSafeRace_UnverifiedRace_CoercesToHuman()
    {
        _raceManager.IsValidRaceId(ElfRaceId).Returns(true);
        _raceManager.GetRaceNameFromId(ElfRaceId).Returns("elf");

        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(ElfRaceId));
    }

    [TestMethod]
    public void ResolveSafeRace_InvalidRaceId_CoercesToHumanWithoutNameLookup()
    {
        _raceManager.IsValidRaceId(-1).Returns(false);

        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(-1));
        // Validate-before-lookup: the fallback-returning name lookup must not be consulted
        // for an invalid id (csharp-architecture.md rule; Codex review #33 class).
        _raceManager.DidNotReceiveWithAnyArgs().GetRaceNameFromId(default);
    }

    [TestMethod]
    public void ResolveSafeRace_InvalidIdWhoseFallbackNameLooksSafe_StillCoerces()
    {
        // The fallback trap: GetRaceNameFromId returns a plausible name for unknown ids. Even if
        // that fallback were ever a safe-listed name, an INVALID id must coerce.
        _raceManager.IsValidRaceId(99).Returns(false);
        _raceManager.GetRaceNameFromId(99).Returns("uruk");

        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(99));
    }

    [TestMethod]
    public void ResolveSafeRace_UnknownLargeRace_CoercesToHuman()
    {
        _raceManager.IsValidRaceId(int.MaxValue).Returns(false);

        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(int.MaxValue));
    }

    [TestMethod]
    public void ResolveSafeRace_RaceManagerThrows_CoercesToHuman()
    {
        // A throw inside the guard would propagate into the cold-menu render tick — the exact
        // crash class this guard exists to prevent. It must fail safe to the human base.
        _raceManager.IsValidRaceId(Arg.Any<int>()).Returns(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(UrukRaceId));
    }
}
