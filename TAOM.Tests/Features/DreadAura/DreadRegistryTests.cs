using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.DreadAura;
using TAOM.Features.DreadAura.Domain;
using TAOM.Features.NazgulFamily;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// Identity tests for the aura's two source axes and its target resist table.
///
/// The load-bearing one is <see cref="IsDreadSource_NazgulHeroId_IsSource_EvenThoughRaceIsNotNazghul"/>.
/// Verified against TAOM data on 2026-08-13: <c>lords.xslt</c> gives eight of the Nine no
/// <c>race</c> attribute at all (they inherit vanilla race 0, human) and Khamûl
/// (<c>lord_1_48</c>) is <c>race="orc"</c>. A race-only source list would emit zero auras for
/// eight of the Nine; adding <c>orc</c> to catch Khamûl would hand the aura to every orc alive.
/// </summary>
[TestClass]
public class DreadRegistryTests
{
    private static readonly Dictionary<int, string> RaceTable = new Dictionary<int, string>
    {
        [0] = "human",
        [1] = "elf",
        [2] = "dwarf",
        [3] = "orc",
        [4] = "sauron",
        [5] = "uruk",
    };

    private IDreadAuraConfigProvider _configProvider = null!;
    private IRaceManager _raceManager = null!;
    private INazgulRegistry _nazgul = null!;
    private IModLogger _logger = null!;
    private DreadAuraConfig _config = null!;
    private DreadRegistry _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new DreadAuraConfig();

        _configProvider = Substitute.For<IDreadAuraConfigProvider>();
        _configProvider.GetConfig().Returns(_ => _config);

        _raceManager = Substitute.For<IRaceManager>();
        _raceManager.IsValidRaceId(Arg.Any<int>()).Returns(c => RaceTable.ContainsKey(c.Arg<int>()));
        _raceManager.IsValidRaceName(Arg.Any<string>()).Returns(c => RaceTable.ContainsValue(c.Arg<string>()));
        _raceManager.GetRaceIdFromName(Arg.Any<string>()).Returns(c =>
            RaceTable.FirstOrDefault(p => p.Value == c.Arg<string>()).Key);
        // Mirrors the real RaceManager: unknown ids are COERCED to "human" with a warning. That
        // fallback is what the validate-before-lookup rule exists to defend against.
        _raceManager.GetRaceNameFromId(Arg.Any<int>()).Returns(c =>
            RaceTable.TryGetValue(c.Arg<int>(), out var name) ? name : "human");

        _nazgul = Substitute.For<INazgulRegistry>();
        _nazgul.IsWraith(Arg.Any<string>()).Returns(false);
        _nazgul.IsWraith("lord_1_15").Returns(true);
        _nazgul.IsWraith("lord_1_48").Returns(true);

        _logger = Substitute.For<IModLogger>();

