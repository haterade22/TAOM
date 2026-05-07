using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class PlayerEquipmentServiceTests
{
    private IPlayerEquipmentAdapter _adapter;
    private IModLogger _logger;
    private PlayerEquipmentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IPlayerEquipmentAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new PlayerEquipmentService(_adapter, _logger);
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_HappyPathMale_BuildsRosterIdAndCallsAdapter()
    {
        _adapter
            .ApplyRosterToPlayer("player_char_creation_gondor_retainer_m", "player_hero_id")
            .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", isFemale: false, "player_hero_id");

        _adapter.Received(1).ApplyRosterToPlayer("player_char_creation_gondor_retainer_m", "player_hero_id");
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_HappyPathFemale_BuildsFemaleSuffixedRosterId()
    {
        _adapter
            .ApplyRosterToPlayer("player_char_creation_rivendell_warrior_f", "player_hero_id")
            .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyPlayerStartingEquipment("rivendell", "warrior", isFemale: true, "player_hero_id");

        _adapter.Received(1).ApplyRosterToPlayer("player_char_creation_rivendell_warrior_f", "player_hero_id");
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_NullCulture_NoOpAndWarns()
    {
        _sut.ApplyPlayerStartingEquipment(null, "retainer", false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_EmptyTitleType_NoOpAndWarns()
    {
        _sut.ApplyPlayerStartingEquipment("gondor", string.Empty, false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("titleType")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_NullHeroId_NoOpAndWarns()
    {
        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, null);

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_RosterNotFound_LogsWarning()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.RosterNotFound);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "player_hero_id");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("player_char_creation_gondor_retainer_m")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_NoSuitableEquipment_LogsWarning()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.NoSuitableEquipment);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "player_hero_id");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no battle or civilian")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_HeroNotFound_LogsError()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.HeroNotFound);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "missing_hero");

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("missing_hero")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_Success_LogsInfo()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "player_hero_id");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("gondor") && s.Contains("retainer")));
    }
}
