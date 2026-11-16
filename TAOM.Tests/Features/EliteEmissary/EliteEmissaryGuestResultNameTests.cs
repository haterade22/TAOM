using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EliteEmissary.Hooks;

namespace TAOM.Tests.Features.EliteEmissary;

/// <summary>
/// A co-op guest's purchase result names the resource in the guest's own language (#1385). The host's reply
/// says whether the purchase was paid in a resource (a name) or in gold (null); each machine localizes the
/// resource name itself, so the host's name is in the host's language.
/// </summary>
[TestClass]
public class EliteEmissaryGuestResultNameTests
{
    [TestMethod]
    public void PaidInAResource_ShowsTheGuestsOwnName()
    {
        Assert.AreEqual("宝石", EliteEmissaryInquiryPresenter.GuestResultResourceName("Gems", "宝石"));
    }

    [TestMethod]
    public void PaidInGold_StaysGold_WhateverTheGuestThinks()
    {
        Assert.IsNull(EliteEmissaryInquiryPresenter.GuestResultResourceName(null, "宝石"));
    }

    [TestMethod]
    public void PaidInAResourceTheGuestDoesNotKnow_ShowsTheHostsName()
    {
        Assert.AreEqual("Gems", EliteEmissaryInquiryPresenter.GuestResultResourceName("Gems", null));
    }
}
