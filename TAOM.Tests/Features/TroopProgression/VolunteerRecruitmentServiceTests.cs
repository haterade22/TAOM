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

    // --- Harad elephant + mûmakil riders: recruitable ONLY by clan_aserai_1 (Ayerikkä) ---
    // Clan pool [harad_levy:7, harad_noble:3, harad_elephant_rider:1, harad_mumakil_rider:1] (total 12):
    // cumulative [7,10,11,12] → roll 10 = elephant rider, roll 11 = mûmakil rider.
    // Per troops.md, the clan pool SHADOWS the aserai culture fallback, so it copies levy/noble too.

    [TestMethod]
    public void GetVolunteerTroopId_ClanAserai1_ElephantBucket_RollsElephantRider()
    {
        _random.Next(12).Returns(10);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_aserai_1",
            cultureId: "aserai");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_elephant_rider", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_ClanAserai1_TopBucket_RollsMumakilRider()
    {
        _random.Next(12).Returns(11);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_aserai_1",
            cultureId: "aserai");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_mumakil_rider", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_ClanAserai1_LowBucket_StillRollsNormalHaradLevy()
    {
        _random.Next(12).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_aserai_1",
            cultureId: "aserai");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_levy", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_AseraiCulture_NoClanPool_NeverRollsElephantRider()
    {
        // Culture fallback pool [levy:7, noble:3] (total 10): top bucket (roll 9) is noble, NOT the rider.
        _random.Next(10).Returns(9);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "aserai");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_noble", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_OtherAseraiClan_FallsToCulture_NeverRollsElephantRider()
    {
        // A different aserai clan has no clan pool -> falls to the culture fallback -> no rider available.
        _random.Next(10).Returns(9);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_aserai_2",
            cultureId: "aserai");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("harad_noble", result);
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

        // town_EW5 = Dol Amroth. Under the 80/20 standard the Belfalas regular line leads the pool and
        // the Swan Knights are the 20% specific share, so roll 0 lands on bel_recruit.
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

        // town_EW5 (Dol Amroth → bel_recruit at roll 0) must win over the clan_empire_west_1 pool
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

        // town_EW5 = Dol Amroth → bel_recruit at roll 0
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
    // town_EW1 (Minas Tirith), mirroring gondor.json: five Anorien troops at 7 (70%), mt_trainee +
    // mt_veteran at 5 (20%), ithilien_ranger at 5 (10%) = total 50.
    // Cumulative: peasant 0-6, archer_militia 7-13, militia 14-20, footman 21-27, skirmisher 28-34,
    // mt_trainee 35-39, mt_veteran 40-44, ranger 45-49.
    [DataRow(0,  "gondor_ano_peasant")]
    [DataRow(6,  "gondor_ano_peasant")]
    [DataRow(7,  "gondor_ano_archer_militia")]
    [DataRow(34, "gondor_ano_skirmisher")]
    [DataRow(35, "gondor_mt_trainee")]
    [DataRow(39, "gondor_mt_trainee")]
    [DataRow(40, "gondor_mt_veteran")]
    [DataRow(45, "gondor_ithilien_ranger")]
    [DataRow(49, "gondor_ithilien_ranger")]
    public void GetVolunteerTroopId_MinasTirith_BoundaryRolls_ReturnExpectedTroop(int roll, string expectedTroopId)
    {
        _random.Next(50).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    // clan_empire_west_5 (Lossarnach) ClanMap pool: lumberman(6) + axebearer(2) + noble(2) = total 10.
    // This is the SAFETY-NET layer. In-game, gondor.json populates SettlementMap for every Gondor
    // settlement and SettlementMap outranks ClanMap in ResolveStandardCascade, so the PRIMARY path for
    // the Lossarnach Noble line is its gondor.json "Bar Melui" (town_EW7) settlement group — the unit
    // harness doesn't load that JSON, so this test exercises the clan fallback that fires when a fief has
    // no settlement pool. Both layers carry the noble (mirrors how every Gondor noble is wired).
    [DataRow(0, "gondor_loss_lumberman")]   // lumberman covers rolls 0..5
    [DataRow(5, "gondor_loss_lumberman")]
    [DataRow(6, "gondor_loss_axebearer")]   // axebearer covers rolls 6..7
    [DataRow(7, "gondor_loss_axebearer")]
    [DataRow(8, "gondor_loss_noble")]       // noble covers rolls 8..9
    [DataRow(9, "gondor_loss_noble")]
    public void GetVolunteerTroopId_LossarnachClan_BoundaryRolls_ReturnExpectedTroop(int roll, string expectedTroopId)
    {
        _random.Next(10).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "unknown_settlement",
            boundSettlementId: null,
            ownerClanId: "clan_empire_west_5",
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    [TestMethod]
    // castle_EW15 / castle_EW16 (Amonost / Erethir) sit in gondor.json's "Hýarthulionath holdings except
    // Methir: Harondor only" group — the Methir noble line is authored for town_EW11 alone, not for these
    // castles. The hand-written pools used to hand them har_conscript(7) + met_noble(3), which the live
    // JSON overrode; they now mirror it: the four Harondor troops at equal weight, total 4.
    [DataRow("castle_EW15", 0, "gondor_har_conscript")]
    [DataRow("castle_EW15", 1, "gondor_har_militia")]
    [DataRow("castle_EW15", 2, "gondor_har_footman")]
    [DataRow("castle_EW15", 3, "gondor_har_skirmisher")]
    [DataRow("castle_EW16", 0, "gondor_har_conscript")]
    [DataRow("castle_EW16", 1, "gondor_har_militia")]
    [DataRow("castle_EW16", 2, "gondor_har_footman")]
    [DataRow("castle_EW16", 3, "gondor_har_skirmisher")]
    public void GetVolunteerTroopId_HyarthulionathCastles_BoundaryRolls_ReturnExpectedTroop(
        string settlementId, int roll, string expectedTroopId)
    {
        _random.Next(4).Returns(roll);
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
    // town_EW2 / town_EW3 (West / East Osgiliath), mirroring gondor.json: five Anorien troops at 7
    // (70%), osg_veteran + osg_skirmisher at 5 (20%), ithilien_ranger at 5 (10%) = total 50.
    // Cumulative: peasant 0-6, archer_militia 7-13, militia 14-20, footman 21-27, skirmisher 28-34,
    // osg_veteran 35-39, osg_skirmisher 40-44, ranger 45-49. The hand-written pool used to omit the
    // ranger entirely while the live JSON offered it at 10%; both layers now carry it.
    [DataRow("town_EW2", 0,  "gondor_ano_peasant")]
    [DataRow("town_EW2", 34, "gondor_ano_skirmisher")]
    [DataRow("town_EW2", 35, "gondor_osg_veteran")]
    [DataRow("town_EW2", 40, "gondor_osg_skirmisher")]
    [DataRow("town_EW2", 45, "gondor_ithilien_ranger")]
    [DataRow("town_EW3", 0,  "gondor_ano_peasant")]
    [DataRow("town_EW3", 34, "gondor_ano_skirmisher")]
    [DataRow("town_EW3", 35, "gondor_osg_veteran")]
    [DataRow("town_EW3", 40, "gondor_osg_skirmisher")]
    [DataRow("town_EW3", 49, "gondor_ithilien_ranger")]
    public void GetVolunteerTroopId_OsgiliathSettlements_BoundaryRolls_ReturnExpectedTroop(
        string settlementId, int roll, string expectedTroopId)
    {
        _random.Next(50).Returns(roll);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroopId, result);
    }

    // --- Specific settlement verifications ---

    [TestMethod]
    // Roll 0 always lands on the first entry, which under the 80/20 standard is the settlement's
    // REGULAR line — the noble / specific line is the trailing 20% share. castle_EW10 moved from a
    // Harondor pool to Belfalas: gondor.json groups it under "Imrazorionath, Garvirionath, and
    // Hirilionath holdings ... Belfalas only", and the hand-written pool now agrees.
    [DataRow("town_EW1",    "gondor_ano_peasant")]
    [DataRow("town_EW4",    "gondor_leb_militia")]
    [DataRow("town_EW5",    "gondor_bel_recruit")]
    [DataRow("town_EW6",    "gondor_anf_levy")]
    [DataRow("town_EW9",    "gondor_lam_clansman")]
    [DataRow("town_EW10",   "gondor_anf_levy")]
    [DataRow("town_EW11",   "gondor_har_conscript")]
    [DataRow("castle_EW3",  "gondor_bel_recruit")]
    [DataRow("castle_EW4",  "gondor_ano_peasant")]
    [DataRow("castle_EW8",  "gondor_pg_volunteer")]
    [DataRow("castle_EW10", "gondor_bel_recruit")]
    [DataRow("castle_EW11", "gondor_bel_recruit")]
    [DataRow("castle_EW9",  "gondor_bel_recruit")]
    [DataRow("castle_EW12", "gondor_bel_recruit")]
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
        // town_DG1 pool (total 18): goblin_slave(7)[0..6] + orc_recruit(4)[7..10] + uruk_foul(2)[11..12]
        // + khamul_shadow_initiate(3)[13..15] + orc_scout(1)[16] + spider(1)[17].
        // Roll 13 lands at the start of the khamul range.
        _random.Next(18).Returns(13);
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
        // Culture pool (total 17): goblin_slave(5)[0..4] + orc_recruit(3)[5..7] + uruk_foul(2)[8..9]
        // + uruk_warrior(3)[10..12] + khamul_shadow_initiate(2)[13..14] + orc_scout(1)[15] + spider(1)[16].
        // Roll 13 lands at the start of the khamul range.
        _random.Next(17).Returns(13);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dg_khamul_shadow_initiate", result);
    }

    // --- Dol Guldur spider recruitment (Patch45_SpiderTroopSpawn) ---

    [TestMethod]
    [DataRow("town_DG1")]
    [DataRow("castle_DG1")]
    [DataRow("castle_DG2")]
    [DataRow("castle_DG3")]
    public void GetVolunteerTroopId_DolGuldurSettlement_MaxRoll_ReturnsSpider(string settlementId)
    {
        // <settlement> pool (total 18): goblin_slave(7) + orc_recruit(4) + uruk_foul(2)
        // + khamul_shadow_initiate(3) + orc_scout(1) + spider(1). Roll 17 lands in the spider range [17,18).
        _random.Next(18).Returns(17);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("taom_spider_creature", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurCulture_MaxRoll_ReturnsSpider()
    {
        // Culture pool (total 17): goblin_slave(5) + orc_recruit(3) + uruk_foul(2) + uruk_warrior(3)
        // + khamul_shadow_initiate(2) + orc_scout(1) + spider(1). Roll 16 lands in the spider range [16,17).
        _random.Next(17).Returns(16);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("taom_spider_creature", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurClanPool_ExcludesSpider()
    {
        // Clan-path pool (total 17): goblin(7) + orc_recruit(4) + uruk_foul(2) + khamul(3) + orc_scout(1)
        // — the spider is intentionally absent (settlement-path only), so clan recruitment never yields it.
        _random.Next(Arg.Any<int>()).Returns(9);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_dolguldur_1",
            cultureId: "dolguldur");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreNotEqual("taom_spider_creature", result);
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
        // town_E1 uses the Erebor+Iron Hills mix (total 18): miner(5)[0..4] + noble(3)[5..7]
        // + iron_hills_reg_recruit(2)[8..9] + iron_hills_noble(2)[10..11] + ironpass_recruit(2)[12..13]
        // + erebor_oathsworn(1)[14] + ironpass_ram_herder(3)[15..17]. Roll 5 still lands in the
        // erebor_noble range: the herder was APPENDED, so nothing below it moved.
        _random.Next(18).Returns(5);
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
        // Culture pool (total 18): miner(5) + noble(3) + iron_hills_reg_recruit(2)[8..9] + iron_hills_noble(2)
        // + ironpass_recruit(2) + erebor_oathsworn(1). Roll 8 lands in iron_hills_reg_recruit range.
        _random.Next(18).Returns(8);
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

    // --- Erebor settlement + clan pools now mix Iron Hills with Erebor ---
    // Mix (weight 12): erebor_reg_miner(5) + erebor_noble(3) + iron_hills_reg_recruit(2) + iron_hills_noble(2).
    // Cumulative: [0..4]=miner, [5..7]=noble, [8..9]=IH recruit, [10..11]=IH noble.

    [TestMethod]
    public void GetVolunteerTroopId_EreborTown_Roll8_ReturnsIronHillsRecruit()
    {
        // town_E1 settlement pool (total 18) includes Iron Hills. Roll 8 → iron_hills_reg_recruit [8..9].
        _random.Next(18).Returns(8);
        var context = new VolunteerContext(
            settlementId: "town_E1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("iron_hills_reg_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_EreborClan_Roll11_ReturnsIronHillsNoble()
    {
        // clan_erebor_1 pool (total 18) includes Iron Hills. Roll 11 → iron_hills_noble [10..11].
        _random.Next(18).Returns(11);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_erebor_1",
            cultureId: "erebor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("iron_hills_noble", result);
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
    // Far-Rhun pool: levy(4) + footman(2) + horseman(3) + loke(1) + horse_master(1) = 11
    // (horse_master appended at index [10]; rolls 0..9 unchanged)
    [DataRow(0, "far_rhun_levy")]
    [DataRow(3, "far_rhun_levy")]
    [DataRow(4, "far_rhun_footman")]
    [DataRow(5, "far_rhun_footman")]
    [DataRow(6, "far_rhun_horseman")]
    [DataRow(8, "far_rhun_horseman")]
    [DataRow(9, "loke_rim_initiate")]
    public void GetVolunteerTroopId_Sart_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(11).Returns(roll);
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
    // Mixed pool: balcoth(1)+blacksun(1)+darkhun(1)+dragon(1)+farrhun(1)+kharaghul(1)+loke(1)+sagarun(1)+wain(2)+easterling(2) = 12
    // (easterling_recruit appended at [10..11]; rolls 0..9 unchanged)
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
    [DataRow(10, "easterling_recruit")]
    public void GetVolunteerTroopId_Mistrand_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(12).Returns(roll);
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
    // Kharaghul pool: loke(1) + youth(5) + raider(2) + horse_scout(2) + horse_master(1) = 11
    // (horse_master appended at index [10]; rolls 0..9 unchanged)
    [DataRow(0, "loke_rim_initiate")]
    [DataRow(1, "kharaghul_youth")]
    [DataRow(5, "kharaghul_youth")]
    [DataRow(6, "kharaghul_raider")]
    [DataRow(7, "kharaghul_raider")]
    [DataRow(8, "kharaghul_horse_scout")]
    [DataRow(9, "kharaghul_horse_scout")]
    public void GetVolunteerTroopId_Iorig_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(11).Returns(roll);
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
    // Culture pool: balcoth(1)+blacksun(1)+darkhun(1)+dragon(1)+farrhun(1)+kharaghul(1)+loke(1)+sagarun(1)+wain(2)+easterling(2) = 12
    // (easterling_recruit appended at [10..11]; rolls 0..9 unchanged)
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
    [DataRow(10, "easterling_recruit")]
    public void GetVolunteerTroopId_RhunCulture_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(12).Returns(roll);
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
        // cultureId="unconfigured_test_culture" ensures the culture fallback is also absent —
        // the test intent is "conditional fails → no fallback → null". Using "mordor" here
        // would now hit the new InitializeMordorCulture pool and obscure the conditional behaviour.
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
                cultureId: "unconfigured_test_culture",
                ownerCultureId: "mordor");

            var result = _sut.GetVolunteerTroopId(context);

            // Condition fails (mordor owner) → conditional pool skipped → no Gondor pool → no settlement pool →
            // no clan pool → no culture pool for unconfigured_test_culture → returns null.
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

    // --- Conditional pools survive CultureConversion (Minas Morgul / town_ES2 fix) ---
    // A conditional pool gates on the LIVE owner culture (OwnerCultureId), not the settlement's original
    // culture, so it must outrank the converted-culture CultureMap fallback once a fief has converted.

    [TestMethod]
    public void GetVolunteerTroopId_Converted_To_Gondor_GondorOwner_ReturnsIthilGuard()
    {
        // A fully culture-converted (Gondor) Minas Morgul that is still Gondor-owned must keep recruiting
        // Ithil Guards. Pre-fix this returned the generic CultureMap["gondor"] (Anorien) pool because the
        // converted branch skipped the conditional check.
        _random.Next(Arg.Any<int>()).Returns(0);
        VolunteerRecruitmentService.AddSettlementConditional(
            "town_ES2_test_converted_gondor",
            ctx => ctx.OwnerCultureId == "gondor",
            ("gondor_ith_watcher", 1),
            ("gondor_ith_veteran", 1));
        try
        {
            var context = new VolunteerContext(
                settlementId: "town_ES2_test_converted_gondor",
                boundSettlementId: null,
                ownerClanId: null,
                cultureId: "mordor",
                ownerCultureId: "gondor",
                settlementCultureId: "gondor",
                isConvertedSettlement: true);

            var result = _sut.GetVolunteerTroopId(context);

            // Conditional satisfied (Gondor owner) → conditional pool used → roll 0 → first entry watcher.
            Assert.AreEqual("gondor_ith_watcher", result);
        }
        finally
        {
            VolunteerRecruitmentService.TryRemoveConditionalSettlement("town_ES2_test_converted_gondor");
        }
    }

    [TestMethod]
    public void GetVolunteerTroopId_Converted_ConditionalPredicateFalse_ReturnsConvertedCulturePool()
    {
        // Converted fief whose conditional predicate is NOT satisfied (non-Gondor owner): the conditional
        // must NOT fire, and resolution falls to the converted-culture pool (CultureMap["gondor"]).
        _random.Next(Arg.Any<int>()).Returns(0);
        VolunteerRecruitmentService.AddSettlementConditional(
            "town_test_converted_nongondor",
            ctx => ctx.OwnerCultureId == "gondor",
            ("gondor_ith_watcher", 1),
            ("gondor_ith_veteran", 1));
        try
        {
            var context = new VolunteerContext(
                settlementId: "town_test_converted_nongondor",
                boundSettlementId: null,
                ownerClanId: null,
                cultureId: "mordor",
                ownerCultureId: "mordor",     // owner is NOT gondor → predicate false
                settlementCultureId: "gondor",
                isConvertedSettlement: true);

            var result = _sut.GetVolunteerTroopId(context);

            // Predicate false → converted-culture fallback (gondor culture pool), not Ithil Guards.
            Assert.AreEqual("gondor_ano_peasant", result);
        }
        finally
        {
            VolunteerRecruitmentService.TryRemoveConditionalSettlement("town_test_converted_nongondor");
        }
    }

    [TestMethod]
    public void GetVolunteerTroopId_Converted_NoConditional_ReturnsConvertedCulturePool()
    {
        // Regression guard: a normal converted fief (no conditional pool) still recruits the converted
        // culture's troops — the common CultureConversion case must be unchanged by the conditional-first fix.
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "town_test_converted_plain",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "gondor",
            settlementCultureId: "gondor",
            isConvertedSettlement: true);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
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
    // Runtime half of the group-total contract. The suite gates the COMMITTED gondor.json, but a player or
    // a hotfix can hand-edit the installed file without ever running a test — and an off-100 group never
    // throws, because PickWeighted normalises cumulatively. That is precisely how one group shipped at
    // 120%. The group must still LOAD (normalisation keeps it playable; dropping it would fall the
    // settlement through to a coarser pool over a cosmetic mistake) but it must WARN.
    public void GondorJsonLoader_GroupTotalNot100_LoadsButWarns()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_total_{System.Guid.NewGuid():N}.json");
        System.IO.File.WriteAllText(path, @"{
            ""chance_groups"": [
                {
                    ""description"": ""over-weighted group"",
                    ""settlements"": [""town_EW7""],
                    ""troops"": { ""gondor_loss_lumberman"": 25.0, ""gondor_loss_woodsman"": 25.0,
                                  ""gondor_loss_axebearer"": 25.0, ""gondor_loss_skirmisher"": 25.0,
                                  ""gondor_loss_noble"": 20.0 }
                }
            ]
        }");
        try
        {
            var applied = new List<string>();
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (id, _) => applied.Add(id),
                addSettlementConditional: (_, __, ___) => { },
                logger: _logger);

            CollectionAssert.AreEqual(new[] { "town_EW7" }, applied,
                "An off-100 group must still register — normalisation keeps it playable");
            _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("120") && m.Contains("not 100")));
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public void GondorJsonLoader_GroupTotalExactly100_DoesNotWarn()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gondor_ok_{System.Guid.NewGuid():N}.json");
        // Uses the 4-dp remainder convention the production file relies on (26.6667/26.6667/26.6666) to
        // prove the 0.01 epsilon absorbs it rather than warning on every well-formed group.
        System.IO.File.WriteAllText(path, @"{
            ""chance_groups"": [
                {
                    ""description"": ""well-formed group"",
                    ""settlements"": [""town_EW9""],
                    ""troops"": { ""gondor_lam_clansman"": 26.6667, ""gondor_lam_footman"": 26.6667,
                                  ""gondor_lam_swordman"": 26.6666, ""gondor_cal_noble"": 10.0,
                                  ""gondor_cal_swordsman"": 10.0 }
                }
            ]
        }");
        try
        {
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: path,
                addSettlement: (_, __) => { },
                addSettlementConditional: (_, __, ___) => { },
                logger: _logger);

            _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("not 100")));
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    // Name carries no group count on purpose — the previous name said 23 while the file held 24, and the
    // assertions are deliberately lower bounds, so a number in the name only goes stale again.
    public void GondorJsonLoader_ProductionJsonFile_ParsesAndAppliesAllGroups()
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

        // The JSON has 24 chance_groups. Conditional group is "Apply only if town_ES2 is captured by Gondor".
        // Total settlement entries across all non-conditional groups is far higher (groups list multiple
        // settlements each — 93 in all). We assert lower bounds rather than exact counts to be tolerant of
        // future JSON edits; exact per-group integrity is covered by
        // GondorJsonLoader_ProductionJson_EveryGroupTotals100AndNoSettlementIsListedTwice.
        Assert.IsTrue(settlementsCalled.Count >= 22, $"Expected at least 22 regular settlement entries, got {settlementsCalled.Count}");
        Assert.AreEqual(1, conditionalsCalled.Count, "Exactly one conditional group (Ithil Guard at town_ES2) is expected");
        Assert.AreEqual("town_ES2", conditionalsCalled[0]);
        Assert.IsTrue(settlementsCalled.Contains("town_EW1"), "Minas Tirith (town_EW1) must be in the JSON pools");
        Assert.IsTrue(settlementsCalled.Contains("town_EW4"), "Pelargir (town_EW4) must be in the JSON pools");
    }

    [TestMethod]
    // Live-behavior regression: the production gondor.json must place gondor_ithilien_ranger at ~10%
    // in the Anórien-front settlements (Minas Tirith, Osgiliath ×2, Cair Andros). The hand-written
    // fallback pools carry the ranger, but the JSON loader OVERWRITES SettlementMap at runtime — so if
    // the JSON drops the ranger it spawns at 0% in-game while the fallback-only unit tests stay green.
    // This test drives the real JSON to guard against that divergence (ranger-dropped-to-0% fix).
    [DataRow("town_EW1")]
    [DataRow("town_EW2")]
    [DataRow("town_EW3")]
    [DataRow("castle_EW4")]
    public void GondorJsonLoader_ProductionJson_PlacesIthilienRangerAtTenPercent(string settlementId)
    {
        var repoJsonPath = ResolveRepoJsonPath();
        if (repoJsonPath == null)
        {
            Assert.Inconclusive("Could not locate Main/_Module/ModuleData/recruitment_pools/gondor.json relative to test bin");
            return;
        }

        var captured = new Dictionary<string, (string troopId, int weight)[]>();
        GondorRecruitmentJsonLoader.LoadFromPath(
            path: repoJsonPath,
            addSettlement: (id, entries) => captured[id] = entries,
            addSettlementConditional: (_, __, ___) => { },
            logger: _logger);

        Assert.IsTrue(captured.ContainsKey(settlementId), $"{settlementId} must be a JSON settlement pool");

        int total = 0;
        int rangerWeight = 0;
        foreach (var (troopId, weight) in captured[settlementId])
        {
            total += weight;
            if (troopId == "gondor_ithilien_ranger")
                rangerWeight += weight;
        }

        Assert.IsTrue(rangerWeight > 0, $"{settlementId} JSON pool must include gondor_ithilien_ranger");
        double share = (double)rangerWeight / total;
        Assert.AreEqual(0.10, share, 1e-4, $"{settlementId}: ranger share {share:P2} should be 10%");
    }

    [TestMethod]
    // Anti-drift guard. gondor.json OVERWRITES SettlementMap in-game, so the hand-written pools in
    // VolunteerRecruitmentService.Gondor.cs are live only in degraded mode (JSON missing/unparseable)
    // — and in these unit tests. The two layers had silently diverged: the C# side stranded the entire
    // 7-troop Ithil Guard line and pooled three ids (anf_guardsman, mt_fountain_guard, ser_pikeman) the
    // JSON never offered, so the suite was asserting behaviour the game never exhibited. A one-time
    // hand-sync that nothing enforces just diverges again, so this pins it.
    //
    // Compares NORMALISED shares, not raw weights: the C# side uses the smallest integers holding each
    // ratio (8/8/8/3/3) while the JSON carries percentages (26.6667/.../10), so the weights differ by
    // construction and only the distribution is meaningful. Villages are excluded — by design the C#
    // layer mirrors the 27 towns/castles only and villages inherit via BoundSettlementId.
    public void GondorPools_HandWrittenFallback_MatchesProductionJson()
    {
        var repoJsonPath = ResolveRepoJsonPath();
        if (repoJsonPath == null)
        {
            Assert.Inconclusive("Could not locate Main/_Module/ModuleData/recruitment_pools/gondor.json relative to test bin");
            return;
        }

        var jsonPools = new Dictionary<string, (string troopId, int weight)[]>();
        var jsonConditional = new HashSet<string>();
        GondorRecruitmentJsonLoader.LoadFromPath(
            path: repoJsonPath,
            addSettlement: (id, entries) => jsonPools[id] = entries,
            addSettlementConditional: (id, _, entries) => { jsonPools[id] = entries; jsonConditional.Add(id); },
            logger: _logger);

        Assert.IsTrue(jsonPools.Count > 0, "Loader produced no pools — gondor.json failed to parse");

        static Dictionary<string, double> Shares(IEnumerable<(string troopId, int weight)> entries)
        {
            var byTroop = new Dictionary<string, double>();
            double total = 0;
            foreach (var (troopId, weight) in entries)
            {
                byTroop[troopId] = byTroop.TryGetValue(troopId, out var prior) ? prior + weight : weight;
                total += weight;
            }
            foreach (var key in new List<string>(byTroop.Keys))
                byTroop[key] /= total;
            return byTroop;
        }

        var mismatches = new List<string>();
        foreach (var kvp in jsonPools)
        {
            var settlementId = kvp.Key;
            // Only the towns/castles are mirrored in C# (see the scope note in Gondor.cs).
            if (settlementId.StartsWith("village_") || settlementId.StartsWith("castle_village_"))
                continue;

            var csharpPool = jsonConditional.Contains(settlementId)
                ? VolunteerRecruitmentService.GetConditionalSettlementPool(settlementId)
                : VolunteerRecruitmentService.GetSettlementPool(settlementId);

            if (csharpPool == null)
            {
                mismatches.Add($"{settlementId}: present in gondor.json, absent from the hand-written pools");
                continue;
            }

            var csharpEntries = new List<(string troopId, int weight)>(csharpPool.Count);
            foreach (var chance in csharpPool)
                csharpEntries.Add((chance.CharacterId, chance.Weight));

            var expected = Shares(kvp.Value);
            var actual = Shares(csharpEntries);

            foreach (var troop in expected.Keys)
                if (!actual.ContainsKey(troop))
                    mismatches.Add($"{settlementId}: JSON offers '{troop}', hand-written pool does not");
            foreach (var troop in actual.Keys)
                if (!expected.ContainsKey(troop))
                    mismatches.Add($"{settlementId}: hand-written pool offers '{troop}', JSON does not");
            foreach (var troop in expected.Keys)
                if (actual.TryGetValue(troop, out var got) && System.Math.Abs(got - expected[troop]) > 1e-4)
                    mismatches.Add($"{settlementId}/{troop}: JSON {expected[troop]:P2} vs hand-written {got:P2}");
        }

        mismatches.Sort();
        Assert.AreEqual(0, mismatches.Count,
            "The hand-written Gondor fallback pools have drifted from gondor.json. Re-sync "
            + "VolunteerRecruitmentService.Gondor.cs (ratios, not raw weights):\n  "
            + string.Join("\n  ", mismatches));
    }

    [TestMethod]
    // Typo gate for the JSON pools. AllPooledTroopIds_ResolveToRealTroops_NoTypos covers the
    // hand-written C# maps only — in the test bin AllPooledTroopIds() never contains a JSON id — and the
    // reachability guard drops unknown ids through an `if (nodes.Contains(...))` filter. So until this
    // test existed, a misspelled troop id in gondor.json passed every check: in-game
    // MBObjectManager.GetObject<CharacterObject> returns null and that troop's whole weight share
    // silently drops out of the pool. This collects ids UNFILTERED, which is the entire point.
    // Same failure class as docs/reviews/rca-rhun-gondor-recruitment-2026-05-23.md (wain_cavalry vs
    // wainrider_cavalry), whose "add a script-level check" follow-up was never built.
    public void GondorJsonLoader_ProductionJson_EveryTroopIdResolvesToARealTroop()
    {
        var repoJsonPath = ResolveRepoJsonPath();
        var troopsDir = ResolveTroopsDir();
        if (repoJsonPath == null || troopsDir == null)
        {
            Assert.Inconclusive("Could not locate gondor.json and/or the troops dir relative to test bin");
            return;
        }

        var (nodes, _) = ParseTroopGraph(troopsDir);

        var jsonIds = new HashSet<string>();
        void Collect(string _, (string troopId, int weight)[] entries)
        {
            foreach (var (troopId, __) in entries)
                jsonIds.Add(troopId);   // NO nodes.Contains filter — an unknown id must survive to be reported
        }

        GondorRecruitmentJsonLoader.LoadFromPath(
            path: repoJsonPath,
            addSettlement: Collect,
            addSettlementConditional: (id, _, entries) => Collect(id, entries),
            logger: _logger);

        Assert.IsTrue(jsonIds.Count > 0, "Loader produced no troop ids — gondor.json failed to parse");

        var missing = new List<string>();
        foreach (var id in jsonIds)
            if (!nodes.Contains(id))
                missing.Add(id);
        missing.Sort();

        Assert.AreEqual(0, missing.Count,
            "gondor.json references troop ids that exist in no troops_*.xml (typos?). These resolve to "
            + "null in-game and their weight share is silently lost:\n  " + string.Join("\n  ", missing));
    }

    [TestMethod]
    // Group integrity: gondor.json's own notes state "Percentages total to 100 per settlement group",
    // but nothing enforced it — the "Bar Melui" group shipped at 120% (4 Lossarnach regulars at 25 plus
    // a 20% noble). PickWeighted normalises cumulatively so an over-100 group never crashes; it just
    // silently delivers a different distribution than the design says. This asserts the contract.
    // A settlement listed in two groups is the sibling failure: the second AddSettlement overwrites the
    // first, so one of the two authored pools vanishes with no warning.
    public void GondorJsonLoader_ProductionJson_EveryGroupTotals100AndNoSettlementIsListedTwice()
    {
        var repoJsonPath = ResolveRepoJsonPath();
        if (repoJsonPath == null)
        {
            Assert.Inconclusive("Could not locate Main/_Module/ModuleData/recruitment_pools/gondor.json relative to test bin");
            return;
        }

        // Percent → weight uses a fixed ×10000 scale, so a group totalling 100% totals 1_000_000 weight.
        const int ExpectedTotalWeight = 100 * 10000;
        var seen = new List<string>();
        var badTotals = new List<string>();

        void Record(string settlementId, (string troopId, int weight)[] entries)
        {
            seen.Add(settlementId);
            int total = 0;
            foreach (var (_, weight) in entries)
                total += weight;
            // Tolerance of 5 absorbs the 4-dp remainder convention (26.6667/26.6667/26.6666), nothing more.
            if (System.Math.Abs(total - ExpectedTotalWeight) > 5)
                badTotals.Add($"{settlementId} totals {total / 10000.0:0.####}%");
        }

        GondorRecruitmentJsonLoader.LoadFromPath(
            path: repoJsonPath,
            addSettlement: Record,
            addSettlementConditional: (id, _, entries) => Record(id, entries),
            logger: _logger);

        Assert.IsTrue(seen.Count > 0, "Loader applied no settlements — gondor.json failed to parse");

        var duplicates = new List<string>();
        var unique = new HashSet<string>();
        foreach (var id in seen)
            if (!unique.Add(id))
                duplicates.Add(id);

        Assert.AreEqual(0, badTotals.Count,
            "Every gondor.json chance_group must total 100% (see the file's own notes):\n  "
            + string.Join("\n  ", badTotals));
        Assert.AreEqual(0, duplicates.Count,
            "These settlements appear in more than one chance_group — the later group silently "
            + "overwrites the earlier one:\n  " + string.Join("\n  ", duplicates));
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

    // --- Mordor settlement pools ---
    // Town pool: mordor_uruk_grunt(1) + mordor_orc_recruit(4) + mordor_orc_impaler(1) + mordor_orc_hunter(1) + mordor_warg_tamer(1) + morannon_recruit(5) = 13
    // Castle pool: same MINUS Black Uruks — orc_recruit(4) + orc_impaler(1) + orc_hunter(1) + warg_tamer(1) + morannon_recruit(4) = 11

    [TestMethod]
    [DataRow("town_ES1", "mordor_uruk_grunt")]  // Danustica
    [DataRow("town_ES2", "mordor_uruk_grunt")]  // Pelgaur — Mordor-owned (default) falls through Ithil Guard conditional
    [DataRow("town_ES3", "mordor_uruk_grunt")]  // Tharbilid
    public void GetVolunteerTroopId_MordorTowns_Roll0_ReturnsBlackUrukGrunt(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Town pool boundary rolls — total weight 13 (grunt weight 1)
    [DataRow(0, "mordor_uruk_grunt")]
    [DataRow(1, "mordor_orc_recruit")]
    [DataRow(4, "mordor_orc_recruit")]
    [DataRow(5, "mordor_orc_impaler")]
    [DataRow(6, "mordor_orc_hunter")]
    [DataRow(7, "mordor_warg_tamer")]
    [DataRow(8, "morannon_recruit")]
    [DataRow(12, "morannon_recruit")]
    public void GetVolunteerTroopId_Danustica_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(13).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "town_ES1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("castle_ES1", "mordor_orc_recruit")]  // The Morannon
    [DataRow("castle_ES2", "mordor_orc_recruit")]  // Carach Angren
    [DataRow("castle_ES3", "mordor_orc_recruit")]  // Cirith Ungol
    [DataRow("castle_ES4", "mordor_orc_recruit")]  // Mornaur
    [DataRow("castle_ES5", "mordor_orc_recruit")]  // Barad Nûrn
    [DataRow("castle_ES6", "mordor_orc_recruit")]  // Cirith Nargil
    [DataRow("castle_ES7", "mordor_orc_recruit")]  // Barad Wath
    [DataRow("castle_ES8", "mordor_orc_recruit")]  // Lûglurag
    public void GetVolunteerTroopId_MordorCastles_Roll0_ReturnsOrcRecruit(
        string settlementId, string expected)
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Castle pool boundary rolls — total weight 11 (no Black Uruks; Morannon weight 4)
    [DataRow(0, "mordor_orc_recruit")]
    [DataRow(3, "mordor_orc_recruit")]
    [DataRow(4, "mordor_orc_impaler")]
    [DataRow(5, "mordor_orc_hunter")]
    [DataRow(6, "mordor_warg_tamer")]
    [DataRow(7, "morannon_recruit")]
    [DataRow(10, "morannon_recruit")]
    public void GetVolunteerTroopId_TheMorannon_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(11).Returns(roll);
        var context = new VolunteerContext(
            settlementId: "castle_ES1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    // Black Uruks are town-only — no roll on any Mordor castle should return them.
    [DataRow("castle_ES1", 0)]
    [DataRow("castle_ES1", 3)]
    [DataRow("castle_ES1", 6)]
    [DataRow("castle_ES5", 0)]
    [DataRow("castle_ES5", 4)]
    [DataRow("castle_ES8", 6)]
    public void GetVolunteerTroopId_MordorCastle_AnyRoll_NeverReturnsBlackUruk(string settlementId, int roll)
    {
        _random.Next(Arg.Any<int>()).Returns(roll);
        var context = new VolunteerContext(
            settlementId: settlementId,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreNotEqual("mordor_uruk_grunt", result, $"castle {settlementId} at roll {roll} returned Black Uruk Grunt — castle pool must exclude all Black Uruks");
    }

    // --- Mordor culture fallback (engine id "mordor", custom TAOM culture) ---

    [TestMethod]
    public void GetVolunteerTroopId_MordorCulture_Roll0_ReturnsBlackUrukGrunt()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("mordor_uruk_grunt", result);
    }

    [TestMethod]
    // Culture pool same as town pool — total weight 13 (grunt weight 1)
    [DataRow(0, "mordor_uruk_grunt")]
    [DataRow(1, "mordor_orc_recruit")]
    [DataRow(4, "mordor_orc_recruit")]
    [DataRow(5, "mordor_orc_impaler")]
    [DataRow(6, "mordor_orc_hunter")]
    [DataRow(7, "mordor_warg_tamer")]
    [DataRow(8, "morannon_recruit")]
    [DataRow(12, "morannon_recruit")]
    public void GetVolunteerTroopId_MordorCulture_BoundaryRolls_ReturnExpectedTroop(int roll, string expected)
    {
        _random.Next(13).Returns(roll);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: null);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expected, result);
    }

    // --- Mordor village bound-settlement fallback ---

    [TestMethod]
    public void GetVolunteerTroopId_MordorVillage_BoundToCastle_InheritsCastlePool_NoBlackUruks()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_ES_unknown",
            boundSettlementId: "castle_ES3",
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        // castle_ES3 = castle pool, roll 0 → orc_recruit (NOT black uruk grunt)
        Assert.AreEqual("mordor_orc_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_MordorVillage_BoundToTown_InheritsTownPool_BlackUrukAvailable()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "village_ES_unknown",
            boundSettlementId: "town_ES1",
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        // town_ES1 = town pool, roll 0 → black uruk grunt
        Assert.AreEqual("mordor_uruk_grunt", result);
    }

    // --- town_ES2 conditional precedence regression ---
    // Existing Ithil Guard conditional on town_ES2 fires when OwnerCultureId == "gondor".
    // After adding Mordor SettlementMap entry for town_ES2, the Mordor-owned default path must
    // fall through to the Mordor town pool — NOT a null return.

    [TestMethod]
    public void GetVolunteerTroopId_TownES2_MordorOwned_FallsThroughToMordorTownPool()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "town_ES2",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor");

        var result = _sut.GetVolunteerTroopId(context);

        // Ithil Guard conditional predicate fails (mordor owner) → falls through to Mordor town pool
        // → roll 0 → Black Uruk Grunt (first entry, weight 1)
        Assert.AreEqual("mordor_uruk_grunt", result);
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

    // --- Dale (Sturgia culture) recruitment pool ---
    // Pool weights (total 10): dale_squire(4) + dale_riverman(1) + dale_man_at_arms(1)
    //   + dale_bowman(1) + dale_crossbowman(1) + dale_outrider(1) + dale_recruit(1)
    // Cumulative ranges: [0..3]=squire, [4]=riverman, [5]=man_at_arms, [6]=bowman,
    //   [7]=crossbowman, [8]=outrider, [9]=recruit.

    [TestMethod]
    public void GetVolunteerTroopId_DaleCulture_LowRoll_ReturnsDalianLevy()
    {
        // Roll 0 lands in the Dalian Levy bucket (weight 4 — the most common recruit).
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "sturgia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dale_squire", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DaleCulture_MidRoll_ReturnsRiverman()
    {
        // Roll 4 lands at the start of the riverman bucket (squire covers 0..3, riverman is at 4).
        _random.Next(10).Returns(4);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "sturgia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dale_riverman", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DaleCulture_HighRoll_ReturnsLakeTownPeasant()
    {
        // Roll 9 is the last index — Lake-Town Peasant (final weight-1 entry).
        _random.Next(10).Returns(9);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "sturgia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dale_recruit", result);
    }

    // --- Lake-Town (town_S1) settlement-specific pool ---
    // Pool weights: dale_recruit(9) + dale_squire(1) = 10. Rolls [0..8] = recruit, [9] = squire.

    [TestMethod]
    public void GetVolunteerTroopId_TownS1_LakeTown_LowRoll_ReturnsLakeTownPeasant()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "town_S1",  // Lake-Town
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "sturgia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dale_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_TownS1_LakeTown_HighRoll_ReturnsDalianLevy()
    {
        // Roll 9 is the rare Dalian Levy slot (weight 1 of 10).
        _random.Next(10).Returns(9);
        var context = new VolunteerContext(
            settlementId: "town_S1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "sturgia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dale_squire", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_OtherDaleSettlement_NoSettlementPool_FallsThroughToCulture()
    {
        // A non-S1 Dale settlement (e.g. town_S2) must fall through to the culture pool.
        // Confirms the Lake-Town override is town_S1-only.
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: "town_S2",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "sturgia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dale_squire", result);
    }

    // --- Rohan (vlandia) clan recruitment pool ---
    // Per InitializeRohanClans: every clan_vlandia_1..11 maps to the 7 Rohan basic troops
    // at weight 1 each. Total weight 7; rolls 0..6 map directly to ordered slot index.

    [TestMethod]
    public void GetVolunteerTroopId_RohanClan_Roll0_ReturnsWoldRecruit()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_vlandia_1",
            cultureId: "vlandia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("rohan_wold_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_RohanClan_Roll6_ReturnsEdorasRecruit()
    {
        // Roll 6 = last slot in the ordered weight-1 pool = rohan_edoras_recruit
        _random.Next(7).Returns(6);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_vlandia_5",
            cultureId: "vlandia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("rohan_edoras_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_RohanClan_HighestNumberedClan_HasSamePool()
    {
        // Confirms clan_vlandia_11 (the highest-numbered Rohan clan) has the pool too —
        // catches off-by-one regressions in the loop range.
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_vlandia_11",
            cultureId: "vlandia");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("rohan_wold_recruit", result);
    }

    // --- Isengard (Uruk-Hai) culture recruitment pool ---
    // Pool weights (total 14): urukhai_recruit(4) + urukhai_skirmisher(2) + orc_warg_scout(2)
    //   + urukhai_warrior(1) + urukhai_scout(1) + isengard_orc_grunt(3) + orthanc_chosen(1).
    // Cumulative: [0..3]=recruit, [4..5]=skirmisher, [6..7]=warg_scout, [8]=warrior, [9]=scout,
    //   [10..12]=orc_grunt, [13]=orthanc_chosen. (orc_grunt + orthanc_chosen appended as
    //   reachability fixes for the isengard_orc_* and Orthanc Guard lines; rolls 0..9 unchanged.)

    [TestMethod]
    public void GetVolunteerTroopId_IsengardCulture_LowRoll_ReturnsRecruit()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "isengard");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("urukhai_recruit", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_IsengardCulture_MidRoll_ReturnsSkirmisher()
    {
        // Roll 4 lands at the start of the skirmisher bucket (recruit covers 0..3). Pool total now 14.
        _random.Next(14).Returns(4);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "isengard");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("urukhai_skirmisher", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_IsengardCulture_Roll9_ReturnsScout()
    {
        // Roll 9 = urukhai_scout (the bow-line entry). Pool total now 14; scout sits at [9],
        // with the appended orc_grunt[10..12] + orthanc_chosen[13] reachability fixes after it.
        _random.Next(14).Returns(9);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "isengard");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("urukhai_scout", result);
    }

    // --- Dunland (Culture.empire) recruitment pools ---
    // Culture pool: dunland_peasant + noble_son + boar_noble_son + raven_noble_son, weight 1 each (total 4).
    // Totem clans: peasant + their own noble son (total 2). Other clans: full roster (total 4).

    [TestMethod]
    public void GetVolunteerTroopId_DunlandCulture_LowRoll_ReturnsPeasant()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "empire");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dunland_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DunlandCulture_HighRoll_ReturnsRavenNobleSon()
    {
        // Roll 3 = last entry (raven noble son) in the weight-1×4 culture pool.
        _random.Next(4).Returns(3);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "empire");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dunland_raven_noble_son", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DunlandWolfClan_HighRoll_ReturnsWolfNobleSon()
    {
        // clan_empire_north_1 (Blaidd/Wolf) pool: peasant(0) + noble_son(1). Roll 1 → wolf noble son.
        _random.Next(2).Returns(1);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_empire_north_1",
            cultureId: "empire");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dunland_noble_son", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DunlandBoarClan_HighRoll_ReturnsBoarNobleSon()
    {
        // clan_empire_north_2 (Turch/Boar) pool: peasant(0) + boar_noble_son(1). Roll 1 → boar.
        _random.Next(2).Returns(1);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_empire_north_2",
            cultureId: "empire");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dunland_boar_noble_son", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_DunlandNonTotemClan_HasFullRoster()
    {
        // clan_empire_north_4 (Arth/Bear) has no signature noble son → full roster (4 entries).
        // Roll 3 = last entry (raven noble son), confirming the full roster is wired.
        _random.Next(4).Returns(3);
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_empire_north_4",
            cultureId: "empire");

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("dunland_raven_noble_son", result);
    }

    // --- New factions: Goblins, Misty Mountain Orcs, Lindon (rivendell culture) ---
    // Goblin / MMO pools: snaga(7)[0..6] + grunt(2)[7..8] + fighter(1)[9] + hunter(2)[10..11] = total 12
    // (hunter appended as a reachability fix for the orphaned archer line; rolls 0..9 unchanged).

    [TestMethod]
    public void GetVolunteerTroopId_GoblinCulture_LowRoll_ReturnsSnaga()
    {
        _random.Next(12).Returns(0);
        var context = new VolunteerContext(null, null, null, "goblin");
        Assert.AreEqual("goblin_snaga", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_GoblinCulture_HighRoll_ReturnsFighter()
    {
        _random.Next(12).Returns(9);
        var context = new VolunteerContext(null, null, null, "goblin");
        Assert.AreEqual("goblin_fighter", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_MistyMountainOrcsCulture_LowRoll_ReturnsSnaga()
    {
        _random.Next(12).Returns(0);
        var context = new VolunteerContext(null, null, null, "mistymountainorcs");
        Assert.AreEqual("goblin_snaga", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_MistyMountainOrcsCulture_HighRoll_ReturnsFighter()
    {
        _random.Next(12).Returns(9);
        var context = new VolunteerContext(null, null, null, "mistymountainorcs");
        Assert.AreEqual("goblin_fighter", _sut.GetVolunteerTroopId(context));
    }

    // rivendell pool: imladris_recruit(5)[0..4]/imladris_infantry(3)[5..7]/imladris_bowman(2)[8..9]
    //   + rivendell_noble(1)[10] + rivendell_knight_golden_flower(1)[11] = total 12.
    // (the two named-elite line entries appended as reachability fixes; rolls 0..9 unchanged.)
    // Serves both the Rivendell kingdom and the new Lindon kingdom (both Culture.rivendell).
    [TestMethod]
    public void GetVolunteerTroopId_RivendellCulture_LowRoll_ReturnsRecruit()
    {
        _random.Next(12).Returns(0);
        var context = new VolunteerContext(null, null, null, "rivendell");
        Assert.AreEqual("imladris_recruit", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_RivendellCulture_HighRoll_ReturnsBowman()
    {
        _random.Next(12).Returns(9);
        var context = new VolunteerContext(null, null, null, "rivendell");
        Assert.AreEqual("imladris_bowman", _sut.GetVolunteerTroopId(context));
    }

    // --- Reachability guard (every troop is accounted for) ---
    // A LOTR troop line is only recruitable if one of its troops is injected into a pool, OR a pooled
    // troop upgrades into it. This test parses the upgrade graph from every troops_*.xml, floods from the
    // union of all pool roots (AllPooledTroopIds), and asserts the ONLY unreachable troops are the
    // intentionally non-recruited ones: settlement militia (*_militia_*), bandit-hideout bosses (*_boss),
    // tavern mercenaries (*_merc — hired for gold through <basic_mercenary_troops>, guarded instead by
    // TavernMercenaryDataTests), and the cave_troll monster (deferred — needs spider-style spawn support
    // before it's safe to recruit).
    // A future orphaned line then fails the build here instead of silently becoming unrecruitable in-game.

    [TestMethod]
    public void AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot()
    {
        var troopsDir = ResolveTroopsDir();
        if (troopsDir == null)
        {
            Assert.Inconclusive("Could not locate Main/_Module/ModuleData/troops relative to test bin");
            return;
        }

        var (nodes, upgrades) = ParseTroopGraph(troopsDir);

        // Roots = every troop id any pool can offer.
        var roots = new HashSet<string>();
        foreach (var id in VolunteerRecruitmentService.AllPooledTroopIds())
            if (nodes.Contains(id))
                roots.Add(id);

        // The Gondor JSON pools — including the conditional Ithil Guard line (gondor_ith_*) — load at
        // runtime in-game but NOT in the test bin (the auto-loader resolves a game-relative path that
        // doesn't exist here, leaving only the hand-written fallback pools). Seed those roots from the
        // production gondor.json via the real loader so the graph matches the live root set. No static
        // state is mutated — the delegates only record troop ids locally.
        var jsonPath = ResolveRepoJsonPath();
        if (jsonPath != null)
        {
            GondorRecruitmentJsonLoader.LoadFromPath(
                path: jsonPath,
                addSettlement: (_, entries) => { foreach (var e in entries) if (nodes.Contains(e.Item1)) roots.Add(e.Item1); },
                addSettlementConditional: (_, __, entries) => { foreach (var e in entries) if (nodes.Contains(e.Item1)) roots.Add(e.Item1); },
                logger: _logger);
        }

        var reachable = new HashSet<string>();
        var stack = new Stack<string>(roots);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (!reachable.Add(n)) continue;
            if (upgrades.TryGetValue(n, out var kids))
                foreach (var k in kids)
                    if (!reachable.Contains(k)) stack.Push(k);
        }

        var gaps = new List<string>();
        foreach (var id in nodes)
            if (!reachable.Contains(id) && !IsIntentionallyUnrecruited(id))
                gaps.Add(id);
        gaps.Sort();

        Assert.AreEqual(0, gaps.Count,
            "These troops are fielded by AI lords but cannot be recruited or upgraded into from any pool " +
            "root. Add their line-entry troop to VolunteerRecruitmentService, or (if intentionally AI-only) " +
            "extend IsIntentionallyUnrecruited:\n  " + string.Join("\n  ", gaps));
    }

    [TestMethod]
    public void AllPooledTroopIds_ResolveToRealTroops_NoTypos()
    {
        var troopsDir = ResolveTroopsDir();
        if (troopsDir == null) { Assert.Inconclusive("troops dir not found"); return; }
        var (nodes, _) = ParseTroopGraph(troopsDir);

        var missing = new List<string>();
        foreach (var id in VolunteerRecruitmentService.AllPooledTroopIds())
            // taom_spider_creature is a roster anchor defined in characters/, not a troops_*.xml row.
            if (id != "taom_spider_creature" && !nodes.Contains(id))
                missing.Add(id);
        missing.Sort();

        Assert.AreEqual(0, missing.Count,
            "Pool references troop ids absent from every troops_*.xml (typos?):\n  " + string.Join("\n  ", missing));
    }

    private static bool IsIntentionallyUnrecruited(string troopId)
        => troopId.Contains("_militia_")          // settlement militia — spawned, not recruited
           || troopId.EndsWith("_boss")           // bandit-hideout bosses
           || troopId.EndsWith("_merc")           // tavern mercenaries — hired for gold, not volunteered
           || troopId == "cave_troll"             // non-humanoid monster; deferred pending spider-style spawn support
           || BorrowedCultureCapstones.Contains(troopId)
           || BlackNumenoreanLine.Contains(troopId);

    // Blue Craig and the Misty Mountain Orcs field the shared goblin tree. Each kept exactly one
    // bespoke top-tier troop so its elite slot stays its own, the same shape Umbar keeps umbar_elite
    // against Harad's tree. Neither is AI-only: taom_partyTemplates.xml grants each one through its
    // culture's vassal reward (vassal_reward_troops_bluecraig, vassal_reward_troops_mistymountainorcs),
    // and DefaultVassalRewardsModel drops every stack in that template into the joining player's own
    // roster. Prisoner recruitment out of a lord party is the second player-facing route.
    //
    // What they are NOT is upgrade-reachable, and that is deliberate. The only troop that could
    // upgrade into them is the shared goblin_chosen_of_tharzog, and adding them there would let a
    // Goblin-town player promote a Mountain Guard into another kingdom's signature unit, which is
    // the exact distinction these two troops exist to draw.
    //
    // An EXPLICIT SET, not a StartsWith, for the reason spelled out under BlackNumenoreanLine: a
    // prefix exempts an unbounded namespace, so the next orphan would pass the guard silently.
    private static readonly System.Collections.Generic.HashSet<string> BorrowedCultureCapstones =
        new System.Collections.Generic.HashSet<string>
        {
            "bluecraig_bolgs_ironfang",
            "mistymountainorcs_bolgs_ironfang",
        };

    // The Black Numenorean line is not offered by any VOLUNTEER POOL, which is all this test
    // measures. It is NOT "AI-only": taom_partyTemplates.xml grants mordor_num_vet_infantry as a
    // Mordor vassal reward, and DefaultVassalRewardsModel adds every stack in that template
    // straight into the joining player's own troop roster. Prisoner recruitment is a second
    // player-facing route, and both then walk the upgrade tree normally.
    //
    // Mordor's elite_basic_troop stays mordor_uruk_warrior and VolunteerRecruitmentService.Mordor.cs
    // gains no entry, so no notable ever offers an Initiate. Recruiting Sauron's human nobility from
    // a village notable would be wrong on its own terms.
    //
    // This is an EXPLICIT SET, not a StartsWith("mordor_num_") prefix. A prefix exempts an unbounded
    // namespace, so a future Black Numenorean troop that was accidentally orphaned from the upgrade
    // graph would silently pass the very guard that exists to catch it. Adding a troop to the line
    // means adding it here deliberately.
    //
    // If the line is ever made recruitable, delete this set rather than extending it, and add
    // mordor_num_initiate to VolunteerRecruitmentService.Mordor.cs.
    private static readonly System.Collections.Generic.HashSet<string> BlackNumenoreanLine =
        new System.Collections.Generic.HashSet<string>
        {
            "mordor_num_initiate",
            "mordor_num_cavalry", "mordor_num_vet_cavalry", "mordor_num_knight", "mordor_num_temple_knight",
            "mordor_num_infantry", "mordor_num_vet_infantry", "mordor_num_warden", "mordor_num_temple_guard",
            "mordor_num_archer", "mordor_num_vet_archer", "mordor_num_marksman", "mordor_num_shadowbow",
        };

    [TestMethod]
    public void BlackNumenoreanExemptionSet_MatchesTheTroopsActuallyDefined()
    {
        // Guards the explicit set above against drift in both directions: a new mordor_num_* troop
        // that nobody added here fails the reachability test with a useful message, and an id left
        // here after a rename silently exempts nothing.
        var troopsDir = ResolveTroopsDir();
        if (troopsDir == null)
        {
            Assert.Inconclusive("Could not locate Main/_Module/ModuleData/troops relative to test bin");
            return;
        }
        var (nodes, _) = ParseTroopGraph(troopsDir);
        var defined = new System.Collections.Generic.HashSet<string>(
            System.Linq.Enumerable.Where(nodes, n => n.StartsWith("mordor_num_")));

        CollectionAssert.AreEquivalent(
            System.Linq.Enumerable.ToList(BlackNumenoreanLine),
            System.Linq.Enumerable.ToList(defined),
            "BlackNumenoreanLine must list exactly the mordor_num_* troops defined in troops_mordor.xml");
    }

    private static string ResolveTroopsDir()
    {
        var dir = new System.IO.DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "Main", "_Module", "ModuleData", "troops");
            if (System.IO.Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static (HashSet<string> nodes, Dictionary<string, List<string>> upgrades) ParseTroopGraph(string troopsDir)
    {
        var nodes = new HashSet<string>();
        var upgrades = new Dictionary<string, List<string>>();
        foreach (var file in System.IO.Directory.GetFiles(troopsDir, "troops_*.xml"))
        {
            var doc = System.Xml.Linq.XDocument.Load(file);
            foreach (var npc in doc.Descendants("NPCCharacter"))
            {
                var id = (string)npc.Attribute("id");
                if (string.IsNullOrEmpty(id)) continue;
                nodes.Add(id);
                var kids = new List<string>();
                foreach (var ut in npc.Descendants("upgrade_target"))
                {
                    var tid = (string)ut.Attribute("id");
                    if (string.IsNullOrEmpty(tid)) continue;
                    const string prefix = "NPCCharacter.";
                    if (tid.StartsWith(prefix)) tid = tid.Substring(prefix.Length);
                    kids.Add(tid);
                }
                if (kids.Count > 0) upgrades[id] = kids;
            }
        }
        return (nodes, upgrades);
    }

    // --- Reachability fixes: user-reported lines now recruitable ---

    [TestMethod]
    public void GetVolunteerTroopId_GundabadCulture_Roll10_ReturnsHunter_ArcherLineEntry()
    {
        // Pool (total 13): snaga(7)[0..6] grunt(2)[7..8] fighter(1)[9] hunter(2)[10..11] scout(1)[12].
        _random.Next(13).Returns(10);
        var context = new VolunteerContext(null, null, null, "gundabad");
        Assert.AreEqual("gundabad_hunter", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_GundabadCulture_Roll12_ReturnsScout_HorseArcherLineEntry()
    {
        _random.Next(13).Returns(12);
        var context = new VolunteerContext(null, null, null, "gundabad");
        Assert.AreEqual("gundabad_scout", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_IsengardCulture_Roll10_ReturnsOrcGrunt_OrcLineEntry()
    {
        _random.Next(14).Returns(10);
        var context = new VolunteerContext(null, null, null, "isengard");
        Assert.AreEqual("isengard_orc_grunt", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_IsengardCulture_Roll13_ReturnsOrthancChosen_EliteLineEntry()
    {
        _random.Next(14).Returns(13);
        var context = new VolunteerContext(null, null, null, "isengard");
        Assert.AreEqual("orthanc_chosen", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurSettlement_Roll7_ReturnsOrcRecruit()
    {
        // town_DG1 (18): goblin(7)[0..6] orc_recruit(4)[7..10] uruk_foul(2)[11..12] khamul(3)[13..15] orc_scout(1)[16] spider(1)[17]
        _random.Next(18).Returns(7);
        var context = new VolunteerContext("town_DG1", null, null, "dolguldur");
        Assert.AreEqual("dg_orc_recruit", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurSettlement_Roll11_ReturnsUrukFoul()
    {
        _random.Next(18).Returns(11);
        var context = new VolunteerContext("town_DG1", null, null, "dolguldur");
        Assert.AreEqual("dg_uruk_foul", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_DolGuldurClan_Roll7_ReturnsOrcRecruit()
    {
        // Clan pool (17): goblin(7)[0..6] orc_recruit(4)[7..10] uruk_foul(2)[11..12] khamul(3)[13..15] orc_scout(1)[16]
        _random.Next(17).Returns(7);
        var context = new VolunteerContext(null, null, "clan_dolguldur_1", "dolguldur");
        Assert.AreEqual("dg_orc_recruit", _sut.GetVolunteerTroopId(context));
    }

    // --- Newly-wired cultures (previously recruited nothing) ---

    [TestMethod]
    public void GetVolunteerTroopId_MirkwoodCulture_ReturnsMirkwoodRecruit()
    {
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(null, null, null, "mirkwood");
        Assert.AreEqual("mirkwood_recruit", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_UmbarCulture_Roll0_ReturnsAuxBasic()
    {
        // umbar pool: aux_basic(7)[0..6] + umbar_elite(3)[7..9] = total 10
        _random.Next(Arg.Any<int>()).Returns(0);
        var context = new VolunteerContext(null, null, null, "umbar");
        Assert.AreEqual("aux_basic", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_UmbarCulture_Roll7_ReturnsUmbarElite()
    {
        _random.Next(10).Returns(7);
        var context = new VolunteerContext(null, null, null, "umbar");
        Assert.AreEqual("umbar_elite", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void HasCulturePool_MirkwoodAndUmbar_NowTrue()
    {
        // Both were absent before this fix — making them valid CultureConversion targets now.
        Assert.IsTrue(_sut.HasCulturePool("mirkwood"));
        Assert.IsTrue(_sut.HasCulturePool("umbar"));
    }

    // --- Erebor ram-cavalry branch (#515): reached by upgrade, never offered as a volunteer ---

    // The five ARMOURED Ironpass ram troops. None of them is pooled, and none of them may become
    // pooled: the branch is entered through ironpass_ram_herder (16), an is_basic_troop root that
    // IS pooled and upgrades into ironpass_ram_rider (21). A player who wants rams therefore starts
    // at the bottom of a branch instead of being handed an armoured level-21 rider by a village
    // notable, which was #515's actual objection. The ironpass_warrior -> ironpass_ram_rider edge
    // still exists too, so the foot line also reaches the rams.
    //
    // Precedent for the shape: Rohan is a cavalry culture and pools only its seven is_basic_troop
    // recruits, reaching every horseman by upgrade.
    private static readonly string[] RamCavalryLine =
    {
        "ironpass_ram_rider",
        "ironpass_goat_charger",
        "ironpass_ram_breaker",
        "ironpass_ram_vanguard",
        "ironpass_ram_marshal",
    };

    [TestMethod]
    public void EreborRamCavalry_IsNotOfferedByAnyVolunteerPool()
    {
        var pooled = new HashSet<string>(VolunteerRecruitmentService.AllPooledTroopIds());

        foreach (var id in RamCavalryLine)
            Assert.IsFalse(pooled.Contains(id),
                id + " is an armoured mid-branch mounted troop and must stay upgrade-only. The "
                   + "branch is entered through the pooled ironpass_ram_herder (16). If you "
                   + "deliberately want this one recruitable too, add it to BOTH EreborMix and "
                   + "CultureMap[\"erebor\"] and retune every Next(18) stub in this file, then "
                   + "delete this assertion.");
    }

    [TestMethod]
    public void GetVolunteerTroopId_EreborCulture_HighestRoll_ReturnsRamHerder()
    {
        // ironpass_ram_herder(3) occupies the top of the Erebor culture pool at [15..17]. This is
        // the entry point that makes the whole war-ram branch reachable from a notable; without it
        // rams appear at no notable anywhere, which is the state players reported.
        _random.Next(18).Returns(15);
        var context = new VolunteerContext(null, null, null, "erebor");

        Assert.AreEqual("ironpass_ram_herder", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_EreborSettlement_HighestRoll_ReturnsRamHerder()
    {
        // Same band in EreborMix, which every Erebor town, castle and clan uses. Roll 17 is the
        // last index of the herder's range, so this also pins the pool's upper bound.
        _random.Next(18).Returns(17);
        var context = new VolunteerContext("town_E1", null, null, "erebor");

        Assert.AreEqual("ironpass_ram_herder", _sut.GetVolunteerTroopId(context));
    }

    [TestMethod]
    public void GetVolunteerTroopId_EreborCulture_RollsAgainstTotalWeightEighteen()
    {
        // Pins the Erebor culture pool's total weight at 18. PickWeighted rolls
        // _random.Next(totalWeight) and every Erebor test in this file stubs a literal Next(18).
        // Move the pool and those stubs stop matching: NSubstitute hands back the default 0 and
        // each of them silently starts asserting the FIRST pool entry instead of the one it names.
        // rca-mumakil-2026-06-29 records that failure mode. This test fails loudly instead.
        _random.Next(18).Returns(14);
        var context = new VolunteerContext(null, null, null, "erebor");

        Assert.AreEqual("erebor_oathsworn", _sut.GetVolunteerTroopId(context));
        _random.Received().Next(18);
    }

    [TestMethod]
    public void GetVolunteerTroopId_EreborSettlement_RollsAgainstTotalWeightEighteen()
    {
        // Same guard for the settlement/clan mix (EreborMix), which the culture pool mirrors.
        _random.Next(18).Returns(14);
        var context = new VolunteerContext("town_E1", null, null, "erebor");

        Assert.AreEqual("erebor_oathsworn", _sut.GetVolunteerTroopId(context));
        _random.Received().Next(18);
    }

    [TestMethod]
    public void EreborRamCavalry_IsReachableFromAPooledRoot()
    {
        // The companion to EreborRamCavalry_IsNotOfferedByAnyVolunteerPool: leaving the branch out
        // of every pool is only correct while an upgrade edge reaches it. Detach the branch from
        // ironpass_warrior and this fails, naming the ids that fell off the graph.
        var troopsDir = ResolveTroopsDir();
        if (troopsDir == null)
        {
            Assert.Inconclusive("Could not locate Main/_Module/ModuleData/troops relative to test bin");
            return;
        }

        var (nodes, upgrades) = ParseTroopGraph(troopsDir);
        if (!nodes.Contains(RamCavalryLine[0]))
        {
            // troops_erebor.xml is owned by a parallel worker on #515; until the branch lands there
            // is no graph to check. The repo-wide AllNonMilitiaNonBossTroops_AreReachable... guard
            // covers it from the moment it does.
            Assert.Inconclusive("ram-cavalry troops are not defined in troops_erebor.xml yet");
            return;
        }

        var reachable = new HashSet<string>();
        var stack = new Stack<string>();
        foreach (var id in VolunteerRecruitmentService.AllPooledTroopIds())
            if (nodes.Contains(id))
                stack.Push(id);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (!reachable.Add(n)) continue;
            if (upgrades.TryGetValue(n, out var kids))
                foreach (var k in kids)
                    if (!reachable.Contains(k)) stack.Push(k);
        }

        var gaps = new List<string>();
        foreach (var id in RamCavalryLine)
            if (!reachable.Contains(id))
                gaps.Add(id);

        Assert.AreEqual(0, gaps.Count,
            "Ram-cavalry troops unreachable from every volunteer pool root. Either restore the "
            + "ironpass_warrior upgrade edge or pool the branch entry: " + string.Join(", ", gaps));
    }
}
