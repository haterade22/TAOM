using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.QuickActions.Audio;
using TAOM.Features.QuickActions.Models;

namespace TAOM.Features.QuickActions;

public sealed class QuickActionsService : IQuickActionsService
{
    private readonly IQuickActionsSettingsProvider _settings;
    private readonly IInventoryVMAdapter _inventory;
    private readonly IPlayerEquipmentAdapter _equipment;
    private readonly IQuickActionsAudioPlayer _audio;
    private readonly IModLogger _logger;

    public QuickActionsService(
        IQuickActionsSettingsProvider settings,
        IInventoryVMAdapter inventory,
        IPlayerEquipmentAdapter equipment,
        IQuickActionsAudioPlayer audio,
        IModLogger logger)
    {
        _settings = settings;
        _inventory = inventory;
        _equipment = equipment;
        _audio = audio;
        _logger = logger;
    }

    public IReadOnlyList<(QuickActionType type, string label, string description)> GetMenuOptions()
    {
        return new List<(QuickActionType, string, string)>
        {
            (QuickActionType.SellDamaged,    "Sell Damaged",     "Sell all items below the configured damage threshold."),
            (QuickActionType.SellLowValue,   "Sell Low Value",   "Sell all items at or below the configured denars threshold."),
            (QuickActionType.UnequipAll,     "Unequip All",      "Strip every slot of the player's battle and civilian equipment."),
            (QuickActionType.OriginalSellAll,"Sell All (Vanilla)","Default Bannerlord 'sell all' behavior."),
        };
    }

    public void OpenMenu(Action originalSellAll)
    {
        try
        {
            var elements = new List<InquiryElement>
            {
                new InquiryElement(
                    QuickActionType.SellDamaged,
                    "Sell Damaged",
                    null, true,
                    "Sells damaged items below your configured threshold."),
                new InquiryElement(
                    QuickActionType.SellLowValue,
                    $"Sell Low Value (≤{_settings.LowValueThreshold} denars)",
                    null, true,
                    "Sells items at or below your denars threshold."),
                new InquiryElement(
                    QuickActionType.UnequipAll,
                    "Unequip All",
                    null, true,
                    "Strips your battle + civilian equipment to inventory."),
                new InquiryElement(
                    QuickActionType.OriginalSellAll,
                    "Sell All (Vanilla)",
                    null, true,
                    "Default Bannerlord behavior."),
            };

            var data = new MultiSelectionInquiryData(
                titleText: new TextObject("{=taom_qa_title}Quick Actions").ToString(),
                descriptionText: new TextObject("{=taom_qa_desc}Select an action.").ToString(),
                inquiryElements: elements,
                isExitShown: true,
                minSelectableOptionCount: 1,
                maxSelectableOptionCount: 1,
                affirmativeText: new TextObject("{=taom_qa_ok}OK").ToString(),
                negativeText: new TextObject("{=taom_qa_cancel}Cancel").ToString(),
                affirmativeAction: chosen =>
                {
                    if (chosen == null || chosen.Count == 0) return;
                    var selected = (QuickActionType)chosen[0].Identifier;
                    DispatchSelected(selected, originalSellAll);
                },
                negativeAction: _ => { });

            MBInformationManager.ShowMultiSelectionInquiry(data);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[QuickActions] OpenMenu failed: {ex.Message}");
            try { originalSellAll?.Invoke(); }
            catch (Exception inner) { _logger.LogError($"[QuickActions] vanilla fallback failed: {inner.Message}"); }
        }
    }