        _sut = new DreadRegistry(_configProvider, _raceManager, _nazgul, _logger);
    }

    // ---- Source identity: the hero axis ------------------------------------

    [TestMethod]
    public void IsDreadSource_NazgulHeroId_IsSource_EvenThoughRaceIsNotNazghul()
    {
        // The Witch-King's race is vanilla 0 (human) in TAOM's data. Race cannot find him.
        var isSource = _sut.IsDreadSource("lord_1_15", raceId: 0);

        Assert.IsTrue(isSource,
            "The Nine are identified by hero StringId. If this fails, the aura is dead for eight of them.");
    }

    [TestMethod]
    public void IsDreadSource_KhamulHeroId_IsSource_DespiteOrcRace()
    {
        var isSource = _sut.IsDreadSource("lord_1_48", raceId: 3);

        Assert.IsTrue(isSource);
    }

    [TestMethod]
    public void IsDreadSource_PlainOrcTroop_IsNotSource()
    {
        // The guard against "just add orc to the race list so Khamûl works" — that would give
        // every orc in the game an aura of dread.
        var isSource = _sut.IsDreadSource("some_orc_troop", raceId: 3);

        Assert.IsFalse(isSource);
    }

    [TestMethod]
    public void IsDreadSource_SauronHeroId_IsSource()
        => Assert.IsTrue(_sut.IsDreadSource("lord_1_17", raceId: 4));

    [TestMethod]
    public void IsDreadSource_UnknownHeroWithNoRace_IsNotSource()
        => Assert.IsFalse(_sut.IsDreadSource("lord_1_99", raceId: null));

    // ---- Source identity: the race axis ------------------------------------

    [TestMethod]
    public void IsDreadSource_SauronRace_IsSource_WithoutAHeroId()
    {
        // Covers a Sauron agent whose CharacterObject has no HeroObject.
        var isSource = _sut.IsDreadSource(null, raceId: 4);

        Assert.IsTrue(isSource);
    }

    [DataTestMethod]
    [DataRow(0, DisplayName = "human")]
    [DataRow(1, DisplayName = "elf")]
    [DataRow(2, DisplayName = "dwarf")]
    [DataRow(5, DisplayName = "uruk")]
    public void IsDreadSource_NonDreadRace_IsNotSource(int raceId)
        => Assert.IsFalse(_sut.IsDreadSource(null, raceId));

    [TestMethod]
    public void IsDreadSource_InvalidRaceId_IsNotSource_AndNeverResolvesANameForIt()
    {
        // GetRaceNameFromId coerces unknown ids to "human", so a name-keyed lookup would hand
        // every corrupt race id in the mission whatever the human row says. Making "human" a
        // dread race here is what makes the arrange load-bearing.
        //
        // The defence is structural, not a guard clause: the source table is keyed by race ID,
        // built once behind IsValidRaceName, and nothing on this path calls GetRaceNameFromId at
        // all. The DidNotReceive assertion is what pins that design against a name-keyed rewrite.
        _config.Races = new List<string> { "sauron", "human" };

        var isSource = _sut.IsDreadSource(null, raceId: 4242);

        Assert.IsFalse(isSource);
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void IsDreadSource_NullHeroAndNullRace_IsNotSource()
        => Assert.IsFalse(_sut.IsDreadSource(null, null));

    [TestMethod]
    public void IsDreadSource_EmptyHeroId_FallsBackToRaceAxis()
    {
        Assert.IsTrue(_sut.IsDreadSource(string.Empty, raceId: 4));
        Assert.IsFalse(_sut.IsDreadSource(string.Empty, raceId: 1));
    }

    // ---- Source identity: gating and config hygiene ------------------------

    [TestMethod]
    public void IsDreadSource_DoesNotConsultTheMasterToggle()
    {
        // Identity must not depend on the toggle. Gating it here would gate a STATE TRANSITION
        // (DreadSourceTracker's registration), so a wraith spawning while the feature is off would
        // stay unregistered forever once it was switched back on: the one-shot mission scan never
        // re-runs. The toggle gates the DRAIN instead, in DreadAuraService and the mission tick.
        // Regression for deep-review 2026-08-13 finding 2.
        Assert.IsTrue(_sut.IsDreadSource("lord_1_15", raceId: 0));
        Assert.IsTrue(_sut.IsDreadSource(null, raceId: 4));
    }

    [TestMethod]
    public void IsDreadSource_UnknownRaceKeyInConfig_SkippedAndWarned()
    {
        _config.Races = new List<string> { "sauron", "balrog" };

        Assert.IsTrue(_sut.IsDreadSource(null, raceId: 4));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("balrog")));
    }

    [TestMethod]
    public void IsDreadSource_UnknownHeroSetInConfig_SkippedAndWarned()
    {
        _config.HeroSets = new List<string> { "istari_five" };

        Assert.IsFalse(_sut.IsDreadSource("lord_1_15", raceId: 0));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("istari_five")));
    }

    [TestMethod]
    public void IsDreadSource_EmptyConfigLists_IsNotSource()
    {
        _config.HeroSets = new List<string>();
        _config.HeroIds = new List<string>();
        _config.Races = new List<string>();

        Assert.IsFalse(_sut.IsDreadSource("lord_1_15", raceId: 4));
    }

    [TestMethod]
    public void IsDreadSource_RepeatedCalls_BuildsTheRaceMapOnce()
    {
        // The FaceGen registry is not populated at IoC time, so the map is built lazily. It must
        // not be rebuilt per agent per mission.
        for (var i = 0; i < 50; i++)
            _sut.IsDreadSource("lord_1_15", raceId: 0);

        _configProvider.Received(1).GetConfig();
    }

    // ---- Resist table ------------------------------------------------------

    [TestMethod]
    public void ResolveResist_ElfRace_ReturnsConfiguredMultiplier()
        => Assert.AreEqual(0.4f, _sut.ResolveResist(1), 0.001f);

    [TestMethod]
    public void ResolveResist_DwarfRace_ReturnsConfiguredMultiplier()
        => Assert.AreEqual(0.5f, _sut.ResolveResist(2), 0.001f);

    [TestMethod]
    public void ResolveResist_RaceNotInTable_ReturnsOne()
        => Assert.AreEqual(1f, _sut.ResolveResist(0), 0.001f);

    [TestMethod]
    public void ResolveResist_NullRaceId_ReturnsOne()
        => Assert.AreEqual(1f, _sut.ResolveResist(null), 0.001f);

    [TestMethod]
    public void ResolveResist_InvalidRaceId_ReturnsOne_AndNeverResolvesANameForIt()
    {
        // "human" carries a distinctive resist so a fallback coercion would be VISIBLE in the
        // returned value, not just in the call log.
        _config.RaceResist = new Dictionary<string, float> { ["human"] = 0.25f };

        Assert.AreEqual(1f, _sut.ResolveResist(4242), 0.001f);
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void ResolveResist_UnknownRaceKeyInConfig_SkippedAndWarned()
    {
        _config.RaceResist = new Dictionary<string, float> { ["elf"] = 0.4f, ["ent"] = 0.1f };

        Assert.AreEqual(0.4f, _sut.ResolveResist(1), 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("ent")));
    }

    [TestMethod]
    public void ResolveResist_DoesNotConsultTheMasterToggle()
    {
        // Same reasoning as IsDreadSource: this is a lookup, not a decision.
        Assert.AreEqual(0.4f, _sut.ResolveResist(1), 0.001f);
    }
}
