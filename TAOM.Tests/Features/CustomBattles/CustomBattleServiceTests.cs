using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Config;
using TaleWorlds.Core;

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
            new() { Id = "gondor", CanHaveSettlement = true, IsBandit = false, HasFactionBanner = true },
            new() { Id = "mordor", CanHaveSettlement = true, IsBandit = false, HasFactionBanner = true },
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
    public void GetFactionIds_CultureWithoutFactionBanner_IsExcluded()
    {
        // Arrange: vanilla v1.5.3 nord/vakken/darshi, settlement-capable but no faction_banner_key.
        // Vanilla CustomBattleHelper.GetCustomBattleParties writes layer 0 of the faction banner and
        // throws ArgumentOutOfRangeException on an empty one (crash f9a7181d).
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "vakken", CanHaveSettlement = true, IsBandit = false, HasFactionBanner = false }
        });

        // Act
        var result = _sut.GetFactionIds();

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetFactionIds_CultureWithFactionBanner_IsIncluded()
    {
        // Arrange
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "rohan", CanHaveSettlement = true, IsBandit = false, HasFactionBanner = true },
            new() { Id = "nord", CanHaveSettlement = true, IsBandit = false, HasFactionBanner = false }
        });

        // Act
        var result = _sut.GetFactionIds();

        // Assert
        CollectionAssert.AreEqual(new[] { "rohan" }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void GetFactionIds_BanditCulture_IsExcluded()
    {
        // Arrange: the shipped raider shape (taom_spcultures.xml dunland_raiders), settlement-capable and
        // banner-bearing, so only the bandit clause keeps it out. gondor is the control that must survive.
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "dunland_raiders", CanHaveSettlement = true, IsBandit = true, HasFactionBanner = true },
            new() { Id = "gondor", CanHaveSettlement = true, IsBandit = false, HasFactionBanner = true }
        });

        // Act
        var result = _sut.GetFactionIds();

        // Assert
        CollectionAssert.AreEqual(new[] { "gondor" }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void GetFactionIds_CultureWithoutSettlement_IsExcluded()
    {
        // Arrange: not a bandit and has a banner, so only the settlement clause keeps it out.
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "landless", CanHaveSettlement = false, IsBandit = false, HasFactionBanner = true },
            new() { Id = "gondor", CanHaveSettlement = true, IsBandit = false, HasFactionBanner = true }
        });

        // Act
        var result = _sut.GetFactionIds();

        // Assert
        CollectionAssert.AreEqual(new[] { "gondor" }, (System.Collections.ICollection)result);
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

    private const FormationClass Infantry = FormationClass.Infantry, Ranged = FormationClass.Ranged,
        Cavalry = FormationClass.Cavalry, HorseArcher = FormationClass.HorseArcher,
        HeavyInfantry = FormationClass.HeavyInfantry, LightCavalry = FormationClass.LightCavalry,
        HeavyCavalry = FormationClass.HeavyCavalry;

    private static CharacterInfo Soldier(string id, string cultureId, FormationClass formationClass) =>
        new() { Id = id, CultureId = cultureId, IsHero = false, IsSoldier = true, DefaultFormationClass = formationClass };

    private void GivenGondor(CultureInfo gondor, params CharacterInfo[] troops)
    {
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo> { gondor });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>(troops));
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_Infantry_ReturnsMeleeMilitia()
    {
        // Arrange
        GivenGondor(
            new() { Id = "gondor", MeleeMilitiaTroopId = "gondor_peasant", BasicTroopId = "gondor_recruit" },
            Soldier("gondor_peasant", "gondor", Infantry),
            Soldier("gondor_recruit", "gondor", Infantry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 0, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_peasant", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_Ranged_ReturnsRangedMilitia()
    {
        // Arrange
        GivenGondor(new() { Id = "gondor", RangedMilitiaTroopId = "gondor_archer" }, Soldier("gondor_archer", "gondor", Ranged));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 1, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_archer", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_Cavalry_ReturnsEliteBasic()
    {
        // Arrange
        GivenGondor(new() { Id = "gondor", EliteBasicTroopId = "gondor_cavalry" }, Soldier("gondor_cavalry", "gondor", Cavalry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 2, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_cavalry", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_HorseArcher_ReturnsRangedEliteMilitia()
    {
        // Arrange
        GivenGondor(new() { Id = "gondor", RangedEliteMilitiaTroopId = "gondor_horse_archer" },
            Soldier("gondor_horse_archer", "gondor", HorseArcher));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 3, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_horse_archer", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_InfantryFallsBackToBasicTroop()
    {
        // Arrange
        GivenGondor(new() { Id = "gondor", MeleeMilitiaTroopId = null, BasicTroopId = "gondor_recruit" },
            Soldier("gondor_recruit", "gondor", Infantry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 0, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_recruit", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_MilitiaNotEligible_FallsBackToEligibleBasicTroop()
    {
        // Arrange: the militia troop is an archer, so the infantry slot cannot show it.
        GivenGondor(new() { Id = "gondor", MeleeMilitiaTroopId = "gondor_militia", BasicTroopId = "gondor_recruit" },
            Soldier("gondor_militia", "gondor", Ranged),
            Soldier("gondor_recruit", "gondor", Infantry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 0, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_recruit", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_TroopOfWrongFormationClass_ReturnsNull()
    {
        // Arrange: shipped data names a foot archer for the horse-archer slot; vanilla's picker would ignore it.
        GivenGondor(new() { Id = "gondor", RangedEliteMilitiaTroopId = "gondor_militia_veteran_archer" },
            Soldier("gondor_militia_veteran_archer", "gondor", Ranged));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 3, vanillaHasPick: true);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_TroopOfAnotherCulture_ReturnsNull()
    {
        // Arrange: Variag (battania) names Rhun troops, whose culture is khuzait.
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "battania", MeleeMilitiaTroopId = "rhun_militia_spearman" }
        });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            Soldier("rhun_militia_spearman", "khuzait", Infantry)
        });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("battania", 0, vanillaHasPick: true);

        // Assert
        Assert.IsNull(result);
    }

    // Abanissa's shape: no soldier of its own culture, and every troop attribute names a Harad (aserai) troop.
    private void GivenAbanissa()
    {
        _objectManager.GetAllCultureInfos().Returns(new List<CultureInfo>
        {
            new() { Id = "abanissa", RangedEliteMilitiaTroopId = "harad_militia_veteran_archer" }
        });
        _objectManager.GetAllCharacterInfos().Returns(new List<CharacterInfo>
        {
            Soldier("harad_militia_veteran_archer", "aserai", Ranged)
        });
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_NoFittingTroop_NoVanillaPick_ReturnsFirstLoadedCandidate()
    {
        // Arrange: with no vanilla pick the slot list is empty, and vanilla's PopulateListsWithDefaults spawns this
        // default as-is at Start; a null default for a slot with troops to spawn would throw there.
        GivenAbanissa();

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("abanissa", 3, vanillaHasPick: false);

        // Assert
        Assert.AreEqual("harad_militia_veteran_archer", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_NoVanillaPick_PrefersFittingCandidate()
    {
        // Arrange: the call every TAOM-only culture makes (vanilla's switch has no pick for it). The militia troop
        // loads but does not fit the infantry slot; the basic troop does, and must win over "first loaded".
        GivenGondor(new() { Id = "gondor", MeleeMilitiaTroopId = "gondor_militia", BasicTroopId = "gondor_recruit" },
            Soldier("gondor_militia", "gondor", Ranged),
            Soldier("gondor_recruit", "gondor", Infantry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 0, vanillaHasPick: false);

        // Assert
        Assert.AreEqual("gondor_recruit", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_NoFittingTroop_VanillaHasPick_ReturnsNull()
    {
        // Arrange
        GivenAbanissa();

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("abanissa", 3, vanillaHasPick: true);

        // Assert: vanilla's own pick stays.
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_NoVanillaPick_NoCandidateLoaded_ReturnsNull()
    {
        // Arrange
        GivenGondor(new() { Id = "gondor", RangedMilitiaTroopId = "gondor_archer" });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 1, vanillaHasPick: false);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_TroopNotLoaded_ReturnsNull()
    {
        // Arrange
        GivenGondor(new() { Id = "gondor", RangedMilitiaTroopId = "gondor_archer" });

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 1, vanillaHasPick: true);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_TroopNotASoldier_ReturnsNull()
    {
        // Arrange: vanilla's slot list holds only soldiers (ArmyCompositionGroupVM: IsSoldier && !IsObsolete);
        // a caravan guard fits the cavalry class but is not one.
        var guard = Soldier("gondor_caravan_guard", "gondor", Cavalry);
        guard.IsSoldier = false;
        GivenGondor(new() { Id = "gondor", EliteBasicTroopId = "gondor_caravan_guard" }, guard);

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 2, vanillaHasPick: true);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_ObsoleteTroop_ReturnsNull()
    {
        // Arrange
        var obsolete = Soldier("gondor_old_archer", "gondor", Ranged);
        obsolete.IsObsolete = true;
        GivenGondor(new() { Id = "gondor", RangedMilitiaTroopId = "gondor_old_archer" }, obsolete);

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 1, vanillaHasPick: true);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_MilitiaNotASoldier_FallsBackToBasicTroop()
    {
        // Arrange
        var militia = Soldier("gondor_militia", "gondor", Infantry);
        militia.IsSoldier = false;
        GivenGondor(new() { Id = "gondor", MeleeMilitiaTroopId = "gondor_militia", BasicTroopId = "gondor_recruit" },
            militia, Soldier("gondor_recruit", "gondor", Infantry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 0, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_recruit", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_SlotOutsideVanillaRange_ReturnsNull()
    {
        // Arrange: vanilla passes only slots 0-3; anything else keeps vanilla's own answer.
        GivenGondor(new() { Id = "gondor", BasicTroopId = "gondor_recruit" }, Soldier("gondor_recruit", "gondor", Infantry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 4, vanillaHasPick: true);

        // Assert: null by design, not through the catch-all.
        Assert.IsNull(result);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_HeavyClasses_AreAcceptedAsSiblings()
    {
        // Arrange: vanilla's slot filter takes HeavyInfantry as infantry and HeavyCavalry as cavalry.
        GivenGondor(new() { Id = "gondor", MeleeMilitiaTroopId = "gondor_guard", EliteBasicTroopId = "gondor_knight" },
            Soldier("gondor_guard", "gondor", HeavyInfantry),
            Soldier("gondor_knight", "gondor", HeavyCavalry));

        // Act
        var infantry = _sut.GetDefaultTroopIdForFormation("gondor", 0, vanillaHasPick: true);
        var cavalry = _sut.GetDefaultTroopIdForFormation("gondor", 2, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_guard", infantry);
        Assert.AreEqual("gondor_knight", cavalry);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_LightCavalry_IsAcceptedAsCavalry()
    {
        // Arrange
        GivenGondor(new() { Id = "gondor", EliteBasicTroopId = "gondor_outrider" },
            Soldier("gondor_outrider", "gondor", LightCavalry));

        // Act
        var result = _sut.GetDefaultTroopIdForFormation("gondor", 2, vanillaHasPick: true);

        // Assert
        Assert.AreEqual("gondor_outrider", result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_UnknownCulture_ReturnsNull()
    {
        // Act
        var result = _sut.GetDefaultTroopIdForFormation("unknown", 0, vanillaHasPick: true);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefaultTroopIdForFormation_NullFactionId_ReturnsNull()
    {
        // Act
        var result = _sut.GetDefaultTroopIdForFormation(null, 0, vanillaHasPick: true);

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
