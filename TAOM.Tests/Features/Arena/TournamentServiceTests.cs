using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.Arena;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Phase 9b #137 — TournamentService extracted from TaomTournamentModel. Pure decision functions
/// (CalculateStartChance, CalculateEndChance, ResolveDummyId) are unit-testable here without
/// Campaign.Current. BuildPrizePool requires Items.All (sealed engine cache) — tested via the
/// boundary in-game; not unit-testable.
/// ShouldDismountInTournament (dwarf tournament-cavalry fix, Patch46) is pure over IRaceManager.
/// </summary>
[TestClass]
public class TournamentServiceTests
{
    // Arbitrary FaceGen race ids — the IRaceManager mock defines their meaning.
    private const int DwarfRaceId = 1;
    private const int HumanRaceId = 0;
    private const int ElfRaceId = 2;
    private const int OrcRaceId = 3;
    private const int InvalidRaceId = 999;

    private IRaceManager _raceManager;
    private TournamentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _raceManager = Substitute.For<IRaceManager>();
        // Default: every id used here is valid unless a test overrides it.
        _raceManager.IsValidRaceId(Arg.Any<int>()).Returns(true);
        _raceManager.GetRaceNameFromId(DwarfRaceId).Returns("dwarf");
        _raceManager.GetRaceNameFromId(HumanRaceId).Returns("human");
        _raceManager.GetRaceNameFromId(ElfRaceId).Returns("elf");
        _raceManager.GetRaceNameFromId(OrcRaceId).Returns("orc");

        _sut = new TournamentService(_raceManager);
    }

    // --- CalculateStartChance ---

    [TestMethod]
    public void CalculateStartChance_ZeroLords_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.CalculateStartChance(0));
    }

    [TestMethod]
    public void CalculateStartChance_NegativeLordCount_ReturnsZero()
    {
        // Defensive — boundary computes count via LINQ which could conceivably underflow.
        Assert.AreEqual(0f, _sut.CalculateStartChance(-1));
    }

    [TestMethod]
    public void CalculateStartChance_OneLord_Returns045()
    {
        Assert.AreEqual(0.45f, _sut.CalculateStartChance(1), 0.001f);
    }

    [TestMethod]
    public void CalculateStartChance_TwoLords_Returns075()
    {
        Assert.AreEqual(0.75f, _sut.CalculateStartChance(2), 0.001f);
    }

    [TestMethod]
    public void CalculateStartChance_ThreeLords_Returns090()
    {
        Assert.AreEqual(0.90f, _sut.CalculateStartChance(3), 0.001f);
    }

    [TestMethod]
    public void CalculateStartChance_FourOrMoreLords_Returns100()
    {
        Assert.AreEqual(1.00f, _sut.CalculateStartChance(4), 0.001f);
        Assert.AreEqual(1.00f, _sut.CalculateStartChance(10), 0.001f);
    }

    // --- CalculateEndChance ---

    [TestMethod]
    public void CalculateEndChance_BeforeGracePeriod_ReturnsZero()
    {
        // Grace period is 20 days; before that, end chance is clamped to 0.
        Assert.AreEqual(0f, _sut.CalculateEndChance(0f));
        Assert.AreEqual(0f, _sut.CalculateEndChance(10f));
        Assert.AreEqual(0f, _sut.CalculateEndChance(19.99f));
    }

    [TestMethod]
    public void CalculateEndChance_AtGracePeriod_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.CalculateEndChance(20f), 0.001f);
    }

    [TestMethod]
    public void CalculateEndChance_OneDayAfterGrace_ReturnsRampValue()
    {
        // ramp = 0.033f, so 1 day post-grace = 0.033
        Assert.AreEqual(0.033f, _sut.CalculateEndChance(21f), 0.001f);
    }

    [TestMethod]
    public void CalculateEndChance_TenDaysAfterGrace_Returns033()
    {
        Assert.AreEqual(0.33f, _sut.CalculateEndChance(30f), 0.001f);
    }

    // --- ResolveDummyId ---

    [TestMethod]
    public void ResolveDummyId_ParticipantCultureGiven_ReturnsCultureSpecificDummy()
    {
        Assert.AreEqual("gear_practice_dummy_gondor", _sut.ResolveDummyId("gondor", null));
    }

    [TestMethod]
    public void ResolveDummyId_NoParticipantCulture_FallsBackToSettlementCulture()
    {
        Assert.AreEqual("gear_practice_dummy_rohan", _sut.ResolveDummyId(null, "rohan"));
    }

    [TestMethod]
    public void ResolveDummyId_NoCultures_FallsBackToEmpireDefault()
    {
        Assert.AreEqual("gear_practice_dummy_empire", _sut.ResolveDummyId(null, null));
    }

    [TestMethod]
    public void ResolveDummyId_EmptyParticipantCulture_FallsBackToSettlement()
    {
        Assert.AreEqual("gear_practice_dummy_mordor", _sut.ResolveDummyId("", "mordor"));
    }

    [TestMethod]
    public void ResolveDummyId_BothEmpty_FallsBackToEmpire()
    {
        Assert.AreEqual("gear_practice_dummy_empire", _sut.ResolveDummyId("", ""));
    }

    // --- ShouldDismountInTournament (Patch46 — dwarf tournament-cavalry fix) ---

    [TestMethod]
    public void ShouldDismountInTournament_DwarfRace_ReturnsTrue()
    {
        Assert.IsTrue(_sut.ShouldDismountInTournament(DwarfRaceId));
    }

    [TestMethod]
    public void ShouldDismountInTournament_DwarfRaceMixedCase_ReturnsTrue()
    {
        // Defensive — name comparison is case-insensitive.
        _raceManager.GetRaceNameFromId(DwarfRaceId).Returns("Dwarf");
        Assert.IsTrue(_sut.ShouldDismountInTournament(DwarfRaceId));
    }

    [TestMethod]
    public void ShouldDismountInTournament_HumanRace_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldDismountInTournament(HumanRaceId));
    }

    [TestMethod]
    public void ShouldDismountInTournament_ElfRace_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldDismountInTournament(ElfRaceId));
    }

    [TestMethod]
    public void ShouldDismountInTournament_OrcRace_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldDismountInTournament(OrcRaceId));
    }

    [TestMethod]
    public void ShouldDismountInTournament_InvalidRaceId_ReturnsFalseAndDoesNotTrustLookup()
    {
        // Validate-before-lookup: GetRaceNameFromId returns "human" for unknown ids, which must
        // never be reached for an invalid id (csharp-architecture.md "Validate Before Lookup").
        _raceManager.IsValidRaceId(InvalidRaceId).Returns(false);

        Assert.IsFalse(_sut.ShouldDismountInTournament(InvalidRaceId));
        _raceManager.DidNotReceive().GetRaceNameFromId(InvalidRaceId);
    }
}
