using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.QuickActions;
using TAOM.Features.QuickActions.Audio;
using TAOM.Features.QuickActions.Models;

namespace TAOM.Tests.Features.QuickActions;

[TestClass]
public class QuickActionsServiceTests
{
    private IQuickActionsSettingsProvider _settings = null!;
    private IInventoryVMAdapter _inventory = null!;
    private IPlayerEquipmentAdapter _equipment = null!;
    private IQuickActionsAudioPlayer _audio = null!;
    private IModLogger _logger = null!;
    private QuickActionsService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IQuickActionsSettingsProvider>();
        _inventory = Substitute.For<IInventoryVMAdapter>();
        _equipment = Substitute.For<IPlayerEquipmentAdapter>();
        _audio = Substitute.For<IQuickActionsAudioPlayer>();
        _logger = Substitute.For<IModLogger>();

        _settings.EnableQuickActions.Returns(true);
        _settings.EnableInventorySearch.Returns(true);
        _settings.DamagedPreset.Returns(DamagedQualityPreset.Moderate);
        _settings.CustomDamagedThreshold.Returns(-0.20f);
        _settings.UseCustomThreshold.Returns(false);
        _settings.SellDamagedEquipped.Returns(false);
        _settings.ExcludeDamagedHorses.Returns(true);
        _settings.LowValueThreshold.Returns(100);
        _settings.SellLowValueEquipped.Returns(false);
        _settings.ExcludeLowValueFood.Returns(true);
        _settings.ExcludeLowValueHorses.Returns(true);
        _settings.ExcludeLowValueTradeGoods.Returns(false);
        _settings.ShowConfirmation.Returns(false); // tests bypass confirmation by default
        _settings.PlaySounds.Returns(true);
        _settings.IsDebugMode.Returns(false);
        _settings.ResolveDamagedThreshold().Returns(-0.20f);

        _inventory.IsAvailable.Returns(true);
        _inventory.TrySellItem(Arg.Any<IInventoryItemAdapter>()).Returns(true);

