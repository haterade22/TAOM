using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
public class SideCommanderFilterTests
{
    private ICustomBattleService _service;
    private IObjectManagerAdapter _objectManager;
    private IModLogger _logger;
    private SideCommanderFilter _sut;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ICustomBattleService>();
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new SideCommanderFilter(_service, _objectManager, _logger);
    }

    [TestMethod]
    public void ResolveCommandersForCulture_NullCulture_ReturnsEmpty()
    {
        // Act
        var result = _sut.ResolveCommandersForCulture(null);

        // Assert
        Assert.AreEqual(0, result.Count);
        _service.DidNotReceive().GetCommanderIdsForFaction(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ResolveCommandersForCulture_EmptyCulture_ReturnsEmpty()
    {
        // Act
        var result = _sut.ResolveCommandersForCulture(string.Empty);

        // Assert
        Assert.AreEqual(0, result.Count);
        _service.DidNotReceive().GetCommanderIdsForFaction(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ResolveCommandersForCulture_QueriesServiceWithCapOfThree()
    {
        // Arrange
        _service.GetCommanderIdsForFaction("empire", 3).Returns(new List<string>());

        // Act
        _sut.ResolveCommandersForCulture("empire");

        // Assert — feature-level cap is 3 per culture
        _service.Received(1).GetCommanderIdsForFaction("empire", 3);
    }

    [TestMethod]
    public void ResolveCommandersForCulture_ResolvesAllReturnedIds()
    {
        // Arrange
        _service.GetCommanderIdsForFaction("empire", 3)
            .Returns(new List<string> { "lord_emp_1", "lord_emp_2", "lord_emp_3" });
        var c1 = Substitute.For<BasicCharacterObject>();
        var c2 = Substitute.For<BasicCharacterObject>();
        var c3 = Substitute.For<BasicCharacterObject>();
        _objectManager.GetBasicCharacter("lord_emp_1").Returns(c1);
        _objectManager.GetBasicCharacter("lord_emp_2").Returns(c2);
        _objectManager.GetBasicCharacter("lord_emp_3").Returns(c3);

        // Act
        var result = _sut.ResolveCommandersForCulture("empire");

        // Assert
        Assert.AreEqual(3, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, c1);
        CollectionAssert.Contains((System.Collections.ICollection)result, c2);
        CollectionAssert.Contains((System.Collections.ICollection)result, c3);
    }

    [TestMethod]
    public void ResolveCommandersForCulture_FiltersNullResolutions()
    {
        // Arrange — adapter returns null for an unknown id
        _service.GetCommanderIdsForFaction("empire", 3)
            .Returns(new List<string> { "lord_emp_1", "missing_lord" });
        var c1 = Substitute.For<BasicCharacterObject>();
        _objectManager.GetBasicCharacter("lord_emp_1").Returns(c1);
        _objectManager.GetBasicCharacter("missing_lord").Returns((BasicCharacterObject)null);

        // Act
        var result = _sut.ResolveCommandersForCulture("empire");

        // Assert
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, c1);
    }

    [TestMethod]
    public void ResolveCommandersForCulture_NoMatches_ReturnsEmpty()
    {
        // Arrange
        _service.GetCommanderIdsForFaction("empire", 3).Returns(new List<string>());

        // Act
        var result = _sut.ResolveCommandersForCulture("empire");

        // Assert
        Assert.AreEqual(0, result.Count);
    }
}
