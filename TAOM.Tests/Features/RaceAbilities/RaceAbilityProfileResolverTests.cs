using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

// Race and culture to ability profile, and who counts as kin. The resolver turns race names into ids and
// keys its maps by id, so it never calls GetRaceNameFromId, whose "human" answer for an unknown id
// (csharp-architecture.md "Lookup Functions With Fallbacks") would otherwise make a junk id a man; an unknown
// id simply misses every map. A culture profile applies to men only, and never over a race's own.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityProfileResolverTests
{
    private const int HumanId = 0;

    private IRaceAbilitiesConfigProvider _configProvider = null!;
    private IRaceManager _raceManager = null!;
    private IModLogger _logger = null!;
    private RaceAbilitiesConfig _config = null!;
    private RaceAbilityProfileResolver _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<IRaceAbilitiesConfigProvider>();
        _raceManager = Substitute.For<IRaceManager>();
        _logger = Substitute.For<IModLogger>();
        _config = new RaceAbilitiesConfig();
        _config.Races.Clear();
        _config.Cultures.Clear();
        _configProvider.GetConfig().Returns(_config);
        RegisterRace("human", HumanId);
        _sut = new RaceAbilityProfileResolver(_configProvider, _raceManager, _logger);
    }

    private void RegisterRace(string name, int id)
    {
        _raceManager.IsValidRaceName(name).Returns(true);
        _raceManager.GetRaceIdFromName(name).Returns(id);
    }

    private RaceAbilityProfile AddRace(string name, int id, params string[] kinRaces)
    {
        var profile = new RaceAbilityProfile { AbilityId = name + "_ability" };
        profile.KinRaces.AddRange(kinRaces);
        _config.Races[name] = profile;
        RegisterRace(name, id);
        return profile;
    }

    private RaceAbilityProfile AddCulture(params string[] cultures)
    {
        var profile = new RaceAbilityProfile { AbilityId = cultures[0] + "_ability" };
        foreach (var culture in cultures)
            _config.Cultures[culture] = profile;
        return profile;
    }

    [TestMethod]
    public void Resolve_KnownRace_ReturnsItsProfile()
    {
        var dwarf = AddRace("dwarf", 3);

        Assert.AreSame(dwarf, _sut.Resolve(3, "erebor"));
    }

    [TestMethod]
    public void Resolve_RaceWithoutAProfile_AndNoCulture_ReturnsNull()
    {
        AddRace("dwarf", 3);

        Assert.IsNull(_sut.Resolve(HumanId, null));
    }

    [TestMethod]
    public void Resolve_NullRace_ReturnsNull()
    {
        AddRace("dwarf", 3);

        Assert.IsNull(_sut.Resolve(null, "gondor"));
    }

    [TestMethod]
    public void Resolve_InvalidRaceId_ReturnsNullWithoutANameLookup()
    {
        AddCulture("gondor");
        _raceManager.GetRaceNameFromId(99).Returns("human");   // the engine's fallback for an unknown id

        Assert.IsNull(_sut.Resolve(99, "gondor"));
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void Resolve_Man_TakesHisCulturesProfile()
    {
        var gondor = AddCulture("gondor", "gondor_soldiers");

        Assert.AreSame(gondor, _sut.Resolve(HumanId, "gondor"));
        Assert.AreSame(gondor, _sut.Resolve(HumanId, "gondor_soldiers"));
    }

    [TestMethod]
    public void Resolve_ManOfACultureWithoutAProfile_ReturnsNull()
    {
        AddCulture("gondor");

        Assert.IsNull(_sut.Resolve(HumanId, "looters"));
    }

    [TestMethod]
    public void Resolve_NonHumanOfAProfiledCulture_GetsNothingFromTheCulture()
    {
        // A troll in Mordor's ranks is not one of Mordor's men.
        AddCulture("mordor");
        RegisterRace("cave_troll", 9);

        Assert.IsNull(_sut.Resolve(9, "mordor"));
    }

    [TestMethod]
    public void Resolve_RaceProfileWinsOverTheCulture()
    {
        // An orc in Mordor's culture is an orc first.
        var orc = AddRace("orc", 4);
        AddCulture("mordor");

        Assert.AreSame(orc, _sut.Resolve(4, "mordor"));
    }

    [TestMethod]
    public void Resolve_NoHumanRaceInTheEngine_NoCultureApplies()
    {
        _raceManager.IsValidRaceName("human").Returns(false);
        AddCulture("gondor");

        Assert.IsNull(_sut.Resolve(HumanId, "gondor"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'human'")));
    }

    [TestMethod]
    public void Resolve_UnknownRaceName_IsSkippedWithAWarning()
    {
        _config.Races["ent"] = new RaceAbilityProfile();
        _raceManager.IsValidRaceName("ent").Returns(false);
        AddRace("dwarf", 3);

        Assert.IsNotNull(_sut.Resolve(3, null));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'ent'")));
    }

    [TestMethod]
    public void Resolve_DisabledConfig_ReturnsNull()
    {
        AddRace("dwarf", 3);
        _config.Enabled = false;

        Assert.IsNull(_sut.Resolve(3, null));
    }

    [TestMethod]
    public void Resolve_BuildsTheMapsOnce()
    {
        AddRace("dwarf", 3);

        _sut.Resolve(3, null);
        _sut.Resolve(3, null);

        _raceManager.Received(1).IsValidRaceName("dwarf");
    }

    [TestMethod]
    public void IsKin_SameProfile_IsKin()
    {
        var orc = AddRace("orc", 4);

        Assert.IsTrue(_sut.IsKin(orc, 4, orc));
    }

    [TestMethod]
    public void IsKin_AKinRace_IsKinWhateverItsOwnProfile()
    {
        var orc = AddRace("orc", 4, "goblin");
        var goblin = AddRace("goblin", 5);

        Assert.IsTrue(_sut.IsKin(orc, 5, goblin));
        Assert.IsFalse(_sut.IsKin(goblin, 4, orc), "kinship is the profile's own list, not symmetric by itself");
    }

    [TestMethod]
    public void IsKin_AnotherRace_IsNotKin()
    {
        var orc = AddRace("orc", 4);
        var dwarf = AddRace("dwarf", 3);

        Assert.IsFalse(_sut.IsKin(orc, 3, dwarf));
    }

    [TestMethod]
    public void KinRaces_UnknownName_IsSkippedWithAWarning()
    {
        var orc = AddRace("orc", 4, "snaga");
        _raceManager.IsValidRaceName("snaga").Returns(false);

        Assert.AreEqual(0, _sut.KinRaces(orc).Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'snaga'")));
    }

    [TestMethod]
    public void TierScaling_ComesFromTheConfig()
    {
        _config.TierScaling.HeroFactor = 1.5f;

        Assert.AreEqual(1.5f, _sut.TierScaling.HeroFactor, 0.0001f);
    }
}
