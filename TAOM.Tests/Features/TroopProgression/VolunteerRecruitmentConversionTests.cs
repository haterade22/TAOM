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
    public void HasCulturePool_PlayableCultureWithTroops_ReturnsTrue(string cultureId)
    {
        Assert.IsTrue(_sut.HasCulturePool(cultureId),
            $"Playable culture '{cultureId}' must have a recruitment pool so its conquests can convert.");
    }

    // KNOWN GAP (documented, not a bug): battania (Khand), mirkwood, and umbar are fief-owning kingdoms but
    // have no recruitable basic-troop set authored (no troops_khand.xml; mirkwood/umbar troops carry no
    // is_basic_troop). Conversion to these is intentionally gated off until their recruitment pools exist.
    // If/when pools are authored, move these rows up to the covered test above.
    [TestMethod]
    [DataRow("battania")]  // Khand -- no troop set authored
    [DataRow("mirkwood")]  // troops exist but none flagged is_basic_troop
    [DataRow("umbar")]     // only boss/elite troops authored
    public void HasCulturePool_PlayableCultureWithoutTroopSet_ReturnsFalse_KnownGap(string cultureId)
    {
        Assert.IsFalse(_sut.HasCulturePool(cultureId),
            $"'{cultureId}' has no recruitable troop set yet -- conversion is intentionally gated off (known gap).");
    }

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
