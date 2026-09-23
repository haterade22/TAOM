using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.NazgulFamily;
using TAOM.Features.UncapturableHeroes;
using TAOM.Features.UncapturableHeroes.Domain;

namespace TAOM.Tests.Features.UncapturableHeroes;

/// <summary>
/// Identity tests for the capture-immunity qualifier, one per resolution row.
///
/// The load-bearing pair is <see cref="IsUncapturable_WraithWithHumanRace_IsProtected"/> and
/// <see cref="IsUncapturable_WraithWithUrukRace_IsProtected"/>. When the feature shipped
/// (2026-08-26) six of the Nine (<c>lord_1_15</c>, <c>lord_1_155</c>, <c>lord_1_16</c>,
/// <c>lord_1_28</c>, <c>lord_1_38</c>, <c>lord_1_48</c>) carried no <c>race</c> attribute, so they
/// were vanilla race 0 (human), and the other three (<c>lord_1_48_1/_2/_3</c>) were
/// <c>race="uruk"</c>. A race-keyed list would have freed six of the Nine, and adding <c>uruk</c>
/// would have protected every uruk lord in the game. Since #644 all nine are <c>race="nazghul"</c>,
/// but the shipped race rule names only <c>sauron</c>, so the hero-set axis is still the only one
/// that covers all nine, and these tests keep it working whatever race an agent reports.
/// </summary>
[TestClass]
public class UncapturableRegistryTests
{
    private const string Sauron = "lord_1_17";
    private const string WitchKing = "lord_1_15";
    private const string NazgulTainted = "lord_1_48_1";
    private const string OrdinaryLord = "lord_2_7";

    private static readonly Dictionary<int, string> RaceTable = new Dictionary<int, string>
    {
        [0] = "human",
        [1] = "elf",
        [2] = "orc",
        [3] = "uruk",
        [4] = "sauron",
    };

    private const int HumanRace = 0;
    private const int UrukRace = 3;
    private const int SauronRace = 4;

    private IUncapturableHeroesConfigProvider _configProvider = null!;
    private IRaceManager _raceManager = null!;
    private INazgulRegistry _nazgul = null!;
    private IModLogger _logger = null!;
    private UncapturableHeroesConfig _config = null!;
    private UncapturableRegistry _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new UncapturableHeroesConfig();

        _configProvider = Substitute.For<IUncapturableHeroesConfigProvider>();
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
        _nazgul.IsWraith(Arg.Any<string>()).Returns(c =>
            c.Arg<string>() == WitchKing || c.Arg<string>() == NazgulTainted);

        _logger = Substitute.For<IModLogger>();