        _sut = new QuickActionsService(_settings, _inventory, _equipment, _audio, _logger);
    }

    private static IInventoryItemAdapter MakeItem(
        string id = "sword_1",
        int value = 200,
        bool isLocked = false,
        bool isTransferable = true,
        bool isEquipped = false,
        bool isHorse = false,
        bool isFood = false,
        bool isTradeGood = false,
        float modifier = 1.0f,
        int stackAmount = 1)
    {
        var item = Substitute.For<IInventoryItemAdapter>();
        item.ItemId.Returns(id);
        item.ItemValue.Returns(value);
        item.IsLocked.Returns(isLocked);
        item.IsTransferable.Returns(isTransferable);
        item.IsEquipped.Returns(isEquipped);
        item.IsHorse.Returns(isHorse);
        item.IsFood.Returns(isFood);
        item.IsTradeGood.Returns(isTradeGood);
        item.ModifierPriceMultiplier.Returns(modifier);
        item.StackAmount.Returns(stackAmount);
        return item;
    }

    private void SetItems(params IInventoryItemAdapter[] items)
    {
        _inventory.GetRightPaneItems().Returns(items);
    }

    // ===== SellAllDamaged: skip-guard exhaustion =====

    [TestMethod]
    public void SellAllDamaged_NoInventoryActive_ReturnsNoInventoryAndDoesNotSell()
    {
        _inventory.IsAvailable.Returns(false);

        var result = _sut.SellAllDamaged();

        Assert.AreEqual(QuickActionStatus.NoInventoryActive, result.Status);
        _inventory.DidNotReceiveWithAnyArgs().TrySellItem(default!);
    }

    [TestMethod]
    public void SellAllDamaged_DamagedItemBelowThreshold_IsSold()
    {
        var damaged = MakeItem(modifier: 0.7f); // priceMultiplier - 1f = -0.30 <= -0.20 threshold
        SetItems(damaged);

        var result = _sut.SellAllDamaged();

        Assert.AreEqual(QuickActionStatus.Success, result.Status);
        Assert.AreEqual(1, result.ItemsAffected);
        _inventory.Received(1).TrySellItem(damaged);
    }

    [TestMethod]
    public void SellAllDamaged_PristineItem_IsNotSold()
    {
        var pristine = MakeItem(modifier: 1.0f); // exactly at base
        SetItems(pristine);

        var result = _sut.SellAllDamaged();

        Assert.AreEqual(QuickActionStatus.NothingMatched, result.Status);
        _inventory.DidNotReceive().TrySellItem(pristine);
    }

    [TestMethod]
    public void SellAllDamaged_LockedItem_IsNotSold()
    {
        var locked = MakeItem(modifier: 0.6f, isLocked: true);
        SetItems(locked);

        _sut.SellAllDamaged();

        _inventory.DidNotReceive().TrySellItem(locked);
    }

    [TestMethod]
    public void SellAllDamaged_NonTransferable_IsNotSold()
    {
        var qi = MakeItem(modifier: 0.6f, isTransferable: false);
        SetItems(qi);

        _sut.SellAllDamaged();

        _inventory.DidNotReceive().TrySellItem(qi);
    }

    [TestMethod]
    public void SellAllDamaged_EquippedItem_NotSoldWhenSellEquippedFalse()
    {
        _settings.SellDamagedEquipped.Returns(false);
        var equipped = MakeItem(modifier: 0.6f, isEquipped: true);
        SetItems(equipped);

        _sut.SellAllDamaged();

        _inventory.DidNotReceive().TrySellItem(equipped);
    }

    [TestMethod]
    public void SellAllDamaged_EquippedItem_SoldWhenSellEquippedTrue()
    {
        _settings.SellDamagedEquipped.Returns(true);
        var equipped = MakeItem(modifier: 0.6f, isEquipped: true);
        SetItems(equipped);

        var result = _sut.SellAllDamaged();

        Assert.AreEqual(1, result.ItemsAffected);
        _inventory.Received(1).TrySellItem(equipped);
    }

    [TestMethod]
    public void SellAllDamaged_DamagedHorse_NotSoldWhenExcludeHorsesTrue()
    {
        _settings.ExcludeDamagedHorses.Returns(true);
        var horse = MakeItem(modifier: 0.6f, isHorse: true);
        SetItems(horse);

        _sut.SellAllDamaged();

        _inventory.DidNotReceive().TrySellItem(horse);
    }

    [TestMethod]
    public void SellAllDamaged_DamagedHorse_SoldWhenExcludeHorsesFalse()
    {
        _settings.ExcludeDamagedHorses.Returns(false);
        var horse = MakeItem(modifier: 0.6f, isHorse: true);
        SetItems(horse);

        var result = _sut.SellAllDamaged();

        Assert.AreEqual(1, result.ItemsAffected);
    }

    [TestMethod]
    public void SellAllDamaged_CustomThreshold_OverridesPreset()
    {
        _settings.UseCustomThreshold.Returns(true);
        _settings.CustomDamagedThreshold.Returns(-0.05f);
        _settings.ResolveDamagedThreshold().Returns(-0.05f);
        var slightlyDamaged = MakeItem(modifier: 0.92f); // -0.08 <= -0.05 (sold)
        var minorlyDamaged = MakeItem(modifier: 0.97f); // -0.03 not <= -0.05 (kept)
        SetItems(slightlyDamaged, minorlyDamaged);

        _sut.SellAllDamaged();

        _inventory.Received(1).TrySellItem(slightlyDamaged);
        _inventory.DidNotReceive().TrySellItem(minorlyDamaged);
    }

    [TestMethod]
    public void SellAllDamaged_ModifierPreservation_FilterUsesPriceMultiplier_NotItemValue()
    {
        // High-value sword with damaged modifier: ItemValue=2000 (irrelevant), modifier=0.7 (sold)
        var pricyButDamaged = MakeItem(value: 2000, modifier: 0.7f);
        // Cheap pristine: ItemValue=10 (looks low), modifier=1.0 (NOT sold by damage filter)
        var cheapPristine = MakeItem(value: 10, modifier: 1.0f);
        SetItems(pricyButDamaged, cheapPristine);

        _sut.SellAllDamaged();

        _inventory.Received(1).TrySellItem(pricyButDamaged);
        _inventory.DidNotReceive().TrySellItem(cheapPristine);
    }

    // ===== SellAllLowValue: skip-guard exhaustion =====

    [TestMethod]
    public void SellAllLowValue_BelowThreshold_IsSold()
    {
        var cheap = MakeItem(value: 50);
        SetItems(cheap);

        var result = _sut.SellAllLowValue();

        Assert.AreEqual(QuickActionStatus.Success, result.Status);
        Assert.AreEqual(1, result.ItemsAffected);
    }

    [TestMethod]
    public void SellAllLowValue_AtThreshold_IsSold()
    {
        var atBoundary = MakeItem(value: 100); // <=100
        SetItems(atBoundary);

        _sut.SellAllLowValue();

        _inventory.Received(1).TrySellItem(atBoundary);
    }

    [TestMethod]
    public void SellAllLowValue_AboveThreshold_NotSold()
    {
        var pricy = MakeItem(value: 101);
        SetItems(pricy);

        _sut.SellAllLowValue();

        _inventory.DidNotReceive().TrySellItem(pricy);
    }

    [TestMethod]
    public void SellAllLowValue_LockedItem_NotSold()
    {
        var locked = MakeItem(value: 10, isLocked: true);
        SetItems(locked);

        _sut.SellAllLowValue();

        _inventory.DidNotReceive().TrySellItem(locked);
    }

    [TestMethod]
    public void SellAllLowValue_EquippedItem_NotSoldWhenSellEquippedFalse()
    {
        var equipped = MakeItem(value: 50, isEquipped: true);
        SetItems(equipped);

        _sut.SellAllLowValue();

        _inventory.DidNotReceive().TrySellItem(equipped);
    }

    [TestMethod]
    public void SellAllLowValue_EquippedItem_SoldWhenSellEquippedTrue()
    {
        _settings.SellLowValueEquipped.Returns(true);
        var equipped = MakeItem(value: 50, isEquipped: true);
        SetItems(equipped);

        _sut.SellAllLowValue();

        _inventory.Received(1).TrySellItem(equipped);
    }

    [TestMethod]
    public void SellAllLowValue_FoodItem_NotSoldWhenExcludeFoodTrue()
    {
        var food = MakeItem(value: 10, isFood: true);
        SetItems(food);

        _sut.SellAllLowValue();

        _inventory.DidNotReceive().TrySellItem(food);
    }

    [TestMethod]
    public void SellAllLowValue_FoodItem_SoldWhenExcludeFoodFalse()
    {
        _settings.ExcludeLowValueFood.Returns(false);
        var food = MakeItem(value: 10, isFood: true);
        SetItems(food);

        _sut.SellAllLowValue();

        _inventory.Received(1).TrySellItem(food);
    }

    [TestMethod]
    public void SellAllLowValue_Horse_NotSoldWhenExcludeHorsesTrue()
    {
        var horse = MakeItem(value: 80, isHorse: true);
        SetItems(horse);

        _sut.SellAllLowValue();

        _inventory.DidNotReceive().TrySellItem(horse);
    }

    [TestMethod]
    public void SellAllLowValue_Horse_SoldWhenExcludeHorsesFalse()
    {
        _settings.ExcludeLowValueHorses.Returns(false);
        var horse = MakeItem(value: 80, isHorse: true);
        SetItems(horse);

        _sut.SellAllLowValue();

        _inventory.Received(1).TrySellItem(horse);
    }

    [TestMethod]
    public void SellAllLowValue_TradeGood_SoldByDefault_ExcludeTradeGoodsFalse()
    {
        var good = MakeItem(value: 50, isTradeGood: true);
        SetItems(good);

        _sut.SellAllLowValue();

        _inventory.Received(1).TrySellItem(good);
    }

    [TestMethod]
    public void SellAllLowValue_TradeGood_NotSoldWhenExcludeTradeGoodsTrue()
    {
        _settings.ExcludeLowValueTradeGoods.Returns(true);
        var good = MakeItem(value: 50, isTradeGood: true);
        SetItems(good);

        _sut.SellAllLowValue();

        _inventory.DidNotReceive().TrySellItem(good);
    }

    [TestMethod]
    public void SellAllLowValue_NoInventoryActive_ReturnsNoInventory()
    {
        _inventory.IsAvailable.Returns(false);

        var result = _sut.SellAllLowValue();

        Assert.AreEqual(QuickActionStatus.NoInventoryActive, result.Status);
    }

    // ===== UnequipAll =====

    [TestMethod]
    public void UnequipAll_DelegatesToEquipmentAdapter_ReturnsCount()
    {
        _equipment.TryUnequipAllPlayerSlots().Returns(7);

        var result = _sut.UnequipAll();

        Assert.AreEqual(QuickActionStatus.Success, result.Status);
        Assert.AreEqual(7, result.ItemsAffected);
    }

    [TestMethod]
    public void UnequipAll_NothingEquipped_ReturnsNothingMatched()
    {
        _equipment.TryUnequipAllPlayerSlots().Returns(0);

        var result = _sut.UnequipAll();

        Assert.AreEqual(QuickActionStatus.NothingMatched, result.Status);
    }

    [TestMethod]
    public void UnequipAll_RefreshesInventoryDisplay_AfterStripping()
    {
        _equipment.TryUnequipAllPlayerSlots().Returns(3);

        _sut.UnequipAll();

        _inventory.Received(1).RefreshDisplay();
    }

    // ===== Audio =====

    [TestMethod]
    public void SellAllDamaged_OnSuccess_PlaysSellSound_WhenPlaySoundsTrue()
    {
        _settings.PlaySounds.Returns(true);
        SetItems(MakeItem(modifier: 0.6f));

        _sut.SellAllDamaged();

        _audio.Received(1).PlaySellCompleted();
    }

    [TestMethod]
    public void SellAllDamaged_OnSuccess_DoesNotPlaySound_WhenPlaySoundsFalse()
    {
        _settings.PlaySounds.Returns(false);
        SetItems(MakeItem(modifier: 0.6f));

        _sut.SellAllDamaged();

        _audio.DidNotReceive().PlaySellCompleted();
    }

    [TestMethod]
    public void UnequipAll_OnSuccess_PlaysUnequipSound_WhenPlaySoundsTrue()
    {
        _equipment.TryUnequipAllPlayerSlots().Returns(2);

        _sut.UnequipAll();

        _audio.Received(1).PlayUnequipCompleted();
    }

    [TestMethod]
    public void SellAllDamaged_NothingMatched_DoesNotPlaySound()
    {
        SetItems(MakeItem(modifier: 1.0f));

        _sut.SellAllDamaged();

        _audio.DidNotReceive().PlaySellCompleted();
    }

    // ===== Refresh =====

    [TestMethod]
    public void SellAllDamaged_OnAnySell_RefreshesDisplay()
    {
        SetItems(MakeItem(modifier: 0.6f));

        _sut.SellAllDamaged();

        _inventory.Received(1).RefreshDisplay();
    }

    [TestMethod]
    public void SellAllLowValue_OnAnySell_RefreshesDisplay()
    {
        SetItems(MakeItem(value: 50));

        _sut.SellAllLowValue();

        _inventory.Received(1).RefreshDisplay();
    }

    // ===== Menu options =====

    [TestMethod]
    public void GetMenuOptions_Returns4Options_OneForEachQuickActionType()
    {
        var options = _sut.GetMenuOptions();

        Assert.AreEqual(4, options.Count);
        var types = new HashSet<QuickActionType>();
        foreach (var (type, _, _) in options)
            types.Add(type);
        Assert.IsTrue(types.Contains(QuickActionType.SellDamaged));
        Assert.IsTrue(types.Contains(QuickActionType.SellLowValue));
        Assert.IsTrue(types.Contains(QuickActionType.UnequipAll));
        Assert.IsTrue(types.Contains(QuickActionType.OriginalSellAll));
    }

    // ===== Stack-amount accounting (Codex review #36 regression coverage) =====

    [TestMethod]
    public void SellAllDamaged_StackOf50_ReportsAllUnitsAsAffected_NotJustOneRow()
    {
        // Regression for Codex review #36: prior code reported ItemsAffected=1 for any
        // stack regardless of size, because TrySellItem returned bool not unit count.
        var damagedStack = MakeItem(value: 10, modifier: 0.7f, stackAmount: 50);
        SetItems(damagedStack);

        var result = _sut.SellAllDamaged();

        Assert.AreEqual(QuickActionStatus.Success, result.Status);
        Assert.AreEqual(50, result.ItemsAffected, "Should report units, not rows");
        Assert.AreEqual(500, result.TotalGold, "Gold should multiply by stack amount");
    }

    [TestMethod]
    public void SellAllLowValue_StackOf30_ReportsAllUnitsAsAffected()
    {
        var cheapStack = MakeItem(value: 5, stackAmount: 30);
        SetItems(cheapStack);

        var result = _sut.SellAllLowValue();

        Assert.AreEqual(30, result.ItemsAffected);
        Assert.AreEqual(150, result.TotalGold);
    }

    [TestMethod]
    public void SellAllDamaged_ZeroStack_SkipsItem()
    {
        // StackAmount of 0 should be skipped (defensive against malformed VMs).
        var emptyStack = MakeItem(value: 100, modifier: 0.6f, stackAmount: 0);
        SetItems(emptyStack);

        var result = _sut.SellAllDamaged();

        Assert.AreEqual(QuickActionStatus.NothingMatched, result.Status);
        _inventory.DidNotReceive().TrySellItem(emptyStack);
    }
}
