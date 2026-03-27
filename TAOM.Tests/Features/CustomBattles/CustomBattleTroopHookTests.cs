using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
public class CustomBattleTroopHookTests
{
    private ICustomBattleService _service;
    private IObjectManagerAdapter _objectManager;
    private IModLogger _logger;
    private CustomBattleTroopHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ICustomBattleService>();
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CustomBattleTroopHook(_service, _objectManager, _logger);
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_VanillaAlreadyResolved_DoesNotOverride()
    {
        // Arrange
        var existingTroop = Substitute.For<BasicCharacterObject>();
        BasicCharacterObject result = existingTroop;

        // Act
        _sut.OnGetDefaultTroopOfFormation("gondor", 0, ref result);

        // Assert
        Assert.AreSame(existingTroop, result);
        _service.DidNotReceive().GetDefaultTroopIdForFormation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_TaomCulture_ResolvesTroop()
    {
        // Arrange
        var gondorTroop = Substitute.For<BasicCharacterObject>();
        _service.GetDefaultTroopIdForFormation("gondor", 0).Returns("gondor_peasant");
        _objectManager.GetBasicCharacter("gondor_peasant").Returns(gondorTroop);
        BasicCharacterObject result = null;

        // Act
        _sut.OnGetDefaultTroopOfFormation("gondor", 0, ref result);

        // Assert
        Assert.AreSame(gondorTroop, result);
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_ServiceReturnsNull_DoesNotSetResult()
    {
        // Arrange
        _service.GetDefaultTroopIdForFormation("gondor", 2).Returns((string)null);
        BasicCharacterObject result = null;

        // Act
        _sut.OnGetDefaultTroopOfFormation("gondor", 2, ref result);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_NullCultureId_DoesNothing()
    {
        // Arrange
        BasicCharacterObject result = null;

        // Act
        _sut.OnGetDefaultTroopOfFormation(null, 0, ref result);

        // Assert
        Assert.IsNull(result);
        _service.DidNotReceive().GetDefaultTroopIdForFormation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_ObjectManagerReturnsNull_DoesNotSetResult()
    {
        // Arrange
        _service.GetDefaultTroopIdForFormation("gondor", 0).Returns("gondor_peasant");
        _objectManager.GetBasicCharacter("gondor_peasant").Returns((BasicCharacterObject)null);
        BasicCharacterObject result = null;

        // Act
        _sut.OnGetDefaultTroopOfFormation("gondor", 0, ref result);

        // Assert
        Assert.IsNull(result);
    }
}
