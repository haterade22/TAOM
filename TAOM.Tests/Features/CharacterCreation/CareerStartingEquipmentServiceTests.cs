using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CareerStartingEquipmentServiceTests
{
    private IPlayerEquipmentAdapter _adapter;
    private ICareerArchetypeService _archetypeService;
    private IModLogger _logger;
    private CareerStartingEquipmentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IPlayerEquipmentAdapter>();
        _archetypeService = Substitute.For<ICareerArchetypeService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CareerStartingEquipmentService(_adapter, _archetypeService, _logger);
    }

    private void ArchetypeReturns(string careerId, CareerArchetype archetype)
    {
        _archetypeService
            .TryGetArchetype(careerId, out Arg.Any<CareerArchetype>())
            .Returns(call =>
            {
                call[1] = archetype;
                return true;
            });
    }

    [TestMethod]
    public void Apply_RangedMale_BuildsCorrectRosterIdAndCallsAdapter()
    {
        ArchetypeReturns("ranger_of_ithilien", CareerArchetype.Ranged);
        _adapter.ApplyRosterToPlayer("player_career_gondor_ranged_m", "player_hero_id")
                .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyCareerStartingEquipment("gondor", "ranger_of_ithilien", isFemale: false, "player_hero_id");

        _adapter.Received(1).ApplyRosterToPlayer("player_career_gondor_ranged_m", "player_hero_id");
    }

    [TestMethod]
    public void Apply_CavalryFemale_BuildsCorrectRosterIdAndCallsAdapter()
    {
        ArchetypeReturns("knight_of_belfalas", CareerArchetype.Cavalry);
        _adapter.ApplyRosterToPlayer("player_career_gondor_cavalry_f", "player_hero_id")
                .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyCareerStartingEquipment("gondor", "knight_of_belfalas", isFemale: true, "player_hero_id");

        _adapter.Received(1).ApplyRosterToPlayer("player_career_gondor_cavalry_f", "player_hero_id");
    }

    [TestMethod]
    public void Apply_InfantryMordor_BuildsCorrectRosterId()
    {
        ArchetypeReturns("black_uruk_captain", CareerArchetype.Infantry);
        _adapter.ApplyRosterToPlayer("player_career_mordor_infantry_m", "h")
                .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyCareerStartingEquipment("mordor", "black_uruk_captain", isFemale: false, "h");

        _adapter.Received(1).ApplyRosterToPlayer("player_career_mordor_infantry_m", "h");
    }

    [TestMethod]
    public void Apply_NullCulture_NoOpAndWarns()
    {
        _sut.ApplyCareerStartingEquipment(null, "ranger_of_ithilien", false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_NullCareerId_NoOpAndLogsInfo()
    {
        _sut.ApplyCareerStartingEquipment("gondor", null, false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("no career selected")));
    }

    [TestMethod]
    public void Apply_EmptyCareerId_NoOpAndLogsInfo()
    {
        _sut.ApplyCareerStartingEquipment("gondor", string.Empty, false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("no career selected")));
    }

    [TestMethod]
    public void Apply_NullHeroId_NoOpAndWarns()
    {
        _sut.ApplyCareerStartingEquipment("gondor", "ranger_of_ithilien", false, null);

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("playerHeroId")));
    }

    [TestMethod]
    public void Apply_UnknownArchetype_NoOpAndLogsInfo()
    {
        _archetypeService
            .TryGetArchetype(Arg.Any<string>(), out Arg.Any<CareerArchetype>())
            .Returns(false);

        _sut.ApplyCareerStartingEquipment("gondor", "mystery_career", false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("no archetype mapped")));
    }

    [TestMethod]
    public void Apply_RosterNotFound_LogsWarningAndDoesNotCrash()
    {
        ArchetypeReturns("ranger_of_ithilien", CareerArchetype.Ranged);
        _adapter.ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
                .Returns(PlayerEquipmentApplyResult.RosterNotFound);

        _sut.ApplyCareerStartingEquipment("gondor", "ranger_of_ithilien", false, "player_hero_id");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("player_career_gondor_ranged_m")));
    }

    [TestMethod]
    public void Apply_NoSuitableEquipment_LogsWarning()
    {
        ArchetypeReturns("ranger_of_ithilien", CareerArchetype.Ranged);
        _adapter.ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
                .Returns(PlayerEquipmentApplyResult.NoSuitableEquipment);

        _sut.ApplyCareerStartingEquipment("gondor", "ranger_of_ithilien", false, "player_hero_id");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no battle or civilian")));
    }

    [TestMethod]
    public void Apply_HeroNotFound_LogsError()
    {
        ArchetypeReturns("ranger_of_ithilien", CareerArchetype.Ranged);
        _adapter.ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
                .Returns(PlayerEquipmentApplyResult.HeroNotFound);

        _sut.ApplyCareerStartingEquipment("gondor", "ranger_of_ithilien", false, "missing_hero");

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("missing_hero")));
    }

    [TestMethod]
    public void Apply_Success_LogsInfo()
    {
        ArchetypeReturns("ranger_of_ithilien", CareerArchetype.Ranged);
        _adapter.ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
                .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyCareerStartingEquipment("gondor", "ranger_of_ithilien", false, "player_hero_id");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("gondor") && s.Contains("Ranged")));
    }
}
