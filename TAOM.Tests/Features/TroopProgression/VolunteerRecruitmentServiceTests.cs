using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.TroopProgression;

[TestClass]
public class VolunteerRecruitmentServiceTests
{
    private VolunteerRecruitmentService _sut;
    private IRandomProvider _random;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _random = Substitute.For<IRandomProvider>();
        _random.Next(Arg.Any<int>()).Returns(0);
        _logger = Substitute.For<IModLogger>();
        _sut = new VolunteerRecruitmentService(_random, _logger);
    }

    // --- Culture fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_KnownCulture_ReturnsTroopFromCulturePool()
    {
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_GondorCulture_ReturnsGondorTroop()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_UnknownEverything_ReturnsNull()
    {
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "unknown_culture");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_AllNulls_ReturnsNull()
    {
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: null);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.IsNull(result);
    }

    // --- Settlement lookup (highest priority) ---

    [TestMethod]
    public void GetVolunteerTroopId_KnownSettlement_ReturnsSettlementTroop()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "town_EW5",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_bel_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_SettlementTakesPriorityOverClan()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "town_EW5",
            boundSettlementId: null,
            ownerClanId: "clan_empire_west_1",
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        // town_EW5 = Dol Amroth, should return Belfalas recruit, not Húrinionath clan troop
        Assert.AreEqual("gondor_bel_recruit", result);
    }

    // --- Bound settlement fallback (villages) ---

    [TestMethod]
    public void GetVolunteerTroopId_UnknownVillage_InheritsBoundSettlement()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_EW5_1",
            boundSettlementId: "town_EW5",
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_bel_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_CastleVillage_InheritsBoundCastle()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "castle_village_EW8_1",
            boundSettlementId: "castle_EW8",
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        // castle_EW8 = Amon Dîn → Lossarnach
        Assert.AreEqual("gondor_loss_lumberman", result);
    }

    // --- Clan fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_UnknownSettlement_FallsThroughToClan()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "unknown_settlement",
            boundSettlementId: null,
            ownerClanId: "clan_empire_west_2",
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        // clan_empire_west_2 = Imrazôrionath → Belfalas
        Assert.AreEqual("gondor_bel_recruit", result);
    }

    // --- Full fallback chain ---

    [TestMethod]
    public void GetVolunteerTroopId_SettlementMiss_BoundMiss_ClanMiss_CultureHit()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "unknown_settlement",
            boundSettlementId: "also_unknown",
            ownerClanId: "unknown_clan",
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }

    // --- Weighted random selection ---

    [TestMethod]
    public void GetVolunteerTroopId_MinasTirith_HighRoll_ReturnsIthilienRanger()
    {
        // town_EW1 = Minas Tirith: gondor_ano_peasant(7) + gondor_ithilien_ranger(3) = total 10
        // Roll 7 should land in the gondor_ithilien_ranger range
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ithilien_ranger", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_AmonostCastle_HighRoll_ReturnsIthilienRanger()
    {
        // castle_EW15 = Amonost: gondor_ano_peasant(7) + gondor_ithilien_ranger(3) = total 10
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(
            settlementId: "castle_EW15",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ithilien_ranger", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_ErethirCastle_HighRoll_ReturnsIthilienRanger()
    {
        // castle_EW16 = Erethir: gondor_ano_peasant(7) + gondor_ithilien_ranger(3) = total 10
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(
            settlementId: "castle_EW16",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ithilien_ranger", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_WeightedRandom_LowRoll_ReturnsRegularTroop()
    {
        // town_EW1: gondor_ano_peasant(7) + gondor_ithilien_ranger(3) = total 10
        // Roll 0 should return gondor_ano_peasant
        _random.Next(10).Returns(0);
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_WeightedRandom_BoundaryRoll_ReturnsCorrectTroop()
    {
        // town_EW1: gondor_ano_peasant(7) + gondor_ithilien_ranger(3) = total 10
        // Roll 6 = last index in regular range
        _random.Next(10).Returns(6);
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_AmonostCastle_LowRoll_ReturnsRegularTroop()
    {
        // castle_EW15 = Amonost: gondor_ano_peasant(7) + gondor_ithilien_ranger(3) = total 10
        _random.Next(10).Returns(0);
        var context = new VolunteerContext(
            settlementId: "castle_EW15",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_ErethirCastle_LowRoll_ReturnsRegularTroop()
    {
        // castle_EW16 = Erethir: gondor_ano_peasant(7) + gondor_ithilien_ranger(3) = total 10
        _random.Next(10).Returns(0);
        var context = new VolunteerContext(
            settlementId: "castle_EW16",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_NonIthilienSettlement_HighRoll_DoesNotReturnIthilienRanger()
    {
        // town_EW2 = Osgiliath: gondor_ano_peasant(7) + gondor_osg_veteran(3) — region-specificity check
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(
            settlementId: "town_EW2",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreNotEqual("gondor_ithilien_ranger", result);
        Assert.AreEqual("gondor_osg_veteran", result);
    }

    // --- Specific settlement verifications ---

    [TestMethod]
    [DataRow("town_EW1", "gondor_ano_peasant")]
    [DataRow("town_EW4", "gondor_leb_militia")]
    [DataRow("town_EW5", "gondor_bel_recruit")]
    [DataRow("town_EW9", "gondor_lam_clansman")]
    [DataRow("castle_EW4", "gondor_ano_peasant")]
    [DataRow("castle_EW8", "gondor_loss_lumberman")]
    [DataRow("castle_EW9", "gondor_bel_recruit")]
    public void GetVolunteerTroopId_SpecificSettlements_ReturnExpectedRegularTroop(
        string settlementId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Specific clan verifications ---

    [TestMethod]
    [DataRow("clan_empire_west_1", "gondor_ano_peasant")]
    [DataRow("clan_empire_west_2", "gondor_bel_recruit")]
    [DataRow("clan_empire_west_3", "gondor_leb_militia")]
    [DataRow("clan_empire_west_5", "gondor_loss_lumberman")]
    [DataRow("clan_empire_west_6", "gondor_pg_volunteer")]
    public void GetVolunteerTroopId_SpecificClans_ReturnExpectedRegularTroop(
        string clanId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: clanId,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Dol Guldur settlement verifications ---

    [TestMethod]
    [DataRow("town_DG1", "dg_goblin_slave")]
    [DataRow("castle_DG1", "dg_goblin_slave")]
    [DataRow("castle_DG2", "dg_goblin_slave")]
    [DataRow("castle_DG3", "dg_goblin_slave")]
    public void GetVolunteerTroopId_DolGuldurSettlements_ReturnExpectedRegularTroop(
        string settlementId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurSettlement_HighRoll_ReturnsShadowInitiate()
    {
        // town_DG1: dg_goblin_slave(7) + dg_khamul_shadow_initiate(3) = total 10
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(
            settlementId: "town_DG1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dg_khamul_shadow_initiate", result);
    }

    // --- Dol Guldur clan verifications ---

    [TestMethod]
    [DataRow("clan_dolguldur_1", "dg_goblin_slave")]
    [DataRow("clan_dolguldur_2", "dg_goblin_slave")]
    [DataRow("clan_dolguldur_3", "dg_goblin_slave")]
    [DataRow("clan_dolguldur_4", "dg_goblin_slave")]
    [DataRow("clan_dolguldur_5", "dg_goblin_slave")]
    [DataRow("clan_dolguldur_6", "dg_goblin_slave")]
    public void GetVolunteerTroopId_DolGuldurClans_ReturnExpectedRegularTroop(
        string clanId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: clanId,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Dol Guldur culture fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurCulture_ReturnsGoblinSlave()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dg_goblin_slave", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurCulture_ContainsKhamulInitiate()
    {
        // Culture pool: dg_goblin_slave(5) + dg_uruk_warrior(3) + dg_khamul_shadow_initiate(2) = 10
        // Roll 8 should land in khamul_shadow_initiate range
        _random.Next(10).Returns(8);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dg_khamul_shadow_initiate", result);
    }

    // --- Dol Guldur village bound settlement fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurVillage_InheritsBoundSettlement()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_DG1_1",
            boundSettlementId: "town_DG1",
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dg_goblin_slave", result);
    }

    // --- Erebor settlement verifications ---

    [TestMethod]
    [DataRow("town_E1", "erebor_reg_miner")]
    [DataRow("town_E2", "erebor_reg_miner")]
    [DataRow("town_E3", "erebor_reg_miner")]
    [DataRow("town_E4", "erebor_reg_miner")]
    [DataRow("castle_E1", "erebor_reg_miner")]
    [DataRow("castle_E2", "erebor_reg_miner")]
    [DataRow("castle_E3", "erebor_reg_miner")]
    [DataRow("castle_E4", "erebor_reg_miner")]
    [DataRow("castle_E5", "erebor_reg_miner")]
    [DataRow("castle_E6", "erebor_reg_miner")]
    [DataRow("castle_E7", "erebor_reg_miner")]
    [DataRow("castle_E8", "erebor_reg_miner")]
    [DataRow("castle_E9", "erebor_reg_miner")]
    public void GetVolunteerTroopId_EreborSettlements_ReturnExpectedRegularTroop(
        string settlementId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_EreborSettlement_HighRoll_ReturnsNoble()
    {
        // town_E1: erebor_reg_miner(5) + erebor_noble(3) = total 8
        _random.Next(8).Returns(5);
        var context = new VolunteerContext(
            settlementId: "town_E1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("erebor_noble", result);
    }

    // --- Erebor clan verifications ---

    [TestMethod]
    [DataRow("clan_erebor_1", "erebor_reg_miner")]
    [DataRow("clan_erebor_2", "erebor_reg_miner")]
    [DataRow("clan_erebor_3", "erebor_reg_miner")]
    [DataRow("clan_erebor_4", "erebor_reg_miner")]
    [DataRow("clan_erebor_5", "erebor_reg_miner")]
    [DataRow("clan_erebor_6", "erebor_reg_miner")]
    [DataRow("clan_erebor_7", "erebor_reg_miner")]
    public void GetVolunteerTroopId_EreborClans_ReturnExpectedRegularTroop(
        string clanId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: clanId,
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Erebor culture fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_EreborCulture_LowRoll_ReturnsMiner()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("erebor_reg_miner", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_EreborCulture_HighRoll_ReturnsIronHills()
    {
        // Culture pool: erebor_reg_miner(5) + erebor_noble(3) + iron_hills_reg_recruit(2) = 10
        // Roll 8 should land in iron_hills_reg_recruit range
        _random.Next(10).Returns(8);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("iron_hills_reg_recruit", result);
    }

    // --- Erebor village bound settlement fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_EreborVillage_InheritsBoundSettlement()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_E1_1",
            boundSettlementId: "town_E1",
            ownerClanId: null,
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("erebor_reg_miner", result);
    }

    // --- Shaghâna culture fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_ShaghânaCulture_ReturnsHaradLevy()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "shaghana");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_levy", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_ShaghânaCulture_HighRoll_ReturnsHaradNoble()
    {
        // Culture pool: harad_levy(7) + harad_noble(3) = total 10
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "shaghana");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_noble", result);
    }

    [TestMethod]
    [DataRow("clan_shaghana_1", "harad_levy")]
    [DataRow("clan_shaghana_2", "harad_levy")]
    [DataRow("clan_shaghana_3", "harad_levy")]
    [DataRow("clan_shaghana_4", "harad_levy")]
    [DataRow("clan_shaghana_5", "harad_levy")]
    [DataRow("clan_shaghana_6", "harad_levy")]
    [DataRow("clan_shaghana_7", "harad_levy")]
    [DataRow("clan_shaghana_8", "harad_levy")]
    [DataRow("clan_shaghana_9", "harad_levy")]
    public void GetVolunteerTroopId_ShaghânaClans_ReturnHaradLevy(string clanId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: clanId,
            cultureId: "shaghana");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Âbanissa culture fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_AbanissaCulture_ReturnsHaradLevy()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "abanissa");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_levy", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_AbanissaCulture_HighRoll_ReturnsHaradNoble()
    {
        // Culture pool: harad_levy(7) + harad_noble(3) = total 10
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "abanissa");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_noble", result);
    }

    [TestMethod]
    [DataRow("clan_abanissa_1", "harad_levy")]
    [DataRow("clan_abanissa_2", "harad_levy")]
    [DataRow("clan_abanissa_3", "harad_levy")]
    [DataRow("clan_abanissa_4", "harad_levy")]
    [DataRow("clan_abanissa_5", "harad_levy")]
    [DataRow("clan_abanissa_6", "harad_levy")]
    [DataRow("clan_abanissa_7", "harad_levy")]
    [DataRow("clan_abanissa_8", "harad_levy")]
    public void GetVolunteerTroopId_AbanissaClans_ReturnHaradLevy(string clanId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: clanId,
            cultureId: "abanissa");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Lothlorien settlement verifications (temporarily borrows Rivendell troops) ---

    [TestMethod]
    [DataRow("town_L1", "imladris_recruit")]
    [DataRow("castle_L1", "imladris_recruit")]
    [DataRow("castle_L2", "imladris_recruit")]
    [DataRow("castle_L3", "imladris_recruit")]
    public void GetVolunteerTroopId_LothlorienSettlements_ReturnExpectedRegularTroop(
        string settlementId, string expectedTroopId)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "lothlorien");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_LothlorienSettlement_HighRoll_ReturnsImladrisInfantry()
    {
        // town_L1: imladris_recruit(5) + imladris_infantry(3) = total 8
        _random.Next(8).Returns(5);
        var context = new VolunteerContext(
            settlementId: "town_L1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "lothlorien");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("imladris_infantry", result);
    }

    // --- Lothlorien clan verifications ---

    [TestMethod]
    public void GetVolunteerTroopId_LothlorienClan_ReturnsImladrisRecruit()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_lothlorien_1",
            cultureId: "lothlorien");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("imladris_recruit", result);
    }

    // --- Lothlorien culture fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_LothlorienCulture_LowRoll_ReturnsImladrisRecruit()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "lothlorien");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("imladris_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_LothlorienCulture_HighRoll_ReturnsImladrisBowman()
    {
        // Culture pool: imladris_recruit(5) + imladris_infantry(3) + imladris_bowman(2) = 10
        // Roll 8 should land in imladris_bowman range
        _random.Next(10).Returns(8);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "lothlorien");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("imladris_bowman", result);
    }

    // --- Lothlorien village bound settlement fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_LothlorienVillage_InheritsBoundSettlement()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_L1_1",
            boundSettlementId: "town_L1",
            ownerClanId: null,
            cultureId: "lothlorien");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("imladris_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_LothlorienCastleVillage_InheritsBoundCastle()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "castle_village_L1_1",
            boundSettlementId: "castle_L1",
            ownerClanId: null,
            cultureId: "lothlorien");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("imladris_recruit", result);
    }
}
