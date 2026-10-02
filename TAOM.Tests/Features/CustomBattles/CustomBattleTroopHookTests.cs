using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
[TestCategory("RequiresGame")]
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
    public void OnGetDefaultTroopOfFormation_VanillaResolvedCalradianTroop_TaomTroopReplacesIt()
    {
        // Arrange: vanilla's switch returns a Calradian troop for the six re-skinned cultures (vlandia ->
        // vlandian_swordsman), which still exist in a Custom Battle; TAOM's own culture troop wins.
        var calradian = Substitute.For<BasicCharacterObject>();
        var rohirrim = Substitute.For<BasicCharacterObject>();
        _service.GetDefaultTroopIdForFormation("vlandia", 0, true).Returns("rohan_militia_spearman");
        _objectManager.GetBasicCharacter("rohan_militia_spearman").Returns(rohirrim);
        BasicCharacterObject result = calradian;

        // Act
        _sut.OnGetDefaultTroopOfFormation("vlandia", 0, ref result);

        // Assert
        Assert.AreSame(rohirrim, result);
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_VanillaResolved_TaomHasNoTroop_KeepsVanilla()
    {
        // Arrange
        var vanillaTroop = Substitute.For<BasicCharacterObject>();
        _service.GetDefaultTroopIdForFormation("vlandia", 3, true).Returns((string)null);
        BasicCharacterObject result = vanillaTroop;

        // Act
        _sut.OnGetDefaultTroopOfFormation("vlandia", 3, ref result);

        // Assert
        Assert.AreSame(vanillaTroop, result);
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_VanillaResolved_TaomTroopUnresolvable_KeepsVanilla()
    {
        // Arrange
        var vanillaTroop = Substitute.For<BasicCharacterObject>();
        _service.GetDefaultTroopIdForFormation("vlandia", 0, true).Returns("rohan_militia_spearman");
        _objectManager.GetBasicCharacter("rohan_militia_spearman").Returns((BasicCharacterObject)null);
        BasicCharacterObject result = vanillaTroop;

        // Act
        _sut.OnGetDefaultTroopOfFormation("vlandia", 0, ref result);

        // Assert
        Assert.AreSame(vanillaTroop, result);
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_TaomCulture_ResolvesTroop()
    {
        // Arrange
        var gondorTroop = Substitute.For<BasicCharacterObject>();
        _service.GetDefaultTroopIdForFormation("gondor", 0, false).Returns("gondor_peasant");
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
        _service.GetDefaultTroopIdForFormation("gondor", 2, false).Returns((string)null);
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
        _service.DidNotReceive().GetDefaultTroopIdForFormation(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [TestMethod]
    public void OnGetDefaultTroopOfFormation_ObjectManagerReturnsNull_DoesNotSetResult()
    {
        // Arrange
        _service.GetDefaultTroopIdForFormation("gondor", 0, false).Returns("gondor_peasant");
        _objectManager.GetBasicCharacter("gondor_peasant").Returns((BasicCharacterObject)null);
        BasicCharacterObject result = null;

        // Act
        _sut.OnGetDefaultTroopOfFormation("gondor", 0, ref result);

        // Assert
        Assert.IsNull(result);
    }
}
