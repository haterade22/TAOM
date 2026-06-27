using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Config;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
public class CustomBattleServiceTests
{
    private IObjectManagerAdapter _objectManager;
    private IModLogger _logger;
    private ICustomBattleCommandersProvider _commandersProvider;
    private CustomBattleService _sut;

    [TestInitialize]
    public void Setup()
    {
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _logger = Substitute.For<IModLogger>();
        _commandersProvider = Substitute.For<ICustomBattleCommandersProvider>();
        // Default: no faction is curated, so existing tests exercise the default selection path.
        _commandersProvider.HasCuratedEntry(Arg.Any<string>()).Returns(false);
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>());
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>());
        _sut = new CustomBattleService(_objectManager, _logger, _commandersProvider);
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
            new() { Id = "lord_1_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "gondor_infantry", IsHero = false, CultureId = "gondor" }
        });

        // Act
        var result = _sut.GetCommanderIds();

        // Assert
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_1_1");
    }

    [TestMethod]
    public void GetCommanderIds_OnlyIncludesKingdomLords()
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
    public void GetCommanderIds_ExcludesSubLords()
    {
        // Arrange — 3-segment IDs are sub-lords (clan members), not kingdom lords
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_1_1",   IsHero = true, CultureId = "gondor" },  // kingdom lord
            new() { Id = "lord_1_1_1", IsHero = true, CultureId = "gondor" },  // sub-lord
            new() { Id = "lord_1_1_2", IsHero = true, CultureId = "gondor" },  // sub-lord
        });

        // Act
        var result = _sut.GetCommanderIds();

        // Assert
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
            new() { Id = "lord_1_1", IsHero = true, CultureId = "gondor" },
            new() { Id = "lord_2_1", IsHero = true, CultureId = "mordor" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("gondor");

        // Assert
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_1_1");
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
            new() { Id = "lord_1_1", IsHero = true, CultureId = "Gondor" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("gondor");

        // Assert
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_TakeMaxThree_CapsResults()
    {
        // Arrange — 5 empire lords; cap should return only 3
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_emp_1", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_2", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_3", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_4", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_5", IsHero = true, CultureId = "empire" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("empire", 3);

        // Assert
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_TakeMax_OrderIsDeterministic()
    {
        // Arrange — same input set, queried twice, should yield same sequence
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_emp_5", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_2", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_4", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_1", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_3", IsHero = true, CultureId = "empire" }
        });

        // Act
        var first = _sut.GetCommanderIdsForFaction("empire", 3);
        var second = _sut.GetCommanderIdsForFaction("empire", 3);

        // Assert — same sequence, alphabetical by Id
        CollectionAssert.AreEqual((System.Collections.ICollection)first, (System.Collections.ICollection)second);
        Assert.AreEqual("lord_emp_1", first[0]);
        Assert.AreEqual("lord_emp_2", first[1]);
        Assert.AreEqual("lord_emp_3", first[2]);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_TakeMax_FewerLordsThanCap_ReturnsAll()
    {
        // Arrange
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_emp_1", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_2", IsHero = true, CultureId = "empire" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("empire", 3);

        // Assert
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_TakeMaxZero_ReturnsEmpty()
    {
        // Arrange
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_emp_1", IsHero = true, CultureId = "empire" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("empire", 0);

        // Assert
        Assert.AreEqual(0, result.Count);
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

    // --- Curated commander config (custom_battle_commanders.json) ---

    [TestMethod]
    public void GetCommanderIdsForFaction_CuratedFaction_ReturnsCuratedListInOrder()
    {
        // Arrange — curated faction returns the provider's exact ordered list, untouched
        var curated = new List<string> { "lord_1_17", "lord_1_15", "lord_1_63" };
        _commandersProvider.HasCuratedEntry("mordor").Returns(true);
        _commandersProvider.GetCuratedCommanderIds("mordor").Returns(curated);
        // All curated ids exist as characters; lord_other is a default-path lord that must NOT appear (curated wins).
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_1_17", IsHero = true, CultureId = "mordor" },
            new() { Id = "lord_1_15", IsHero = true, CultureId = "mordor" },
            new() { Id = "lord_1_63", IsHero = true, CultureId = "mordor" },
            new() { Id = "lord_other", IsHero = true, CultureId = "mordor" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("mordor", 3);

        // Assert — same sequence, same order, no default-path lord
        CollectionAssert.AreEqual((System.Collections.ICollection)curated, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_CuratedFaction_BypassesRegexAndCap()
    {
        // Arrange — 5 ids including 3-segment ids that the IsValidCommander regex would reject
        var curated = new List<string> { "lord_4_1", "lord_4_3_1", "lord_4_3_2", "lord_4_7", "lord_4_16" };
        _commandersProvider.HasCuratedEntry("vlandia").Returns(true);
        _commandersProvider.GetCuratedCommanderIds("vlandia").Returns(curated);
        // All 5 exist as characters — existence is id-only, so 3-segment ids survive (the regex is not applied on the curated path).
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_4_1", IsHero = true, CultureId = "vlandia" },
            new() { Id = "lord_4_3_1", IsHero = true, CultureId = "vlandia" },
            new() { Id = "lord_4_3_2", IsHero = true, CultureId = "vlandia" },
            new() { Id = "lord_4_7", IsHero = true, CultureId = "vlandia" },
            new() { Id = "lord_4_16", IsHero = true, CultureId = "vlandia" }
        });

        // Act — takeMax=3 must be ignored on the curated path
        var result = _sut.GetCommanderIdsForFaction("vlandia", 3);

        // Assert — all 5 returned, including the 3-segment ids
        Assert.AreEqual(5, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_4_3_1");
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_4_3_2");
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_CuratedFaction_IgnoresCultureFilter()
    {
        // Arrange — curated ids whose CharacterInfo culture would NOT match the faction key
        var curated = new List<string> { "lord_1_48", "lord_WE9_l" }; // dolguldur + empire culture lords under "mordor"
        _commandersProvider.HasCuratedEntry("mordor").Returns(true);
        _commandersProvider.GetCuratedCommanderIds("mordor").Returns(curated);
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_1_48", IsHero = true, CultureId = "dolguldur" },
            new() { Id = "lord_WE9_l", IsHero = true, CultureId = "empire" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("mordor", 3);

        // Assert — returned despite culture mismatch
        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual((System.Collections.ICollection)curated, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_CuratedFaction_AllIdsUnresolvable_FallsBackToDefault()
    {
        // Arrange — curated faction lists only ids that don't exist as characters (typos / removed lords).
        // The faction's REAL lords still exist via the default path. (Codex review 2026-06-27 finding #1.)
        _commandersProvider.HasCuratedEntry("mordor").Returns(true);
        _commandersProvider.GetCuratedCommanderIds("mordor").Returns(new List<string> { "lord_typo_1", "lord_typo_2" });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_real_1", IsHero = true, CultureId = "mordor" },
            new() { Id = "lord_real_2", IsHero = true, CultureId = "mordor" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("mordor", 3);

        // Assert — falls back to the default per-culture path, NOT the unresolvable curated ids or the global list
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("lord_real_1", result[0]);
        Assert.AreEqual("lord_real_2", result[1]);
        CollectionAssert.DoesNotContain((System.Collections.ICollection)result, "lord_typo_1");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("falling back to default")));
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_CuratedFaction_PartiallyResolvable_ReturnsOnlyExistingInOrder()
    {
        // Arrange — curated list has two real ids and a typo; only the real ids exist.
        _commandersProvider.HasCuratedEntry("mordor").Returns(true);
        _commandersProvider.GetCuratedCommanderIds("mordor").Returns(new List<string> { "lord_1_17", "lord_typo", "lord_1_15" });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_1_17", IsHero = true, CultureId = "mordor" },
            new() { Id = "lord_1_15", IsHero = true, CultureId = "mordor" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("mordor", 3);

        // Assert — typo dropped; real ids kept in CURATED order (17 before 15 = not alphabetical -> curated path, no fallback)
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("lord_1_17", result[0]);
        Assert.AreEqual("lord_1_15", result[1]);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_NonCuratedFaction_UsesDefaultAlphabeticalTopN()
    {
        // Arrange — provider has no entry; default path applies
        _commandersProvider.HasCuratedEntry("empire").Returns(false);
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_emp_3", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_1", IsHero = true, CultureId = "empire" },
            new() { Id = "lord_emp_2", IsHero = true, CultureId = "empire" }
        });

        // Act
        var result = _sut.GetCommanderIdsForFaction("empire", 3);

        // Assert — alphabetical default behavior preserved
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("lord_emp_1", result[0]);
        Assert.AreEqual("lord_emp_2", result[1]);
        Assert.AreEqual("lord_emp_3", result[2]);
    }

    [TestMethod]
    public void GetCommanderIdsForFaction_NullFactionId_DoesNotConsultProvider()
    {
        // Act
        var result = _sut.GetCommanderIdsForFaction(null, 3);

        // Assert — null guard precedes the provider branch
        Assert.AreEqual(0, result.Count);
        _commandersProvider.DidNotReceive().HasCuratedEntry(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCommanderIds_Unchanged_DoesNotConsultCuratedProvider()
    {
        // Arrange — master list path must stay regex-filtered, independent of curated config
        _commandersProvider.HasCuratedEntry(Arg.Any<string>()).Returns(true);
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            new() { Id = "lord_1_1",   IsHero = true, CultureId = "gondor" },
            new() { Id = "lord_1_1_1", IsHero = true, CultureId = "gondor" }  // 3-segment sub-lord, regex-excluded
        });

        // Act
        var result = _sut.GetCommanderIds();

        // Assert — only the 2-segment lord; curated provider never consulted by the master list
        Assert.AreEqual(1, result.Count);
        CollectionAssert.Contains((System.Collections.ICollection)result, "lord_1_1");
        _commandersProvider.DidNotReceive().GetCuratedCommanderIds(Arg.Any<string>());
    }
}
