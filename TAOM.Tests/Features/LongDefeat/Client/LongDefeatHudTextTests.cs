using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>The English default of each line, with its variables filled and no brace left over. A translator reorders the variables, so the numbers are never glued on by code.</summary>
[TestClass]
public class LongDefeatHudTextTests
{
    [TestMethod]
    public void Wave_NamesEachPhase_AndTheWaveNumber()
    {
        Assert.AreEqual("Preparation", LongDefeatHudText.Wave(RunPhase.Preparation, 0));
        Assert.AreEqual("Wave 3", LongDefeatHudText.Wave(RunPhase.Wave, 3));
        Assert.AreEqual("Wave 3 cleared", LongDefeatHudText.Wave(RunPhase.Intermission, 3));
        Assert.AreEqual("The run is over", LongDefeatHudText.Wave(RunPhase.Ended, 3));
    }

    [TestMethod]
    public void TheCounters_FillTheirVariable()
    {
        Assert.AreEqual("Gold: 420", LongDefeatHudText.Gold(420));
        Assert.AreEqual("Attackers: 101", LongDefeatHudText.Attackers(101));
        Assert.AreEqual("Defenders: 150", LongDefeatHudText.Defenders(150));
    }

    [TestMethod]
    public void TheOffer_ResolvesTheConfigKey_ToItsEnglishDefault()
    {
        Assert.AreEqual("Westfold Militia", LongDefeatHudText.OfferName("{=TAOM_LD_Offer_Militia}Westfold Militia"));
        Assert.AreEqual("Plain Name", LongDefeatHudText.OfferName("Plain Name"));
        Assert.AreEqual("Pack of 12", LongDefeatHudText.PackSize(12));
        Assert.AreEqual("180 gold", LongDefeatHudText.Price(180));
    }

    [TestMethod]
    public void EveryBuyRefusal_HasALine_AndOkHasNone()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.Refusal(BuyResult.Ok));
        foreach (BuyResult result in System.Enum.GetValues(typeof(BuyResult)))
        {
            if (result == BuyResult.Ok)
                continue;
            var line = LongDefeatHudText.Refusal(result);
            Assert.IsTrue(line.Length > 0 && !line.Contains("{"), result + " -> '" + line + "'");
        }

        StringAssert.Contains(LongDefeatHudText.Refusal(BuyResult.NoGold), "gold");
        StringAssert.Contains(LongDefeatHudText.Refusal(BuyResult.GarrisonFull), "garrison");
    }

    [TestMethod]
    public void TheHornRefusal_IsNamedForBothRefusals_AndOkHasNone()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.Refusal(StartResult.Ok));
        Assert.IsTrue(LongDefeatHudText.Refusal(StartResult.NotOpen).Length > 0);
        Assert.IsTrue(LongDefeatHudText.Refusal(StartResult.RunOver).Length > 0);
    }

    [TestMethod]
    public void EveryEndReason_HasItsOwnTitle()
    {
        var titles = new System.Collections.Generic.HashSet<string>();
        foreach (RunEndReason reason in System.Enum.GetValues(typeof(RunEndReason)))
        {
            var title = LongDefeatHudText.EndTitle(reason);
            Assert.IsTrue(title.Length > 0 && !title.Contains("{"));
            Assert.IsTrue(titles.Add(title), "two reasons share the title " + title);
        }
    }

    [TestMethod]
    public void TheEndLines_ReportWavesAndKills_AndTheBest()
    {
        Assert.AreEqual("Waves cleared: 4\nKills: 321\nDefenders lost in cleared waves: 120", LongDefeatHudText.EndLines(4, 321, 120));
        Assert.AreEqual("Report file, in bin/Win64_Shipping_Client/Logs:\nlong_defeat_report_x_ld1.json", LongDefeatHudText.ReportFile("long_defeat_report_x_ld1.json"));
        Assert.AreEqual("Best run: waves cleared 5, kills 900", LongDefeatHudText.Best(5, 900));
        Assert.AreEqual("No best run yet", LongDefeatHudText.NoBest());
    }

    [TestMethod]
    public void TheClock_IsMinutesAndSeconds_AndANaNOrNegativeTimerReadsZero()
    {
        Assert.AreEqual("1:30", LongDefeatHudText.Clock(90f));
        Assert.AreEqual("0:01", LongDefeatHudText.Clock(0.2f));
        Assert.AreEqual("0:00", LongDefeatHudText.Clock(float.NaN));
        Assert.AreEqual("0:00", LongDefeatHudText.Clock(-5f));
        Assert.AreEqual("0:00", LongDefeatHudText.Clock(float.PositiveInfinity));
    }
}
