using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The verdicts of the field trial's two panel click steps (Marshal's Word stage M7).</summary>
[TestClass]
public class SeatTrialPanelRulesTests
{
    [TestMethod]
    public void CardClick_WithTheCompanySelected_Passes()
    {
        Assert.IsTrue(SeatTrialPanelRules.CardClickWorked(true, SeatRelayResult.Acted, true, false));
    }

    [DataTestMethod]
    [DataRow(false, (int)SeatRelayResult.Acted, true, false, DisplayName = "no card found")]
    [DataRow(true, (int)SeatRelayResult.Idle, true, false, DisplayName = "the relay did nothing")]
    [DataRow(true, (int)SeatRelayResult.SiteMissing, true, false, DisplayName = "the view model could not be read")]
    [DataRow(true, (int)SeatRelayResult.Acted, false, false, DisplayName = "the company is not the only one selected")]
    [DataRow(true, (int)SeatRelayResult.Acted, true, true, DisplayName = "the Seat faulted")]
    public void CardClick_Fails(bool found, int clicked, bool only, bool faulted)
    {
        Assert.IsFalse(SeatTrialPanelRules.CardClickWorked(found, (SeatRelayResult)clicked, only, faulted));
    }

    [TestMethod]
    public void OrderClick_SetThenItem_WithAnOrderSeen_Passes()
    {
        Assert.IsTrue(SeatTrialPanelRules.OrderClickWorked(true, SeatRelayResult.Acted, true, true, SeatRelayResult.Acted, 1, false));
    }

    [DataTestMethod]
    [DataRow(false, (int)SeatRelayResult.Acted, true, true, (int)SeatRelayResult.Acted, 1, false, DisplayName = "no set found")]
    [DataRow(true, (int)SeatRelayResult.Idle, true, true, (int)SeatRelayResult.Acted, 1, false, DisplayName = "the set click did nothing")]
    [DataRow(true, (int)SeatRelayResult.Acted, false, true, (int)SeatRelayResult.Acted, 1, false, DisplayName = "the sub-menu did not open")]
    [DataRow(true, (int)SeatRelayResult.Acted, true, false, (int)SeatRelayResult.Acted, 1, false, DisplayName = "no order found in the sub-menu")]
    [DataRow(true, (int)SeatRelayResult.Acted, true, true, (int)SeatRelayResult.Idle, 1, false, DisplayName = "the order click did nothing")]
    [DataRow(true, (int)SeatRelayResult.Acted, true, true, (int)SeatRelayResult.Acted, 0, false, DisplayName = "the view model never ran an order")]
    [DataRow(true, (int)SeatRelayResult.Acted, true, true, (int)SeatRelayResult.Acted, 1, true, DisplayName = "the Seat faulted")]
    public void OrderClick_Fails(bool setFound, int setClicked, bool setOpened, bool itemFound, int itemClicked, int seen, bool faulted)
    {
        Assert.IsFalse(SeatTrialPanelRules.OrderClickWorked(setFound, (SeatRelayResult)setClicked, setOpened, itemFound, (SeatRelayResult)itemClicked, seen, faulted));
    }
}
