using System.Collections.Generic;
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

        // town_EW5 = Dol Amroth, regular troop is now Dol Amroth noble (was Belfalas recruit before geography pass)
        Assert.AreEqual("gondor_da_noble", result);
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

        // town_EW5 (Dol Amroth → da_noble) must win over the clan_empire_west_1 pool
        Assert.AreEqual("gondor_da_noble", result);
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

        // town_EW5 = Dol Amroth → da_noble at roll 0
        Assert.AreEqual("gondor_da_noble", result);
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

        // castle_EW8 = Hyarpëndë (Pinnath Gelin / Arndir) → pg_volunteer at roll 0
        Assert.AreEqual("gondor_pg_volunteer", result);
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
    // town_EW1 (Minas Tirith): peasant(6) + ranger(1) + fountain_guard(1) + trainee(2) = total 10
    [DataRow(0, "gondor_ano_peasant")]       // peasant covers rolls 0..5
    [DataRow(5, "gondor_ano_peasant")]
    [DataRow(6, "gondor_ithilien_ranger")]   // ranger at roll 6
    [DataRow(7, "gondor_mt_fountain_guard")] // fountain_guard at roll 7
    [DataRow(8, "gondor_mt_trainee")]        // trainee covers rolls 8..9
    [DataRow(9, "gondor_mt_trainee")]
    public void GetVolunteerTroopId_MinasTirith_BoundaryRolls_ReturnExpectedTroop(int roll, string expectedTroopId)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    // castle_EW15 / castle_EW16 (Amonost / Erethir, both owned by clan_empire_west_10 / Methir):
    // har_conscript(7) + met_noble(3) — total 10
    [DataRow("castle_EW15", 0, "gondor_har_conscript")]
    [DataRow("castle_EW15", 6, "gondor_har_conscript")]
    [DataRow("castle_EW15", 7, "gondor_met_noble")]
    [DataRow("castle_EW15", 9, "gondor_met_noble")]
    [DataRow("castle_EW16", 0, "gondor_har_conscript")]
    [DataRow("castle_EW16", 6, "gondor_har_conscript")]
    [DataRow("castle_EW16", 7, "gondor_met_noble")]
    [DataRow("castle_EW16", 9, "gondor_met_noble")]
    public void GetVolunteerTroopId_MethirClanCastles_BoundaryRolls_ReturnExpectedTroop(
        string settlementId, int roll, string expectedTroopId)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    // clan_empire_west_1: peasant(6) + ranger(1) + fountain_guard(1) + trainee(2) = total 10
    [DataRow(0, "gondor_ano_peasant")]
    [DataRow(5, "gondor_ano_peasant")]
    [DataRow(6, "gondor_ithilien_ranger")]
    [DataRow(7, "gondor_mt_fountain_guard")]
    [DataRow(8, "gondor_mt_trainee")]
    [DataRow(9, "gondor_mt_trainee")]
    public void GetVolunteerTroopId_ClanEmpireWest1_BoundaryRolls_ReturnExpectedTroop(int roll, string expectedTroopId)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_empire_west_1",
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    // town_EW2 / town_EW3 (West / East Osgiliath): osg_veteran(6) + ano_peasant(4) — total 10
    [DataRow("town_EW2", 0, "gondor_osg_veteran")]
    [DataRow("town_EW2", 5, "gondor_osg_veteran")]
    [DataRow("town_EW2", 6, "gondor_ano_peasant")]
    [DataRow("town_EW2", 9, "gondor_ano_peasant")]
    [DataRow("town_EW3", 0, "gondor_osg_veteran")]
    [DataRow("town_EW3", 5, "gondor_osg_veteran")]
    [DataRow("town_EW3", 6, "gondor_ano_peasant")]
    [DataRow("town_EW3", 9, "gondor_ano_peasant")]
    public void GetVolunteerTroopId_OsgiliathSettlements_BoundaryRolls_ReturnExpectedTroop(
        string settlementId, int roll, string expectedTroopId)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreNotEqual("gondor_ithilien_ranger", result);
        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Specific settlement verifications ---

    [TestMethod]
    [DataRow("town_EW1",    "gondor_ano_peasant")]
    [DataRow("town_EW4",    "gondor_pel_skirmisher")]
    [DataRow("town_EW5",    "gondor_da_noble")]
    [DataRow("town_EW6",    "gondor_anf_levy")]
    [DataRow("town_EW9",    "gondor_cal_noble")]
    [DataRow("town_EW10",   "gondor_ser_noble")]
    [DataRow("town_EW11",   "gondor_met_noble")]
    [DataRow("castle_EW3",  "gondor_bel_recruit")]
    [DataRow("castle_EW4",  "gondor_ca_noble")]
    [DataRow("castle_EW8",  "gondor_pg_volunteer")]
    [DataRow("castle_EW10", "gondor_har_conscript")]
    [DataRow("castle_EW11", "gondor_bel_recruit")]
    [DataRow("castle_EW9",  "gondor_tol_arbalest")]
    [DataRow("castle_EW12", "gondor_lin_noble")]
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
    [DataRow("clan_empire_west_1",  "gondor_ano_peasant")]
    [DataRow("clan_empire_west_2",  "gondor_bel_recruit")]
    [DataRow("clan_empire_west_3",  "gondor_leb_militia")]
    [DataRow("clan_empire_west_5",  "gondor_loss_lumberman")]
    [DataRow("clan_empire_west_6",  "gondor_pg_volunteer")]
    [DataRow("clan_empire_west_8",  "gondor_anf_levy")]
    [DataRow("clan_empire_west_9",  "gondor_brv_bowman")]
    [DataRow("clan_empire_west_10", "gondor_har_conscript")]
    [DataRow("clan_empire_west_11", "gondor_ca_noble")]
    [DataRow("clan_empire_west_12", "gondor_lin_noble")]
    [DataRow("clan_empire_west_13", "gondor_tol_arbalest")]
    [DataRow("clan_empire_west_14", "gondor_anf_levy")]
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

    [TestMethod]
    // clan_empire_west_11 (House of Caladionath, Cair Andros): ca_noble(9) + ithilien_ranger(1) — total 10
    [DataRow(0, "gondor_ca_noble")]
    [DataRow(8, "gondor_ca_noble")]
    [DataRow(9, "gondor_ithilien_ranger")]
    public void GetVolunteerTroopId_ClanEmpireWest11_BoundaryRolls_ReturnExpectedTroop(int roll, string expectedTroopId)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_empire_west_11",
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    // clan_empire_west_8 (House of Olindurionath, Anfalas + Seregond):
    // anf_levy(5) + ser_pikeman(2) + ser_noble(2) + anf_guardsman(1) — total 10
    [DataRow(0, "gondor_anf_levy")]
    [DataRow(4, "gondor_anf_levy")]
    [DataRow(5, "gondor_ser_pikeman")]
    [DataRow(6, "gondor_ser_pikeman")]
    [DataRow(7, "gondor_ser_noble")]
    [DataRow(8, "gondor_ser_noble")]
    [DataRow(9, "gondor_anf_guardsman")]
    public void GetVolunteerTroopId_ClanEmpireWest8_BoundaryRolls_ReturnExpectedTroop(int roll, string expectedTroopId)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_empire_west_8",
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

    // --- Rhun settlement pools ---

    [TestMethod]
    [DataRow("town_RU7",   "dragon_wrath_acolyte")]  // Khûndol
    [DataRow("castle_RU1", "dragon_wrath_acolyte")]  // Mârdûn
    [DataRow("castle_RU2", "dragon_wrath_acolyte")]  // Tarlat Arlan
    [DataRow("castle_RU3", "dragon_wrath_acolyte")]  // Khûsar
    public void GetVolunteerTroopId_RhunDragonWrathSettlements_Roll0_ReturnsAcolyte(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Khûndol pool: acolyte(3) + archer(1) + infantry(1) + lancer(1) + darkhun(2) + black_sun(1) + loke(1) = 10
    [DataRow(0, "dragon_wrath_acolyte")]
    [DataRow(2, "dragon_wrath_acolyte")]
    [DataRow(3, "dragon_wrath_archer")]
    [DataRow(4, "dragon_wrath_infantry")]
    [DataRow(5, "dragon_wrath_lancer")]
    [DataRow(6, "darkhun_recruit")]
    [DataRow(7, "darkhun_recruit")]
    [DataRow(8, "black_sun_trainee")]
    [DataRow(9, "loke_rim_initiate")]
    public void GetVolunteerTroopId_Khundol_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_RU7",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("town_RU4",    "balcoth_volunteer")]  // Ûrushban
    [DataRow("castle_RU10", "balcoth_volunteer")]  // Nîrakh
    [DataRow("town_RU3",    "balcoth_volunteer")]  // Vorgavuld
    [DataRow("castle_RU9",  "balcoth_volunteer")]  // Castle RU9
    public void GetVolunteerTroopId_RhunBalcothSettlements_Roll0_ReturnsVolunteer(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Balcoth pool: volunteer(5) + archer(2) + axeman(1) + loke(2) = 10
    [DataRow(0, "balcoth_volunteer")]
    [DataRow(4, "balcoth_volunteer")]
    [DataRow(5, "balcoth_archer")]
    [DataRow(6, "balcoth_archer")]
    [DataRow(7, "balcoth_axeman")]
    [DataRow(8, "loke_rim_initiate")]
    [DataRow(9, "loke_rim_initiate")]
    public void GetVolunteerTroopId_Urushban_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_RU4",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("town_RU5",    "far_rhun_levy")]  // Sârt
    [DataRow("castle_RU11", "far_rhun_levy")]  // Ulbarath
    [DataRow("castle_RU12", "far_rhun_levy")]  // Chêya
    public void GetVolunteerTroopId_RhunFarRhunSettlements_Roll0_ReturnsLevy(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Far-Rhun pool: levy(4) + footman(2) + horseman(3) + loke(1) = 10
    [DataRow(0, "far_rhun_levy")]
    [DataRow(3, "far_rhun_levy")]
    [DataRow(4, "far_rhun_footman")]
    [DataRow(5, "far_rhun_footman")]
    [DataRow(6, "far_rhun_horseman")]
    [DataRow(8, "far_rhun_horseman")]
    [DataRow(9, "loke_rim_initiate")]
    public void GetVolunteerTroopId_Sart_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_RU5",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("castle_RU7", "loke_rim_initiate")]  // Tôrcâin
    [DataRow("castle_RU8", "loke_rim_initiate")]  // Kârashûn
    [DataRow("town_RU6",   "loke_rim_initiate")]  // Kelepar
    [DataRow("castle_RU6", "loke_rim_initiate")]  // Rûartar
    public void GetVolunteerTroopId_RhunWainSettlements_Roll0_ReturnsLokeRimInitiate(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Wain pool: loke(1) + youngblood(5) + glaiveman(2) + wainrider_cavalry(2) = 10
    [DataRow(0, "loke_rim_initiate")]
    [DataRow(1, "wain_youngblood")]
    [DataRow(5, "wain_youngblood")]
    [DataRow(6, "wain_glaiveman")]
    [DataRow(7, "wain_glaiveman")]
    [DataRow(8, "wainrider_cavalry")]
    [DataRow(9, "wainrider_cavalry")]
    public void GetVolunteerTroopId_Torcain_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "castle_RU7",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("town_RU1",   "balcoth_volunteer")]  // Mistrand
    [DataRow("town_RU2",   "balcoth_volunteer")]  // Lest
    [DataRow("castle_RU4", "balcoth_volunteer")]  // Samârnûl
    public void GetVolunteerTroopId_RhunMixedSettlements_Roll0_ReturnsBalcothVolunteer(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Mixed pool: balcoth(1)+blacksun(1)+darkhun(1)+dragon(1)+farrhun(1)+kharaghul(1)+loke(1)+sagarun(1)+wain(2) = 10
    [DataRow(0, "balcoth_volunteer")]
    [DataRow(1, "black_sun_trainee")]
    [DataRow(2, "darkhun_recruit")]
    [DataRow(3, "dragon_wrath_acolyte")]
    [DataRow(4, "far_rhun_levy")]
    [DataRow(5, "kharaghul_youth")]
    [DataRow(6, "loke_rim_initiate")]
    [DataRow(7, "sagarun_deckhand")]
    [DataRow(8, "wain_youngblood")]
    [DataRow(9, "wain_youngblood")]
    public void GetVolunteerTroopId_Mistrand_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_RU1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("town_RU8",   "loke_rim_initiate")]  // Iôrig
    [DataRow("castle_RU5", "loke_rim_initiate")]  // Ulathar
    public void GetVolunteerTroopId_RhunKharaghulSettlements_Roll0_ReturnsLokeRimInitiate(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Kharaghul pool: loke(1) + youth(5) + raider(2) + horse_scout(2) = 10
    [DataRow(0, "loke_rim_initiate")]
    [DataRow(1, "kharaghul_youth")]
    [DataRow(5, "kharaghul_youth")]
    [DataRow(6, "kharaghul_raider")]
    [DataRow(7, "kharaghul_raider")]
    [DataRow(8, "kharaghul_horse_scout")]
    [DataRow(9, "kharaghul_horse_scout")]
    public void GetVolunteerTroopId_Iorig_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_RU8",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    // --- Rhun culture fallback (engine id "khuzait") ---

    [TestMethod]
    public void GetVolunteerTroopId_RhunCulture_Roll0_ReturnsBalcothVolunteer()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("balcoth_volunteer", result);
    }

    [TestMethod]
    // Culture pool: balcoth(1)+blacksun(1)+darkhun(1)+dragon(1)+farrhun(1)+kharaghul(1)+loke(1)+sagarun(1)+wain(2) = 10
    [DataRow(0, "balcoth_volunteer")]
    [DataRow(1, "black_sun_trainee")]
    [DataRow(2, "darkhun_recruit")]
    [DataRow(3, "dragon_wrath_acolyte")]
    [DataRow(4, "far_rhun_levy")]
    [DataRow(5, "kharaghul_youth")]
    [DataRow(6, "loke_rim_initiate")]
    [DataRow(7, "sagarun_deckhand")]
    [DataRow(8, "wain_youngblood")]
    [DataRow(9, "wain_youngblood")]
    public void GetVolunteerTroopId_RhunCulture_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    // --- Rhun village bound-settlement fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_RhunUnknownVillage_InheritsBoundCastle_BalcothPool()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_RU9_1",
            boundSettlementId: "castle_RU9",
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        // castle_RU9 = Balcoth pool, first entry at roll 0
        Assert.AreEqual("balcoth_volunteer", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_RhunVillage_UnlistedBoundSettlement_FallsToKhuzaitCulture()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_unknown_1",
            boundSettlementId: "castle_unknown",
            ownerClanId: null,
            cultureId: "khuzait");

        var result = _sut.GetVolunteerTroopId(context);

        // Falls through SettlementMap (both miss) → ClanMap (null clan) → CultureMap["khuzait"] → balcoth_volunteer @ roll 0
        Assert.AreEqual("balcoth_volunteer", result);
    }

    // --- Conditional pool (Ithil Guard rule for town_ES2) ---

    [TestMethod]
    public void GetVolunteerTroopId_Town_ES2_OwnerCultureMordor_DoesNotReturnIthilGuard()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        // Seed the Ithil conditional pool (mirrors what the JSON loader does in production).
        VolunteerRecruitmentService.AddSettlementConditional(
            "town_ES2_test_mordor",
            ctx => ctx.OwnerCultureId == "gondor",
            ("gondor_ith_watcher", 1),
            ("gondor_ith_veteran", 1));
        try
        {
            var context = new VolunteerContext(
                settlementId: "town_ES2_test_mordor",
                boundSettlementId: null,
                ownerClanId: null,
                cultureId: "mordor",
                ownerCultureId: "mordor");

            var result = _sut.GetVolunteerTroopId(context);

            // Condition fails (mordor owner) → conditional pool skipped → no Gondor pool → no settlement pool →
            // no clan pool → no culture pool for "mordor" → returns null.
            Assert.IsNull(result);
            Assert.AreNotEqual("gondor_ith_watcher", result);
            Assert.AreNotEqual("gondor_ith_veteran", result);
        }
        finally
        {
            VolunteerRecruitmentService.TryRemoveConditionalSettlement("town_ES2_test_mordor");
        }
    }

    [TestMethod]
    public void GetVolunteerTroopId_Town_ES2_OwnerCultureGondor_ReturnsIthilGuard()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        VolunteerRecruitmentService.AddSettlementConditional(
            "town_ES2_test_gondor",
            ctx => ctx.OwnerCultureId == "gondor",
            ("gondor_ith_watcher", 1),
            ("gondor_ith_veteran", 1));
        try
        {
            var context = new VolunteerContext(
                settlementId: "town_ES2_test_gondor",
                boundSettlementId: null,
                ownerClanId: null,
                cultureId: "mordor",
                ownerCultureId: "gondor");

            var result = _sut.GetVolunteerTroopId(context);

            // Condition satisfied → conditional pool used → roll 0 → first entry watcher
            Assert.AreEqual("gondor_ith_watcher", result);
        }
        finally
        {
            VolunteerRecruitmentService.TryRemoveConditionalSettlement("town_ES2_test_gondor");
        }
    }

    [TestMethod]
    public void AddSettlementConditional_NullPredicate_Throws()
    {
        Assert.ThrowsException<System.ArgumentNullException>(() =>
            VolunteerRecruitmentService.AddSettlementConditional(
                "conditional_test_null_predicate",
                condition: null,
                ("any_troop", 1)));
    }

    // --- Gondor JSON loader (Phase E) ---

    [TestMethod]
    public void GondorJsonLoader_MissingFile_NoEntriesAdded_NoThrow()
    {
        var recorder = new List<(string SettlementId, (string troopId, int weight)[] entries)>();
        var conditionalRecorder = new List<string>();

        GondorRecruitmentJsonLoader.LoadFromPath(
            path: "C:/nonexistent/path/that/does/not/exist.json",
            addSettlement: (id, entries) => recorder.Add((id, entries)),
            addSettlementConditional: (id, _, __) => conditionalRecorder.Add(id),
            logger: _logger);

        Assert.AreEqual(0, recorder.Count, "Missing file must not add any settlement entries");
        Assert.AreEqual(0, conditionalRecorder.Count);
    }

    [TestMethod]
    public void GondorJsonLoader_MalformedJson_NoEntriesAdded_NoThrow()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_malformed_{System.Guid.NewGuid():N}.json");
        System.IO.File.WriteAllText(path, "{ this is not valid json :::");
        try
        {
            var recorder = new List<(string, (string, int)[])>();
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (id, entries) => recorder.Add((id, entries)),
                addSettlementConditional: (_, __, ___) => { },
                logger: _logger);
            Assert.AreEqual(0, recorder.Count);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public void GondorJsonLoader_PercentageConvertedToIntegerWeight_PreservesProportion()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_pct_{System.Guid.NewGuid():N}.json");
        System.IO.File.WriteAllText(path, @"{
            ""chance_groups"": [
                {
                    ""description"": ""percentage precision"",
                    ""settlements"": [""test_town_pct""],
                    ""troops"": { ""troop_a"": 33.3333, ""troop_b"": 33.3333, ""troop_c"": 33.3334 }
                }
            ]
        }");
        try
        {
            (string troopId, int weight)[] capturedEntries = null;
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (_, entries) => capturedEntries = entries,
                addSettlementConditional: (_, __, ___) => { },
                logger: _logger);
            Assert.IsNotNull(capturedEntries);
            Assert.AreEqual(3, capturedEntries.Length);
            // 33.3334 * 10000 = 333334; 33.3333 * 10000 = 333333
            Assert.AreEqual(333333, capturedEntries[0].weight);
            Assert.AreEqual(333333, capturedEntries[1].weight);
            Assert.AreEqual(333334, capturedEntries[2].weight);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public void GondorJsonLoader_NaNWeight_SkipsEntryNotEntireGroup()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_nan_{System.Guid.NewGuid():N}.json");
        System.IO.File.WriteAllText(path, @"{
            ""chance_groups"": [
                {
                    ""description"": ""nan rejection"",
                    ""settlements"": [""test_town_nan""],
                    ""troops"": { ""good_troop"": 50.0, ""bad_troop"": ""NaN"" }
                }
            ]
        }");
        try
        {
            (string troopId, int weight)[] capturedEntries = null;
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (_, entries) => capturedEntries = entries,
                addSettlementConditional: (_, __, ___) => { },
                logger: _logger);
            // Bad troop skipped; good_troop entry remains
            Assert.IsNotNull(capturedEntries);
            Assert.AreEqual(1, capturedEntries.Length);
            Assert.AreEqual("good_troop", capturedEntries[0].troopId);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public void GondorJsonLoader_NegativeWeight_SkipsEntry()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_neg_{System.Guid.NewGuid():N}.json");
        System.IO.File.WriteAllText(path, @"{
            ""chance_groups"": [
                {
                    ""description"": ""negative rejection"",
                    ""settlements"": [""test_town_neg""],
                    ""troops"": { ""ok_troop"": 50.0, ""bad_troop"": -5.0 }
                }
            ]
        }");
        try
        {
            (string troopId, int weight)[] capturedEntries = null;
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (_, entries) => capturedEntries = entries,
                addSettlementConditional: (_, __, ___) => { },
                logger: _logger);
            Assert.IsNotNull(capturedEntries);
            Assert.AreEqual(1, capturedEntries.Length);
            Assert.AreEqual("ok_troop", capturedEntries[0].troopId);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public void GondorJsonLoader_RecognisedCondition_RoutesToConditionalAddRather_ThanSettlement()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_cond_{System.Guid.NewGuid():N}.json");
        System.IO.File.WriteAllText(path, @"{
            ""chance_groups"": [
                {
                    ""description"": ""ithil rule"",
                    ""condition"": ""Apply only if town_ES2 is captured by Gondor."",
                    ""settlements"": [""town_ES2""],
                    ""troops"": { ""gondor_ith_watcher"": 50.0, ""gondor_ith_veteran"": 50.0 }
                }
            ]
        }");
        try
        {
            var regular = new List<string>();
            var conditional = new List<string>();
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (id, _) => regular.Add(id),
                addSettlementConditional: (id, _, __) => conditional.Add(id),
                logger: _logger);
            Assert.AreEqual(0, regular.Count, "Conditional group must not call AddSettlement");
            Assert.AreEqual(1, conditional.Count);
            Assert.AreEqual("town_ES2", conditional[0]);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public void GondorJsonLoader_UnrecognisedCondition_SkipsGroup_FailClosed()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_uncond_{System.Guid.NewGuid():N}.json");
        System.IO.File.WriteAllText(path, @"{
            ""chance_groups"": [
                {
                    ""description"": ""mystery rule"",
                    ""condition"": ""something about phases of the moon and prosperity"",
                    ""settlements"": [""town_unknown""],
                    ""troops"": { ""some_troop"": 100.0 }
                }
            ]
        }");
        try
        {
            var regular = new List<string>();
            var conditional = new List<string>();
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (id, _) => regular.Add(id),
                addSettlementConditional: (id, _, __) => conditional.Add(id),
                logger: _logger);
            Assert.AreEqual(0, regular.Count, "Unrecognised condition must NOT silently degrade to non-conditional");
            Assert.AreEqual(0, conditional.Count);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public void GondorJsonLoader_ProductionJsonFile_ParsesAndApplies23Groups()
    {
        // Integration: load the actual gondor.json from the repo and confirm it parses end-to-end.
        // This guards against schema drift between the user's JSON spec and the loader's DTO mapping.
        var repoJsonPath = ResolveRepoJsonPath();
        if (repoJsonPath == null)
        {
            Assert.Inconclusive("Could not locate Main/_Module/ModuleData/recruitment_pools/gondor.json relative to test bin");
            return;
        }

        var settlementsCalled = new List<string>();
        var conditionalsCalled = new List<string>();

        GondorRecruitmentJsonLoader.LoadFromPath(
            path: repoJsonPath,
            addSettlement: (id, _) => settlementsCalled.Add(id),
            addSettlementConditional: (id, _, __) => conditionalsCalled.Add(id),
            logger: _logger);

        // The JSON has 23 chance_groups. Conditional group is "Apply only if town_ES2 is captured by Gondor".
        // Total settlement entries across all non-conditional groups should be well > 23 (groups list multiple
        // settlements each). We assert lower bounds rather than exact counts to be tolerant of future JSON edits.
        Assert.IsTrue(settlementsCalled.Count >= 22, $"Expected at least 22 regular settlement entries, got {settlementsCalled.Count}");
        Assert.AreEqual(1, conditionalsCalled.Count, "Exactly one conditional group (Ithil Guard at town_ES2) is expected");
        Assert.AreEqual("town_ES2", conditionalsCalled[0]);
        Assert.IsTrue(settlementsCalled.Contains("town_EW1"), "Minas Tirith (town_EW1) must be in the JSON pools");
        Assert.IsTrue(settlementsCalled.Contains("town_EW4"), "Pelargir (town_EW4) must be in the JSON pools");
    }

    private static string ResolveRepoJsonPath()
    {
        // Walk up from test bin to find the repo root (contains Main/_Module/ModuleData/)
        var dir = new System.IO.DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "Main", "_Module", "ModuleData", "recruitment_pools", "gondor.json");
            if (System.IO.File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    // --- BuildPool validation ---

    [TestMethod]
    [ExpectedException(typeof(System.ArgumentException))]
    public void BuildPool_EmptyEntries_Throws()
    {
        VolunteerRecruitmentService.BuildPool("owner_id", new (string, int)[0]);
    }

    [TestMethod]
    [ExpectedException(typeof(System.ArgumentException))]
    public void BuildPool_NonPositiveWeight_Throws()
    {
        VolunteerRecruitmentService.BuildPool("owner_id", new[] { ("some_troop", 0) });
    }

    [TestMethod]
    [ExpectedException(typeof(System.ArgumentException))]
    public void BuildPool_NegativeWeight_Throws()
    {
        VolunteerRecruitmentService.BuildPool("owner_id", new[] { ("some_troop", -1) });
    }

    [TestMethod]
    [ExpectedException(typeof(System.ArgumentException))]
    public void BuildPool_BlankTroopId_Throws()
    {
        VolunteerRecruitmentService.BuildPool("owner_id", new[] { ("", 5) });
    }
}
