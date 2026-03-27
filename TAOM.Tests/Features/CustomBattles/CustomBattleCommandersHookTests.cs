using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
public class CustomBattleCommandersHookTests
{
    private ICustomBattleService _service;
    private IObjectManagerAdapter _objectManager;
    private IModLogger _logger;
    private CustomBattleCommandersHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ICustomBattleService>();
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CustomBattleCommandersHook(_service, _objectManager, _logger);
    }

    [TestMethod]
    public void OnGetCustomBattleCommanders_ResolvesAllCommanderIds()
    {
        // Arrange
        _service.GetCommanderIds().Returns(new List<string> { "lord_gondor_1", "lord_mordor_1" });
        var gondorLord = Substitute.For<BasicCharacterObject>();
        var mordorLord = Substitute.For<BasicCharacterObject>();
        _objectManager.GetBasicCharacter("lord_gondor_1").Returns(gondorLord);
        _objectManager.GetBasicCharacter("lord_mordor_1").Returns(mordorLord);

        IEnumerable<BasicCharacterObject> result = new List<BasicCharacterObject>();

        // Act
        _sut.OnGetCustomBattleCommanders(ref result);

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public void OnGetCustomBattleCommanders_FiltersNullResolutions()
    {
        // Arrange
        _service.GetCommanderIds().Returns(new List<string> { "lord_gondor_1", "missing" });
        var gondorLord = Substitute.For<BasicCharacterObject>();
        _objectManager.GetBasicCharacter("lord_gondor_1").Returns(gondorLord);
        _objectManager.GetBasicCharacter("missing").Returns((BasicCharacterObject)null);

        IEnumerable<BasicCharacterObject> result = new List<BasicCharacterObject>();

        // Act
        _sut.OnGetCustomBattleCommanders(ref result);

        // Assert
        Assert.AreEqual(1, result.Count());
    }

    [TestMethod]
    public void OnGetCustomBattleCommanders_EmptyCommanderIds_ReturnsEmpty()
    {
        // Arrange
        _service.GetCommanderIds().Returns(new List<string>());

        IEnumerable<BasicCharacterObject> result = new List<BasicCharacterObject>();

        // Act
        _sut.OnGetCustomBattleCommanders(ref result);

        // Assert
        Assert.AreEqual(0, result.Count());
    }
}
