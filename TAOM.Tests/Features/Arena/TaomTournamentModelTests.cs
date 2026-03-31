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
}
