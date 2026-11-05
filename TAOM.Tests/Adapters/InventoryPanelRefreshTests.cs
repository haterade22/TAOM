using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Tests.Adapters;

/// <summary>
/// #924, #926: after a programmatic batch of transfers, the open inventory runs vanilla's equip sequence
/// (<c>ProcessEquipItem</c>, v1.4.8 SPInventoryVM.cs:3475-3486): the paper doll, the information values, then
/// <c>ExecuteRemoveZeroCounts</c>. The VM is built bare, so the doll and information steps throw and are
/// logged; the zero-count prune must still run, and it is the step these tests can observe.
/// </summary>
[TestClass]
public class InventoryPanelRefreshTests
{
    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static SPInventoryVM VmWithSoldOutRow(out SPItemVM soldOut)
    {
        var vm = Bare<SPInventoryVM>();
        soldOut = Bare<SPItemVM>(); // ItemCount is 0 on a bare row: the state AfterTransfer leaves (:3853-3856)
        vm.LeftItemListVM = new MBBindingList<SPItemVM>();
        vm.RightItemListVM = new MBBindingList<SPItemVM> { soldOut };
        return vm;
    }

    [TestMethod]
    public void RefreshActiveVM_AfterPresetLoad_RemovesZeroCountRows()
    {
        var vm = VmWithSoldOutRow(out var soldOut);
        var adapter = new InventoryScreenAdapter(Substitute.For<IModLogger>());
        adapter.SetActive(vm);

        adapter.RefreshActiveVM();

        CollectionAssert.DoesNotContain(vm.RightItemListVM, soldOut,
            "A preset load that equips the last unit of a stack must not leave its row at count 0.");
    }

    [TestMethod]
    public void RefreshDisplay_AfterBatchSell_RemovesZeroCountRows()
    {
        // TrySellItem passes cameFromTradeData: true, which skips vanilla's prune (:3565-3568).
        var vm = VmWithSoldOutRow(out var soldOut);
        var adapter = new InventoryVMAdapter(Substitute.For<IModLogger>());
        adapter.SetActive(vm);

        adapter.RefreshDisplay();

        CollectionAssert.DoesNotContain(vm.RightItemListVM, soldOut,
            "A Quick Actions batch sell of a whole stack must not leave its row at count 0.");
    }
}
