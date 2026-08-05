using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// Covers the CultureConversion integration into recruitment: the converted-settlement branch in
/// GetVolunteerTroopId (resolve the converted culture's pool, bypassing settlement/clan pools) and the
/// HasCulturePool gate used by the conversion service. Kept separate from the large
/// VolunteerRecruitmentServiceTests file.
/// </summary>
[TestClass]
public class VolunteerRecruitmentConversionTests
{
    private VolunteerRecruitmentService _sut = null!;
    private IRandomProvider _random = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _random = Substitute.For<IRandomProvider>();
        _random.Next(Arg.Any<int>()).Returns(0);
        _logger = Substitute.For<IModLogger>();
        _sut = new VolunteerRecruitmentService(_random, _logger);
    }

    // --- HasCulturePool ---

    [TestMethod]
    [DataRow("gondor")]
    [DataRow("mordor")]
    [DataRow("empire")]   // Dunland (vanilla-reused id)
    [DataRow("sturgia")]  // Dale
    [DataRow("khuzait")]  // Rhûn
    public void HasCulturePool_KnownCulture_ReturnsTrue(string cultureId)
    {
        Assert.IsTrue(_sut.HasCulturePool(cultureId));
    }

    [TestMethod]
    [DataRow("looters")]
    [DataRow("nonexistent_culture")]
    [DataRow("")]
    public void HasCulturePool_UnrecruitableOrUnknown_ReturnsFalse(string cultureId)
    {
        Assert.IsFalse(_sut.HasCulturePool(cultureId));
    }

    // Codex review 2026-06-02 (HIGH): the HasCulturePool gate must recognize every fief-owning playable
    // culture, or that culture's conquests silently never convert. vlandia (Rohan) + aserai (Harad) were
    // missing and are now added. This test pins the full playable set so a future regression is caught.
    [TestMethod]
    [DataRow("gondor")]
    [DataRow("mordor")]
    [DataRow("empire")]    // Dunland
    [DataRow("vlandia")]   // Rohan -- added 2026-06-02
    [DataRow("aserai")]    // Harad -- added 2026-06-02
    [DataRow("khuzait")]   // Rhun
    [DataRow("sturgia")]   // Dale
    [DataRow("erebor")]
    [DataRow("rivendell")]
    [DataRow("lothlorien")]
    [DataRow("isengard")]
    [DataRow("gundabad")]
    [DataRow("dolguldur")]
    [DataRow("shaghana")]
    [DataRow("abanissa")]
    [DataRow("goblin")]
    [DataRow("mistymountainorcs")]
    [DataRow("mirkwood")]  // wired 2026-06-10: mirkwood_recruit culture pool (recruitment-reachability fix)
    [DataRow("umbar")]     // wired 2026-06-10: aux_basic + umbar_elite culture pool (recruitment-reachability fix)
    [DataRow("battania")]  // Variag/Khand -- wired 2026-08-04: shares the Rhun pool (see below)
    public void HasCulturePool_PlayableCultureWithTroops_ReturnsTrue(string cultureId)
    {
        Assert.IsTrue(_sut.HasCulturePool(cultureId),
            $"Playable culture '{cultureId}' must have a recruitment pool so its conquests can convert.");
    }

    // The battania (Variag/Khand) gap closed 2026-08-04. Khand still has no roster of its own, but the
    // K-series settlements were retagged from khuzait to battania so the Variag culture would stop being
    // landless -- vanilla SpawnLordParty calls Settlement.All.First(x => x.Culture == hero.Culture) with
    // no guard, so a landless culture on a homeless lord CTDs the daily clan tick (crash 099f650c). The
    // retag moved those settlements' volunteer lookups onto CultureMap["battania"], which now aliases the
    // Rhun pool -- exactly what Khand recruited before. (mirkwood + umbar were wired 2026-06-10 the same way.)

    [TestMethod]
    [DataRow("vlandia", "rohan_wold_recruit")]
    [DataRow("aserai", "harad_levy")]
    public void GetVolunteerTroopId_ConvertedToNewlyPooledCulture_ResolvesItsTroop(string cultureId, string expectedTroop)
    {
        var context = new VolunteerContext(
            settlementId: "town_EW1", // a Gondor-pool town, now converted away
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: cultureId,
            ownerCultureId: cultureId,
            settlementCultureId: cultureId,
            isConvertedSettlement: true);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual(expectedTroop, result);
        Assert.AreNotEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void HasCulturePool_Null_ReturnsFalse()
    {
        Assert.IsFalse(_sut.HasCulturePool(null));
    }

    // --- Converted-settlement branch ---

    [TestMethod]
    public void GetVolunteerTroopId_Converted_UsesConvertedCulturePool_NotSettlementPool()
    {
        // town_EW1 has a Gondor settlement pool (peasant at roll 0). Once converted to Mordor, recruitment
        // must yield the Mordor culture pool's troop instead, bypassing the stale Gondor settlement pool.
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "mordor",
            ownerCultureId: "mordor",
            settlementCultureId: "mordor",
            isConvertedSettlement: true);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("mordor_uruk_grunt", result);
        Assert.AreNotEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_Converted_BypassesClanPool()
    {
        var context = new VolunteerContext(
            settlementId: null,
            boundSettlementId: null,
            ownerClanId: "clan_empire_west_1", // Gondor clan pool
            cultureId: "mordor",
            ownerCultureId: "mordor",
            settlementCultureId: "mordor",
            isConvertedSettlement: true);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("mordor_uruk_grunt", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_NotConverted_IgnoresSettlementCultureId_UsesStandardCascade()
    {
        // settlementCultureId is set but the settlement is NOT flagged converted → standard cascade wins.
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor",
            ownerCultureId: "gondor",
            settlementCultureId: "mordor",
            isConvertedSettlement: false);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_Converted_NoPoolForConvertedCulture_FallsBackToStandardCascade()
    {
        // Defensive path: if a converted culture somehow has no pool, fall through rather than return null.
        var context = new VolunteerContext(
            settlementId: "town_EW1",
            boundSettlementId: null,
            ownerClanId: null,
            cultureId: "gondor",
            ownerCultureId: "gondor",
            settlementCultureId: "looters", // no CultureMap pool
            isConvertedSettlement: true);

        var result = _sut.GetVolunteerTroopId(context);

        Assert.AreEqual("gondor_ano_peasant", result);
    }
}
