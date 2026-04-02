using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation;

namespace TAOM.Tests.Features.ShaderPrecompilation;

[TestClass]
public class ShaderPrecompilationServiceTests
{
    private IObjectManagerAdapter _objectManager;
    private IModLogger _logger;
    private ShaderPrecompilationService _sut;

    [TestInitialize]
    public void Setup()
    {
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _logger = Substitute.For<IModLogger>();
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>());
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>());
        _sut = new ShaderPrecompilationService(_objectManager, _logger);
    }

    [TestMethod]
    public void GetCharacterIdsForShaderBattle_WithNonBanditCultureCharacters_ReturnsTheirIds()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "gondor", CanHaveSettlement = true, IsBandit = false },
            new() { Id = "mordor", CanHaveSettlement = true, IsBandit = false }
        });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "gondor_infantry",    IsHero = false, CultureId = "gondor" },
            new() { Id = "mordor_orc_warrior", IsHero = false, CultureId = "mordor" }
        });

        // Act
        var result = _sut.GetCharacterIdsForShaderBattle();

        // Assert
        Assert.AreEqual(2, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "gondor_infantry");
        CollectionAssert.Contains((System.Collections.ICollection)result, "mordor_orc_warrior");
    }

    [TestMethod]
    public void GetCharacterIdsForShaderBattle_WithBanditCultureCharacters_ExcludesThem()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "looters", CanHaveSettlement = false, IsBandit = true }
        });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "looter", IsHero = false, CultureId = "looters" }
        });

        // Act
        var result = _sut.GetCharacterIdsForShaderBattle();

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetCharacterIdsForShaderBattle_WhenAdapterThrows_ReturnsEmptyAndLogsError()
    {
        // Arrange
        _objectManager.GetAllCharacterInfos().Throws(new Exception("Adapter failure"));

        // Act
        var result = _sut.GetCharacterIdsForShaderBattle();

        // Assert
        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCharacterIdsForShaderBattle_WithDuplicateIds_DeduplicatesThem()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "gondor", CanHaveSettlement = true, IsBandit = false }
        });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "gondor_infantry", IsHero = false, CultureId = "gondor" },
            new() { Id = "gondor_infantry", IsHero = false, CultureId = "gondor" }
        });

        // Act
        var result = _sut.GetCharacterIdsForShaderBattle();

        // Assert
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void GetCharacterIdsForShaderBattle_WithNullOrEmptyIds_ExcludesThem()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "gondor", CanHaveSettlement = true, IsBandit = false }
        });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = null,              IsHero = false, CultureId = "gondor" },
            new() { Id = "",               IsHero = false, CultureId = "gondor" },
            new() { Id = "gondor_soldier", IsHero = false, CultureId = "gondor" }
        });

        // Act
        var result = _sut.GetCharacterIdsForShaderBattle();

        // Assert
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "gondor_soldier");
    }

    [TestMethod]
    public void GetCultureIdsForShaderBattle_WithMixedBanditAndNonBandit_ReturnsOnlyNonBandit()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "gondor",  CanHaveSettlement = true,  IsBandit = false },
            new() { Id = "rohan",   CanHaveSettlement = true,  IsBandit = false },
            new() { Id = "looters", CanHaveSettlement = false, IsBandit = true  }
        });

        // Act
        var result = _sut.GetCultureIdsForShaderBattle();

        // Assert
        Assert.AreEqual(2, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "gondor");
        CollectionAssert.Contains((System.Collections.ICollection)result, "rohan");
    }
}
