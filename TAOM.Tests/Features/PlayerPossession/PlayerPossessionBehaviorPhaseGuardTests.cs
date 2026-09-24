using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.PlayerPossession;

namespace TAOM.Tests.Features.PlayerPossession;

/// <summary>
/// v1.5.0 made OnCharacterCreationIsOverEvent an MbEvent&lt;int&gt; fired ten times (index 0..9). The
/// character-creation choices are captured in the last phase only; the positive case reads
/// Hero.MainHero, so it is live-game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PlayerPossessionBehaviorPhaseGuardTests
{
    private IPlayerPossessionService _possession;
    private PlayerPossessionBehavior _sut;

    [TestInitialize]
    public void Setup()
    {
        _possession = Substitute.For<IPlayerPossessionService>();
        _sut = new PlayerPossessionBehavior(
            _possession,
            Substitute.For<IJoinReconciliationService>(),
            Substitute.For<ICareerMenuService>(),
            Substitute.For<IModLogger>());
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void OnCharacterCreationIsOver_BeforeTheLastPhase_DoesNotCapture(int index)
    {
        _sut.OnCharacterCreationIsOver(index);

        _possession.DidNotReceive().CaptureCharacterCreationChoices(Arg.Any<PlayerCharacterCreationChoices>());
    }
}
