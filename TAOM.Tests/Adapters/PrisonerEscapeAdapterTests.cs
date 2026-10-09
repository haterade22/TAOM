using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The prisoner-escape adapter outside a campaign: it must answer empty or false, never throw from a
/// computed engine getter (Hero.MainHero and Clan.PlayerClan both dereference Campaign.Current).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PrisonerEscapeAdapterTests
{
    private readonly PrisonerEscapeAdapter _sut = new PrisonerEscapeAdapter();

    [TestMethod]
    public void GetCapturedLords_NoCampaign_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.GetCapturedLords(new List<string> { "empire_w" }).Count);
    }

    [TestMethod]
    public void GetCapturedLords_NullKingdomIds_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.GetCapturedLords(null!).Count);
    }

    [TestMethod]
    public void Escape_NoCampaign_ReturnsFalse()
    {
        Assert.IsFalse(_sut.Escape("lord_1"));
    }

    [TestMethod]
    public void Escape_EmptyId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.Escape(string.Empty));
    }
}
