using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCamp;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The nearest-fortification rule behind the camp, refuge and stronghold keep-outs
/// (<see cref="FortificationSearch.NearestDistance"/>): only towns and castles count, the
/// nearest wins, and a NaN distance never wins. Pure, so the class is untagged and hosted CI
/// runs it; the services' own keep-out tests stay in their RequiresGame classes.
/// </summary>
[TestClass]
public class FortificationSearchTests
{
    private static SettlementSite TownSite(float distance) =>
        new SettlementSite(isTown: true, isCastle: false, distance: distance);

    private static SettlementSite CastleSite(float distance) =>
        new SettlementSite(isTown: false, isCastle: true, distance: distance);

    private static SettlementSite VillageSite(float distance) =>
        new SettlementSite(isTown: false, isCastle: false, distance: distance);

    [TestMethod]
    public void NearestDistance_NullSites_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue, FortificationSearch.NearestDistance(null));
    }

    [TestMethod]
    public void NearestDistance_NoSites_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue, FortificationSearch.NearestDistance(new List<SettlementSite>()));
    }

    [TestMethod]
    public void NearestDistance_OnlyVillages_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue,
            FortificationSearch.NearestDistance(new List<SettlementSite> { VillageSite(1f), VillageSite(2f) }),
            "a settlement that is neither a town nor a castle is no fortification");
    }

    [TestMethod]
    public void NearestDistance_Town_Counts()
    {
        Assert.AreEqual(7f, FortificationSearch.NearestDistance(new List<SettlementSite> { TownSite(7f) }));
    }

    [TestMethod]
    public void NearestDistance_Castle_Counts()
    {
        Assert.AreEqual(9f, FortificationSearch.NearestDistance(new List<SettlementSite> { CastleSite(9f) }));
    }

    [TestMethod]
    public void NearestDistance_SeveralSettlements_PicksTheNearestFortification()
    {
        var sites = new List<SettlementSite> { TownSite(5f), VillageSite(1f), CastleSite(3f), TownSite(4f) };

        Assert.AreEqual(3f, FortificationSearch.NearestDistance(sites));
    }

    [TestMethod]
    public void NearestDistance_NaNDistance_NeverWins()
    {
        var sites = new List<SettlementSite> { TownSite(float.NaN), CastleSite(7f) };

        Assert.AreEqual(7f, FortificationSearch.NearestDistance(sites),
            "NaN < nearest is false, so a corrupt position never becomes the nearest fortification");
    }

    [TestMethod]
    public void NearestDistance_OnlyNaNDistances_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue,
            FortificationSearch.NearestDistance(new List<SettlementSite> { TownSite(float.NaN) }),
            "the keep-out gates compare with <, so MaxValue never blocks");
    }
}
