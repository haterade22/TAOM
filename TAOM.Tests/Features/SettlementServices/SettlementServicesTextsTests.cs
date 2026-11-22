using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementServices;
using TAOM.Features.SettlementServices.Hooks;

namespace TAOM.Tests.Features.SettlementServices;

/// <summary>
/// The player text of Watch-Silver and Tidings of the Lists. An inline <c>{=key}default</c> text renders its default without the
/// game, so these run headless on the desktop; they construct <c>TextObject</c>, so the reference-assembly run skips them.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SettlementServicesTextsTests
{
    [TestMethod]
    public void Tier_NamesThePointsAndThePrice_AndCarriesItsOwnCoinIcon()
    {
        var text = SettlementServicesTexts.Tier(5, 2400).ToString();

        StringAssert.StartsWith(text, "Raise loyalty by 5 for 2400");
        StringAssert.Contains(text, "<img src=\"General\\Icons\\Coin@2x\"");
        StringAssert.EndsWith(text, ".");
    }

    [TestMethod]
    public void TheCeilingReason_ReadsAsDecided()
        => Assert.AreEqual("Silver cannot steady this place beyond what it holds now.", SettlementServicesTexts.AtCeiling().ToString());

    [TestMethod]
    public void TheVanillaGoldReason_IsReusedByItsKey_NotCopied()
        => Assert.AreEqual("You don't have enough gold.", SettlementServicesTexts.NotEnoughGold().ToString());

    [TestMethod]
    public void TidingsTitleAndNone_ReadAsDecided()
    {
        Assert.AreEqual("Tidings of the Lists", SettlementServicesTexts.TidingsTitle());
        Assert.AreEqual("The training master knows of no tournament in a friendly town. You keep your coin.", SettlementServicesTexts.TidingsNone());
    }

    [TestMethod]
    public void TheTidingsRefusal_ReadsAsDecided()
        => Assert.AreEqual("The training master has no answer for you now.", SettlementServicesTexts.TidingsRefused());

    [TestMethod]
    public void TidingsBody_ListsEachRowAsTownAndRealm_OnItsOwnLine()
    {
        var body = SettlementServicesTexts.TidingsBody(new[] { new TidingsRow("Aldburg", "Rohan"), new TidingsRow("Bree", "Arnor") });

        // {newline} is a text variable the running game sets; headless it renders empty, so only the header and the rows are asserted.
        StringAssert.StartsWith(body, "The training master names the nearest tournaments:");
        StringAssert.EndsWith(body.Replace("\r", "").Replace("\n", "|"), "Aldburg (Rohan)|Bree (Arnor)");
    }
}
