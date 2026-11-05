using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #936: the kit-tier members say tier now, but the save key stays "kitRank". Renaming the key would
/// wipe every save's kit ledger, so this pins the old name on the read and the write path
/// (ServiceContentRecord.TryParse and Serialize). Moved here from the 1.5.x ServiceRankLadderTests,
/// which belongs to the unported seven-rank ladder.
/// </summary>
[TestClass]
public class KitTierSaveKeyTests
{
    [TestMethod]
    public void ContentRecord_SaveKeyKitRank_IsFrozen_ReadAndWrittenUnderTheOldName()
    {
        Assert.IsTrue(ServiceContentRecord.TryParse("rank=1;kitRank=2;kitItems=a,b", out var parsed));
        Assert.AreEqual(2, parsed.HighestIssuedKitTier);

        var record = new ServiceContentRecord { HighestIssuedKitTier = 2 };
        StringAssert.Contains(record.Serialize(), "kitRank=2");
    }
}
