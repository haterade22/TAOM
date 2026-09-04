using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AiPartySize;
using TAOM.Features.AiPartySize.Cheats;

namespace TAOM.Tests.Features.AiPartySize;

/// <summary>
/// The diagnostic exists to separate two causes that look identical in raw FoodChange data: a
/// relief that composed badly, and a party that was never eligible for the relief. These pin that
/// split and the band arithmetic the report judges "healthy" against.
/// </summary>
[TestClass]
public class AiFoodReliefReportTests
{
    private const float Tolerance = 0.001f;

    private static AiFoodReliefRow Row(float residual, bool eligible) => new AiFoodReliefRow
    {
        PartyName = "party",
        ClanName = "clan",
        CultureName = "culture",
        Members = 100,
        Eligible = eligible,
        Residual = residual,
    };

    [TestMethod]
    public void ExpectedResidualBand_IsTheRelievedBaselineTimesTheClampBounds()
    {
        AiFoodReliefReport.ExpectedResidualBand(0.9f, out var low, out var high);

        Assert.AreEqual(0.1f * AiPartySizeService.MinAbilityScale, low, Tolerance);
        Assert.AreEqual(0.1f * AiPartySizeService.MaxAbilityScale, high, Tolerance);
    }

    [TestMethod]
    public void ExpectedResidualBand_ZeroRelief_IsTheClampBandItself()
    {
        AiFoodReliefReport.ExpectedResidualBand(0f, out var low, out var high);

        Assert.AreEqual(AiPartySizeService.MinAbilityScale, low, Tolerance);
        Assert.AreEqual(AiPartySizeService.MaxAbilityScale, high, Tolerance);
    }

    [TestMethod]
    public void Summarize_NoParties_SaysSoRatherThanPrintingAnEmptyTable()
    {
        var text = AiFoodReliefReport.Summarize(new List<AiFoodReliefRow>(), 0.9f);

        StringAssert.Contains(text, "No AI parties");
    }

    [TestMethod]
    public void Summarize_EligibleAndIneligible_AreCountedSeparately()
    {
        // The discriminating case, and the reason the command exists. A high-residual party that is
        // NOT eligible is vanilla behaviour, not a relief failure, and the report must not let the
        // two be read as one population.
        var rows = new List<AiFoodReliefRow>
        {
            Row(0.10f, eligible: true),
            Row(0.12f, eligible: true),
            Row(0.95f, eligible: false),
        };

        var text = AiFoodReliefReport.Summarize(rows, 0.9f);

        StringAssert.Contains(text, "Eligible (relief applies): 2 parties");
        StringAssert.Contains(text, "Not eligible (relief never runs): 1 parties");
        StringAssert.Contains(text, "Every eligible party is inside the band");
    }

    [TestMethod]
    public void Summarize_EligiblePartyOutsideTheBand_IsCalledOut()
    {
        // What the shipped-before behaviour looked like: a Lothlorien party that tripped the old
        // skip ate 0.85 of vanilla while asking for a 90% relief.
        var rows = new List<AiFoodReliefRow>
        {
            Row(0.10f, eligible: true),
            Row(0.85f, eligible: true),
        };

        var text = AiFoodReliefReport.Summarize(rows, 0.9f);

        StringAssert.Contains(text, "1 eligible parties are OUTSIDE the band");
    }

    [TestMethod]
    public void Summarize_ReportsTheSpreadBecauseOneSettingShouldMeanOneOutcome()
    {
        var rows = new List<AiFoodReliefRow>
        {
            Row(0.100f, eligible: true),
            Row(0.923f, eligible: true),
        };

        var text = AiFoodReliefReport.Summarize(rows, 0.9f);

        StringAssert.Contains(text, "spread 9.230x");
    }

    [TestMethod]
    public void Table_SortsWorstFirstAndSaysWhatItTruncated()
    {
        var rows = new List<AiFoodReliefRow>
        {
            Row(0.10f, eligible: true),
            Row(0.90f, eligible: false),
            Row(0.50f, eligible: true),
        };

        var text = AiFoodReliefReport.Table(rows, limit: 2);

        int worst = text.IndexOf("0.900", System.StringComparison.Ordinal);
        int middle = text.IndexOf("0.500", System.StringComparison.Ordinal);
        Assert.IsTrue(worst > 0 && middle > worst, "highest residual must come first");
        StringAssert.Contains(text, "1 more");
    }
}
