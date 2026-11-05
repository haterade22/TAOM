using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// #994: the clan screen's UpdateProperties resets a refuge row's name and wage line, so the
/// decoration is re-applied by a postfix on every UpdateProperties call, looking the row up through
/// IRefugeBook.GetByPartyId.
/// </summary>
[TestClass]
public class RefugeClanRowDecisionTests
{
    [TestMethod]
    public void ClanScreenPatch_NoLongerDecoratesRowsItself()
    {
        // The row constructor's RefreshValues now runs the UpdateProperties postfix, which applies the
        // suffix; a second application in RefugeClanScreenPatch would print "(being raised)" twice.
        var src = RepoPaths.ReadSource("Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs", stripComments: true);

        Assert.IsFalse(src.Contains("taom_rf_clan_row_building"), "the suffix belongs to the UpdateProperties postfix only");
        Assert.IsFalse(src.Contains("SuppressWagePanel"), "wage suppression belongs to the UpdateProperties postfix only");
    }

    [TestMethod]
    public void RowRefreshPatch_AppliesTheSuffixOnce_AndTargetsUpdateProperties()
    {
        var src = RepoPaths.ReadSource("Main/Features/Refuge/Hooks/RefugeClanRowRefreshPatch.cs", stripComments: true);

        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(src, "taom_rf_clan_row_building").Count);
        StringAssert.Contains(src, "UpdateProperties");
        StringAssert.Contains(src, "Patch75_Refuge");
    }

    [TestMethod]
    public void RefugeIoC_InitializesTheRowRefreshPatch_WithTheBook()
    {
        // RefugeIoC.InitializePatchStatics hands the read-only book to the row patch; a dropped line leaves the
        // postfix null-guarded and every refuge row undecorated, with no error.
        var src = RepoPaths.ReadSource("Main/Features/Refuge/RefugeIoC.cs", stripComments: true);

        StringAssert.Contains(src, "RefugeClanRowRefreshPatch.Initialize(container.Resolve<IRefugeBook>());");
    }
}