        _sut = new UncapturableRegistry(_configProvider, _raceManager, _nazgul, _logger);
    }

    // ---- The include axes -------------------------------------------------

    [TestMethod]
    public void IsUncapturable_SauronByHeroId_IsProtected()
        => Assert.IsTrue(_sut.IsUncapturable(Sauron, HumanRace));

    [TestMethod]
    public void IsUncapturable_SauronRaceAloneWithoutTheId_IsProtected()
    {
        // The rule axis on its own. Proves the shipped config survives a data change that drops
        // lord_1_17 from heroIds, which is exactly why he is listed on both axes.
        _config.HeroIds = new List<string>();

        Assert.IsTrue(_sut.IsUncapturable("some_other_id", SauronRace));
    }

    [TestMethod]
    public void IsUncapturable_WraithWithHumanRace_IsProtected()
    {
        // Before #644 six of the Nine were race 0. A race-only qualifier freed all six.
        Assert.IsTrue(_sut.IsUncapturable(WitchKing, HumanRace));
    }

    [TestMethod]
    public void IsUncapturable_WraithWithUrukRace_IsProtected()
    {
        // Before #644 the other three were race="uruk". Caught by the hero set, NOT by the race rule.
        Assert.IsTrue(_sut.IsUncapturable(NazgulTainted, UrukRace));
    }

    [TestMethod]
    public void IsUncapturable_OrdinaryUrukLord_IsNotProtected()
    {
        // The other half of the claim above: protecting the three uruk Nazgul must not protect
        // every uruk lord in the game.
        Assert.IsFalse(_sut.IsUncapturable(OrdinaryLord, UrukRace));
    }

    [TestMethod]
    public void IsUncapturable_OrdinaryHumanLord_IsNotProtected()
        => Assert.IsFalse(_sut.IsUncapturable(OrdinaryLord, HumanRace));

    // ---- The exclude axis beats everything --------------------------------

    [TestMethod]
    public void IsUncapturable_ExcludeList_BeatsTheHeroIdInclude()
    {
        _config.ExcludeHeroIds = new List<string> { Sauron };

        Assert.IsFalse(_sut.IsUncapturable(Sauron, HumanRace));
    }

    [TestMethod]
    public void IsUncapturable_ExcludeList_BeatsTheNazgulHeroSet()
    {
        _config.ExcludeHeroIds = new List<string> { WitchKing };

        Assert.IsFalse(_sut.IsUncapturable(WitchKing, HumanRace));
    }

    [TestMethod]
    public void IsUncapturable_ExcludeList_BeatsTheRaceRule()
    {
        // The one that would break if exclude were checked after the race axis instead of first.
        _config.HeroIds = new List<string>();
        _config.ExcludeHeroIds = new List<string> { Sauron };

        Assert.IsFalse(_sut.IsUncapturable(Sauron, SauronRace));
    }

    [TestMethod]
    public void IsUncapturable_ExcludeList_IsCaseInsensitive()
    {
        _config.ExcludeHeroIds = new List<string> { "LORD_1_17" };

        Assert.IsFalse(_sut.IsUncapturable(Sauron, SauronRace));
    }

    // ---- Parsed-but-unresolvable entries ----------------------------------

    [TestMethod]
    public void IsUncapturable_UnknownHeroSet_IsSkippedAndWarned()
    {
        _config.HeroSets = new List<string> { "balrogs_of_morgoth" };

        Assert.IsFalse(_sut.IsUncapturable(WitchKing, HumanRace));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("balrogs_of_morgoth")));
    }

    [TestMethod]
    public void IsUncapturable_UnknownRaceName_IsSkippedAndWarned()
    {
        _config.UncapturableRaces = new List<string> { "balrog" };
        _config.HeroIds = new List<string>();

        Assert.IsFalse(_sut.IsUncapturable(Sauron, SauronRace));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("balrog")));
    }

    [TestMethod]
    public void IsUncapturable_UnknownRaceId_DoesNotCoerceToAProtectedRow()
    {
        // The validate-before-lookup trap, inverted. GetRaceNameFromId would answer "human" for
        // race 99; because the table is keyed by ID and built once, an unrecognised id simply
        // misses every row instead of landing on one.
        _config.HeroIds = new List<string>();

        Assert.IsFalse(_sut.IsUncapturable("unknown_hero", 99));
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    // ---- Degenerate inputs ------------------------------------------------

    [TestMethod]
    public void IsUncapturable_NullRaceId_StillMatchesOnHeroId()
        => Assert.IsTrue(_sut.IsUncapturable(Sauron, null));

    [TestMethod]
    public void IsUncapturable_NullRaceId_AndUnlistedHero_IsNotProtected()
        => Assert.IsFalse(_sut.IsUncapturable(OrdinaryLord, null));

    [TestMethod]
    public void IsUncapturable_NullHeroId_FallsThroughToTheRaceRule()
        => Assert.IsTrue(_sut.IsUncapturable(null!, SauronRace));

    [TestMethod]
    public void IsUncapturable_EmptyLists_ProtectNobody()
    {
        _config.HeroIds = new List<string>();
        _config.HeroSets = new List<string>();
        _config.UncapturableRaces = new List<string>();

        Assert.IsFalse(_sut.IsUncapturable(Sauron, SauronRace));
        Assert.IsFalse(_sut.IsUncapturable(WitchKing, HumanRace));
    }

    [TestMethod]
    public void IsUncapturable_HeroSetsWithoutNazgul_NeverConsultsTheWraithRegistry()
    {
        _config.HeroSets = new List<string>();

        _sut.IsUncapturable(WitchKing, HumanRace);

        _nazgul.DidNotReceive().IsWraith(Arg.Any<string>());
    }

    // ---- Table lifetime ---------------------------------------------------

    [TestMethod]
    public void IsUncapturable_BuildsTheTableOnce_AcrossManyCalls()
    {
        _sut.IsUncapturable(Sauron, SauronRace);
        _sut.IsUncapturable(WitchKing, HumanRace);
        _sut.IsUncapturable(OrdinaryLord, UrukRace);

        _configProvider.Received(1).GetConfig();
    }
}