    private void DispatchSelected(QuickActionType type, Action originalSellAll)
    {
        Action runner = type switch
        {
            QuickActionType.SellDamaged => () => SellAllDamaged(),
            QuickActionType.SellLowValue => () => SellAllLowValue(),
            QuickActionType.UnequipAll => () => UnequipAll(),
            QuickActionType.OriginalSellAll => () => SafelyInvokeOriginal(originalSellAll),
            _ => () => SafelyInvokeOriginal(originalSellAll),
        };

        if (_settings.ShowConfirmation && type != QuickActionType.OriginalSellAll)
        {
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=taom_qa_confirm_title}Confirm").ToString(),
                    new TextObject("{=taom_qa_confirm_msg}Apply this bulk action?").ToString(),
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: new TextObject("{=taom_qa_yes}Yes").ToString(),
                    negativeText: new TextObject("{=taom_qa_no}No").ToString(),
                    affirmativeAction: () => runner(),
                    negativeAction: () => { }));
            }
            catch (Exception ex)
            {
                _logger.LogError($"[QuickActions] confirm-inquiry failed: {ex.Message}");
                runner();
            }
        }
        else
        {
            runner();
        }
    }

    private void SafelyInvokeOriginal(Action originalSellAll)
    {
        try { originalSellAll?.Invoke(); }
        catch (Exception ex) { _logger.LogError($"[QuickActions] vanilla sell-all failed: {ex.Message}"); }
    }

    public QuickActionResult SellAllDamaged()
    {
        if (!_inventory.IsAvailable)
        {
            if (_settings.IsDebugMode)
                _logger.LogDebug("[QuickActions] SellAllDamaged: no inventory active");
            return QuickActionResult.Empty(QuickActionStatus.NoInventoryActive);
        }

        var threshold = _settings.ResolveDamagedThreshold();
        var items = _inventory.GetRightPaneItems();
        int unitsSold = 0;
        int gold = 0;

        foreach (var item in items)
        {
            if (!IsDamagedSellTarget(item, threshold)) continue;
            // Codex review #36 fix: count units, not rows. A stack of 50 arrows is one row
            // but TrySellItem (post-fix) sells the full stack via TransactionCount.
            var stack = item.StackAmount;
            if (stack <= 0) continue;
            if (_inventory.TrySellItem(item))
            {
                unitsSold += stack;
                gold += item.ItemValue * stack;
            }
        }

        return FinalizeSell(unitsSold, gold, isUnequip: false);
    }

    public QuickActionResult SellAllLowValue()
    {
        if (!_inventory.IsAvailable)
        {
            if (_settings.IsDebugMode)
                _logger.LogDebug("[QuickActions] SellAllLowValue: no inventory active");
            return QuickActionResult.Empty(QuickActionStatus.NoInventoryActive);
        }

        var threshold = _settings.LowValueThreshold;
        var items = _inventory.GetRightPaneItems();
        int unitsSold = 0;
        int gold = 0;

        foreach (var item in items)
        {
            if (!IsLowValueSellTarget(item, threshold)) continue;
            var stack = item.StackAmount;
            if (stack <= 0) continue;
            if (_inventory.TrySellItem(item))
            {
                unitsSold += stack;
                gold += item.ItemValue * stack;
            }
        }

        return FinalizeSell(unitsSold, gold, isUnequip: false);
    }

    public QuickActionResult UnequipAll()
    {
        int affected;
        try
        {
            affected = _equipment.TryUnequipAllPlayerSlots();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[QuickActions] UnequipAll failed: {ex.Message}");
            return QuickActionResult.Empty(QuickActionStatus.Failed);
        }

        if (affected <= 0)
            return QuickActionResult.Empty(QuickActionStatus.NothingMatched);

        // Refresh so the inventory display picks up the items deposited into the roster.
        try { _inventory.RefreshDisplay(); }
        catch (Exception ex) { _logger.LogError($"[QuickActions] post-unequip refresh failed: {ex.Message}"); }

        if (_settings.PlaySounds)
        {
            try { _audio.PlayUnequipCompleted(); }
            catch (Exception ex) { _logger.LogError($"[QuickActions] audio.PlayUnequipCompleted failed: {ex.Message}"); }
        }

        if (_settings.IsDebugMode)
            _logger.LogDebug($"[QuickActions] UnequipAll stripped {affected} slots");

        return QuickActionResult.OfSuccess(affected, gold: 0);
    }

    private QuickActionResult FinalizeSell(int sold, int gold, bool isUnequip)
    {
        if (sold == 0)
            return QuickActionResult.Empty(QuickActionStatus.NothingMatched);

        try { _inventory.RefreshDisplay(); }
        catch (Exception ex) { _logger.LogError($"[QuickActions] post-sell refresh failed: {ex.Message}"); }

        if (_settings.PlaySounds)
        {
            try { _audio.PlaySellCompleted(); }
            catch (Exception ex) { _logger.LogError($"[QuickActions] audio.PlaySellCompleted failed: {ex.Message}"); }
        }

        if (_settings.IsDebugMode)
            _logger.LogDebug($"[QuickActions] sold {sold} items for ~{gold} denars (pre-tax)");

        return QuickActionResult.OfSuccess(sold, gold);
    }

    // ===== Filters =====

    private bool IsDamagedSellTarget(IInventoryItemAdapter item, float threshold)
    {
        if (item == null) return false;
        if (item.IsLocked) return false;
        if (!item.IsTransferable) return false;
        if (item.IsEquipped && !_settings.SellDamagedEquipped) return false;
        if (item.IsHorse && _settings.ExcludeDamagedHorses) return false;
        // Pristine threshold (0f) is reserved as a sentinel — never matches; user must pick a real preset.
        if (threshold >= 0f) return false;
        var modifierOffset = item.ModifierPriceMultiplier - 1f;
        return modifierOffset <= threshold;
    }

    private bool IsLowValueSellTarget(IInventoryItemAdapter item, int threshold)
    {
        if (item == null) return false;
        if (item.IsLocked) return false;
        if (!item.IsTransferable) return false;
        if (item.IsEquipped && !_settings.SellLowValueEquipped) return false;
        if (item.IsHorse && _settings.ExcludeLowValueHorses) return false;
        if (item.IsFood && _settings.ExcludeLowValueFood) return false;
        if (item.IsTradeGood && _settings.ExcludeLowValueTradeGoods) return false;
        return item.ItemValue <= threshold;
    }
}
