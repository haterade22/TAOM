using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// The culture-market pool skips items the Armory marks not for sale (is_merchandise="false", which v1.4.8
/// ItemObject reads into NotMerchandise, ItemObject.cs:154, :428-432, :498-502). Vanilla's own inflows skip them
/// too (WorkshopsCampaignBehavior.cs:993, DefaultTournamentModel.cs:101 in the v1.4.8 dump).
/// </summary>
[TestClass]
public class ItemPoolAdapterMerchandiseTests
{
    [TestMethod]
    public void IsMarketCandidate_MerchandiseItem_IsKept()
        => Assert.IsTrue(ItemPoolAdapter.IsMarketCandidate("test_item", notMerchandise: false));

    [TestMethod]
    public void IsMarketCandidate_NotMerchandiseItem_IsSkipped()
        => Assert.IsFalse(ItemPoolAdapter.IsMarketCandidate("test_item", notMerchandise: true));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IsMarketCandidate_ItemWithoutAnId_IsSkipped(string? id)
        => Assert.IsFalse(ItemPoolAdapter.IsMarketCandidate(id, notMerchandise: false));

    [TestMethod]
    public void GetAllItems_AsksIsMarketCandidate_ForEveryItem()
        => StringAssert.Contains(ReadSource("Main/Adapters/ItemPoolAdapter.cs", stripComments: true),
            "IsMarketCandidate(item.StringId, item.NotMerchandise)");
}
