using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class EyeHeightAdjustmentHookTests
{
    private EyeHeightAdjustmentHook _sut;
    private IRaceManager _raceManager;
    private IFaceGenAdapter _faceGenAdapter;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _raceManager = Substitute.For<IRaceManager>();
        _faceGenAdapter = Substitute.For<IFaceGenAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new EyeHeightAdjustmentHook(_raceManager, _faceGenAdapter, _logger);
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_RaceZero_ReturnsEarlyWithoutAction()
    {
        Monster monster = default;

        _sut.OnGetBaseMonsterFromRace(ref monster, 0);

        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_NegativeRace_ReturnsEarlyWithoutAction()
    {
        Monster monster = default;

        _sut.OnGetBaseMonsterFromRace(ref monster, -1);

        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_WhenAdapterReturnsNullMonster_LogsInitErrorAndReturnsEarly()
    {
        Monster monster = default;
        _raceManager.GetRaceNameFromId(1).Returns("elf");

        _sut.OnGetBaseMonsterFromRace(ref monster, 1);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed to initialize _defaultMonster")));
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_AfterInitFailure_DoesNotReinitialize()
    {
        Monster monster = default;

        _sut.OnGetBaseMonsterFromRace(ref monster, 1);
        _sut.OnGetBaseMonsterFromRace(ref monster, 2);

        _faceGenAdapter.Received(1).GetBaseMonsterFromRace(0);
    }
}
