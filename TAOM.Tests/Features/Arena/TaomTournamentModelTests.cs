using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arena.Models;

namespace TAOM.Tests.Features.Arena;

[TestClass]
public class TaomTournamentModelTests
{
    [TestMethod]
    public void ResolveDummyId_ParticipantCulturePresent_ReturnsParticipantCultureId()
    {
        var result = TaomTournamentModel.ResolveDummyId("erebor", "gondor");

        Assert.AreEqual("gear_practice_dummy_erebor", result);
    }

    [TestMethod]
    public void ResolveDummyId_NullParticipantCulture_ReturnsSettlementCultureId()
    {
        var result = TaomTournamentModel.ResolveDummyId(null, "gondor");

        Assert.AreEqual("gear_practice_dummy_gondor", result);
    }

    [TestMethod]
    public void ResolveDummyId_EmptyParticipantCulture_ReturnsSettlementCultureId()
    {
        var result = TaomTournamentModel.ResolveDummyId("", "gondor");

        Assert.AreEqual("gear_practice_dummy_gondor", result);
    }

    [TestMethod]
    public void ResolveDummyId_BothNull_ReturnsEmpireFallback()
    {
        var result = TaomTournamentModel.ResolveDummyId(null, null);

        Assert.AreEqual("gear_practice_dummy_empire", result);
    }

    [TestMethod]
    public void ResolveDummyId_NullParticipantEmptySettlement_ReturnsEmpireFallback()
    {
        var result = TaomTournamentModel.ResolveDummyId(null, "");

        Assert.AreEqual("gear_practice_dummy_empire", result);
    }

    [TestMethod]
    public void TierConstants_RegularAndElite_NoGapOrOverlap()
    {
        Assert.AreEqual(TaomTournamentModel.RegularMaxTier, TaomTournamentModel.EliteMinTier);
    }

    [TestMethod]
    public void TierConstants_RegularMin_ExcludesJunkTier()
    {
        Assert.IsTrue(TaomTournamentModel.RegularMinTier > 0f);
    }

    [TestMethod]
    public void TierConstants_EliteMin_IsAboveRegularMin()
    {
        Assert.IsTrue(TaomTournamentModel.EliteMinTier > TaomTournamentModel.RegularMinTier);
    }

    [TestMethod]
    public void TournamentStartChanceMultiplier_IsAboveVanilla()
    {
        // Vanilla uses 0.1f — we use a higher value to increase frequency
        Assert.IsTrue(TaomTournamentModel.TournamentStartChanceMultiplier > 0.1f);
    }

    [TestMethod]
    public void TournamentEndChanceGraceDays_IsAboveVanilla()
    {
        // Vanilla grace period is 10 days — we extend to keep tournaments alive longer
        Assert.IsTrue(TaomTournamentModel.TournamentEndChanceGraceDays > 10f);
    }

    [TestMethod]
    public void TournamentEndChanceRamp_IsSlowerThanVanilla()
    {
        // Vanilla ramp is 0.05f per day — we use a slower ramp
        Assert.IsTrue(TaomTournamentModel.TournamentEndChanceRamp < 0.05f);
    }
}
