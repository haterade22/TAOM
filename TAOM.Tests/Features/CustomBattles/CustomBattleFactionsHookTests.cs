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
public class CustomBattleFactionsHookTests
{
    private ICustomBattleService _service;
    private IObjectManagerAdapter _objectManager;
    private IModLogger _logger;
    private CustomBattleFactionsHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ICustomBattleService>();
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CustomBattleFactionsHook(_service, _objectManager, _logger);
    }

    [TestMethod]
    public void OnGetCustomBattleFactions_ResolvesAllFactionIds()
    {
        // Arrange
        _service.GetFactionIds().Returns(new List<string> { "gondor", "mordor" });
        var gondorCulture = Substitute.For<BasicCultureObject>();
        var mordorCulture = Substitute.For<BasicCultureObject>();
        _objectManager.GetBasicCulture("gondor").Returns(gondorCulture);
        _objectManager.GetBasicCulture("mordor").Returns(mordorCulture);

        IEnumerable<BasicCultureObject> result = new List<BasicCultureObject>();

        // Act
        _sut.OnGetCustomBattleFactions(ref result);

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public void OnGetCustomBattleFactions_FiltersNullResolutions()
    {
        // Arrange
        _service.GetFactionIds().Returns(new List<string> { "gondor", "missing" });
        var gondorCulture = Substitute.For<BasicCultureObject>();
        _objectManager.GetBasicCulture("gondor").Returns(gondorCulture);
        _objectManager.GetBasicCulture("missing").Returns((BasicCultureObject)null);

        IEnumerable<BasicCultureObject> result = new List<BasicCultureObject>();

        // Act
        _sut.OnGetCustomBattleFactions(ref result);

        // Assert
        Assert.AreEqual(1, result.Count());
    }

    [TestMethod]
    public void OnGetCustomBattleFactions_EmptyFactionIds_ReturnsEmpty()
    {
        // Arrange
        _service.GetFactionIds().Returns(new List<string>());

        IEnumerable<BasicCultureObject> result = new List<BasicCultureObject>();

        // Act
        _sut.OnGetCustomBattleFactions(ref result);

        // Assert
        Assert.AreEqual(0, result.Count());
    }
}
