using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.QuickActions;

/// <summary>
/// #917: InventoryVMAdapter.RefreshDisplay re-syncs the open inventory after a batch transfer through two
/// private SPInventoryVM methods bound as open delegates. If either stops binding after an engine update,
/// Unequip All lands but the paper doll, armour, weight and capacity stay stale, with no error anywhere.
/// </summary>
[TestClass]
public class InventoryPanelRefreshBindingTests
{
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PanelRefresh_BothPrivateMethods_BindAsOpenDelegates()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsTrue(InventoryVMAdapter.IsPanelRefreshBound,
            "SPInventoryVM.UpdateRightCharacter() or RefreshInformationValues() did not bind: "
            + "RefreshDisplay would leave the paper doll and stats stale after Unequip All.");
    }
}
