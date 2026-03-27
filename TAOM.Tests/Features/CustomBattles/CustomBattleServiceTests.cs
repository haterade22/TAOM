using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
public class CustomBattleServiceTests
{
    private IObjectManagerAdapter _objectManager;
    private IModLogger _logger;
    private CustomBattleService _sut;

    [TestInitialize]
    public void Setup()
    {
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _logger = Substitute.For<IModLogger>();
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>());
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>());
        _sut = new CustomBattleService(_objectManager, _logger);
    }

    [TestMethod]
    public void GetFactionIds_ReturnsCulturesWithSettlements()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "gondor", CanHaveSettlement = true, IsBandit = false },
            new() { Id = "mordor", CanHaveSettlement = true, IsBandit = false },
            new() { Id = "looters", CanHaveSettlement = false, IsBandit = true }
        });

        // Act
        var result = _sut.GetFactionIds();

        // Assert
        Assert.AreEqual(2, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "gondor");
        CollectionAssert.Contains((System.Collections.ICollection)result, "mordor");
    }

    [TestMethod]
    public void GetFactionIds_ExcludesBanditCultures()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "sea_raiders", CanHaveSettlement = false, IsBandit = true }
        });

        // Act
        var result = _sut.GetFactionIds();

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetFactionIds_EmptyObjectManager_ReturnsEmpty()
    {
        // Act
        var result = _sut.GetFactionIds();

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetCommanderIds_ReturnsLordCharacters()
    {
        // Arrange
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_gondor_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "gondor_infantry", IsHero = false, CultureId = "gondor" }
        });

        // Act
        var result = _sut.GetCommanderIds();

        // Assert
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_gondor_1");
    }

    [TestMethod]
    public void GetCommanderIds_OnlyIncludesLordPrefix()
    {
        // Arrange — various non-lord heroes that should be excluded
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_1_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "companion_wanderer_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "spc_wanderer_gondor_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "spc_notable_gondor_0", IsHero = true, CultureId = "gondor" },
            new() { Id = "commander_1", IsHero = true, CultureId = "empire" },
            new() { Id = "tutorial_npc_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "battania_townsman", IsHero = true, CultureId = "battania" },
            new() { Id = "gondor_infantry", IsHero = false, CultureId = "gondor" }
        });

        // Act
        var result = _sut.GetCommanderIds();

        // Assert — only the lord_ prefixed hero passes
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_1_1");
    }

    [TestMethod]
    public void GetCommanderIds_ExcludesNonHeroLords()
    {
        // Arrange — lord_ prefix but not a hero
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_template_1", IsHero = false, CultureId = "gondor" }
        });

        // Act
        var result = _sut.GetCommanderIds();

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_FiltersByCulture()
    {
        // Arrange
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_gondor_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "lord_mordor_1", IsHero = true, CultureId = "mordor" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("gondor");

        // Assert
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_gondor_1");
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_NullFactionId_ReturnsEmpty()
    {
        // Act
        var result = _sut.GetCommanderIdsForFaction(null);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_CaseInsensitive()
    {
        // Arrange
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_gondor_1", IsHero = true, CultureId = "Gondor" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("gondor");

        // Assert
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_Infantry_ReturnsMeleeMilitia()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new()
            {
                Id = "gondor", CanHaveSettlement = true, IsBandit = false,
                MeleeMilitiaTroopId = "gondor_peasant",
                RangedMilitiaTroopId = "gondor_archer",
                EliteBasicTroopId = "gondor_cavalry",
                RangedEliteMilitiaTroopId = "gondor_horse_archer",
                BasicTroopId = "gondor_recruit"
            }
        });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 0);

        // Assert
        Assert.AreEqual("gondor_peasant", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_Ranged_ReturnsRangedMilitia()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new()
            {
                Id = "gondor", CanHaveSettlement = true, IsBandit = false,
                RangedMilitiaTroopId = "gondor_archer"
            }
        });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 1);

        // Assert
        Assert.AreEqual("gondor_archer", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_Cavalry_ReturnsEliteBasic()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new()
            {
                Id = "gondor", CanHaveSettlement = true, IsBandit = false,
                EliteBasicTroopId = "gondor_cavalry"
            }
        });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 2);

        // Assert
        Assert.AreEqual("gondor_cavalry", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_HorseArcher_ReturnsRangedEliteMilitia()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new()
            {
                Id = "gondor", CanHaveSettlement = true, IsBandit = false,
                RangedEliteMilitiaTroopId = "gondor_horse_archer"
            }
        });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 3);

        // Assert
        Assert.AreEqual("gondor_horse_archer", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_InfantryFallsBackToBasicTroop()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new()
            {
                Id = "gondor", CanHaveSettlement = true, IsBandit = false,
                MeleeMilitiaTroopId = null,
                BasicTroopId = "gondor_recruit"
            }
        });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 0);

        // Assert
        Assert.AreEqual("gondor_recruit", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_UnknownCulture_ReturnsNull()
    {
        // Act
        var result = _sut.GetDefaultTroopIdForFormation("unknown", 0);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_NullFactionId_ReturnsNull()
    {
        // Act
        var result = _sut.GetDefaultTroopIdForFormation(null, 0);

        // Assert
        Assert.IsNull(result);
    }
}
