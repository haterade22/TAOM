using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class RaceCombatModifiersResolverTests
{
    private ICombatMechanicsConfigProvider _configProvider;
    private ICombatMechanicsSettingsProvider _settings;
    private IRaceManager _raceManager;
    private IModLogger _logger;
    private CombatMechanicsConfig _config;
    private RaceCombatModifiersResolver _sut;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _raceManager = Substitute.For<IRaceManager>();
        _logger = Substitute.For<IModLogger>();

        _config = new CombatMechanicsConfig();
        _config.RaceModifiers.Clear();
        _configProvider.GetConfig().Returns(_config);
        _settings.RaceCombatModifiersEnabled.Returns(true);

        _sut = new RaceCombatModifiersResolver(_configProvider, _settings, _raceManager, _logger);
    }

    private void AddRace(string name, int id, RaceCombatModifiers modifiers)
    {
        _config.RaceModifiers[name] = modifiers;
        _raceManager.IsValidRaceName(name).Returns(true);
        _raceManager.GetRaceIdFromName(name).Returns(id);
        _raceManager.IsValidRaceId(id).Returns(true);
    }

    [TestMethod]
    public void Resolve_Disabled_ReturnsNeutral()
    {
        AddRace("dwarf", 3, new RaceCombatModifiers { KnockdownResistanceMultiplier = 2.5f });
        _settings.RaceCombatModifiersEnabled.Returns(false);

        var result = _sut.Resolve(3);

        Assert.AreSame(RaceCombatModifiers.Neutral, result);
    }

    [TestMethod]
    public void Resolve_NullRaceId_ReturnsNeutral()
    {
        var result = _sut.Resolve(null);

        Assert.AreSame(RaceCombatModifiers.Neutral, result);
    }

    [TestMethod]
    public void Resolve_InvalidRaceId_ReturnsNeutral_WithoutNameLookup()
    {
        // Validate-before-lookup regression (Codex #33 class): an invalid id must never be
        // resolved through GetRaceNameFromId's "human" fallback into a real modifier row.
        AddRace("human", 0, new RaceCombatModifiers { CtbAttackBonus = 99f });
        _raceManager.IsValidRaceId(42).Returns(false);

        var result = _sut.Resolve(42);

        Assert.AreSame(RaceCombatModifiers.Neutral, result);
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void Resolve_ValidRaceWithRow_ReturnsConfiguredModifiers()
    {
        var dwarf = new RaceCombatModifiers { KnockdownResistanceMultiplier = 2.5f, StaggerThresholdMultiplier = 1.5f };
        AddRace("dwarf", 3, dwarf);

        var result = _sut.Resolve(3);

        Assert.AreSame(dwarf, result);
    }

    [TestMethod]
    public void Resolve_ValidRaceWithoutRow_ReturnsNeutral()
    {
        AddRace("dwarf", 3, new RaceCombatModifiers());
        _raceManager.IsValidRaceId(7).Returns(true);

        var result = _sut.Resolve(7);

        Assert.AreSame(RaceCombatModifiers.Neutral, result);
    }

    [TestMethod]
    public void Resolve_UnknownConfigKey_SkippedAndWarned()
    {
        _config.RaceModifiers["not_a_race"] = new RaceCombatModifiers { CtbAttackBonus = 50f };
        _raceManager.IsValidRaceName("not_a_race").Returns(false);
        _raceManager.IsValidRaceId(5).Returns(true);

        var result = _sut.Resolve(5);

        Assert.AreSame(RaceCombatModifiers.Neutral, result);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not_a_race")));
    }

    [TestMethod]
    public void Resolve_NullRow_ReturnsNeutralNotNull()
    {
        _config.RaceModifiers["elf"] = null;
        _raceManager.IsValidRaceName("elf").Returns(true);
        _raceManager.GetRaceIdFromName("elf").Returns(9);
        _raceManager.IsValidRaceId(9).Returns(true);

        var result = _sut.Resolve(9);

        Assert.IsNotNull(result);
        Assert.AreSame(RaceCombatModifiers.Neutral, result);
    }
}
