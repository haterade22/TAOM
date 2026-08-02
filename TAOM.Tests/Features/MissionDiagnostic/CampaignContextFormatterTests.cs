using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionDiagnostic;

namespace TAOM.Tests.Features.MissionDiagnostic;

// The campaign-context line is the correlation key on every crash report we receive: which save,
// which hero, which in-game day. Its two halves read different engine subsystems and fail
// independently -- reading CampaignTime before Campaign.Models is up throws DivideByZeroException
// (CampaignTime.GetDayOfSeason divides by the static TimeTicksPerDay, still 0 pre-init), which is
// exactly what the 2026-08-02 dwarf-vs-Rhun crash log shows. One half failing must never blank the
// other, and neither may propagate into engine code.
[TestClass]
public class CampaignContextFormatterTests
{
    [TestMethod]
    public void BothReadersSucceed_LineCarriesBoth()
    {
        var line = CampaignContextFormatter.Describe(() => "now=Spring 5, 1084", () => "MainHero='Drel'");

        Assert.AreEqual("now=Spring 5, 1084, MainHero='Drel'", line);
    }

    [TestMethod]
    public void TimeReaderThrows_HeroStillReported_AndExceptionTypeNamed()
    {
        var line = CampaignContextFormatter.Describe(
            () => throw new DivideByZeroException(),
            () => "MainHero='Drel'");

        Assert.AreEqual("<time read failed: DivideByZeroException>, MainHero='Drel'", line);
    }

    [TestMethod]
    public void HeroReaderThrows_TimeStillReported()
    {
        var line = CampaignContextFormatter.Describe(
            () => "now=Spring 5, 1084",
            () => throw new NullReferenceException());

        Assert.AreEqual("now=Spring 5, 1084, <hero read failed: NullReferenceException>", line);
    }

    [TestMethod]
    public void BothReadersThrow_LineStillProduced()
    {
        var line = CampaignContextFormatter.Describe(
            () => throw new DivideByZeroException(),
            () => throw new InvalidOperationException());

        Assert.AreEqual(
            "<time read failed: DivideByZeroException>, <hero read failed: InvalidOperationException>",
            line);
    }

    // A reader that returns null is not the same as one that throws -- an engine getter can hand
    // back null without failing, and "MainHero=" with nothing after it reads as a bug in our logger.
    [TestMethod]
    public void ReaderReturnsNull_FallsBackToPlaceholderNotEmpty()
    {
        var line = CampaignContextFormatter.Describe(() => null, () => null);

        Assert.AreEqual("<time unavailable>, <no hero>", line);
    }

    [TestMethod]
    public void NullReaderDelegates_DoNotThrow()
    {
        var line = CampaignContextFormatter.Describe(null, null);

        Assert.AreEqual("<time unavailable>, <no hero>", line);
    }
}
