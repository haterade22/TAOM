using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arena;

namespace TAOM.Tests.Features.Arena;

[TestClass]
public class TaomTournamentModelTests
{
    // The start and end tuning constants live on TournamentService (internal const, visible here through
    // InternalsVisibleTo); ResolveDummyId is tested in TournamentServiceTests and the prize bands in
    // TournamentPrizeRulesTests.

    [TestMethod]
    public void TournamentStartChance_DiminishingReturns_EachStepLowerThanPrevious()
    {
        // Phase 9b #137 — moved from TaomTournamentModel.TournamentStartChance* to
        // TournamentService.TournamentStartChance* (internal const). Test via the service.
        float gain1 = TournamentService.TournamentStartChance1Lord;
        float gain2 = TournamentService.TournamentStartChance2Lords - TournamentService.TournamentStartChance1Lord;
        float gain3 = TournamentService.TournamentStartChance3Lords - TournamentService.TournamentStartChance2Lords;

        Assert.IsTrue(gain2 < gain1);
        Assert.IsTrue(gain3 < gain2);
    }

    [TestMethod]
    public void TournamentStartChance_AllValues_AreInValidRange()
    {
        Assert.IsTrue(TournamentService.TournamentStartChance1Lord is > 0f and <= 1f);
        Assert.IsTrue(TournamentService.TournamentStartChance2Lords is > 0f and <= 1f);
        Assert.IsTrue(TournamentService.TournamentStartChance3Lords is > 0f and <= 1f);
    }

    [TestMethod]
    public void TournamentEndChanceGraceDays_IsAboveVanilla()
    {
        // Vanilla grace period is 10 days — we extend to keep tournaments alive longer
        Assert.IsTrue(TournamentService.TournamentEndChanceGraceDays > 10f);
    }

    [TestMethod]
    public void TournamentEndChanceRamp_IsSlowerThanVanilla()
    {
        // Vanilla ramp is 0.05f per day — we use a slower ramp
        Assert.IsTrue(TournamentService.TournamentEndChanceRamp < 0.05f);
    }
}
