using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;
using SaveResult = TAOM.Features.CompanionTactics.FormationPresets.Models.SaveResult;

namespace TAOM.Features.CompanionTactics.FormationPresets.UI;

/// <summary>
/// View model for the OOB Save / Load preset overlay. Bound by OOBButtonsOverlay.xml.
/// Boundary class — captures the active OrderOfBattleVM from the tracker and runs CRUD
/// inquiries on top of <see cref="IFormationPresetService"/>.
/// </summary>
public sealed class OOBButtonsVM : ViewModel
{
    private readonly IFormationPresetService _presetService;
    private readonly IOrderOfBattleVMTracker _vmTracker;
    private readonly IModLogger _logger;

    private bool _isVisible;
    private string _presetsButtonText = "Presets";

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
            }
        }
    }

    [DataSourceProperty]
    public string PresetsButtonText
    {
        get => _presetsButtonText;
        set
        {
            if (_presetsButtonText != value)
            {
                _presetsButtonText = value;
                OnPropertyChangedWithValue(value, nameof(PresetsButtonText));
            }
        }
    }

    public OOBButtonsVM(
        IFormationPresetService presetService,
        IOrderOfBattleVMTracker vmTracker,
        IModLogger logger)
    {
        _presetService = presetService;
        _vmTracker = vmTracker;
        _logger = logger;
        UpdatePresetsButtonText();
        IsVisible = true;
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        UpdatePresetsButtonText();
    }

    public void ExecuteAssignCharacters()
    {
        var vm = _vmTracker.Current;
        if (vm == null)
        {
            DisplayMessage("No Order of Battle screen detected.", Colors.Red);
            return;
        }
        // Phase-1 stub. Codex review #36 (2026-05-06) flagged this as P2 — the button
        // surfaces the "Auto-Assign" intent but does NOT invoke HeroAutoAssigner against
        // the live OrderOfBattleVM. Full implementation requires reflection on
        // OrderOfBattleVM._allHeroes (private List<OrderOfBattleHeroItemVM>) and the per-
        // formation Heroes collection on OrderOfBattleVM.Formations[N], plus a corresponding
        // mutation path. Tracked as follow-up; until then, users see this message and can
        // still drag heroes manually.
        DisplayMessage("Auto-Assign is a Phase-1 stub — feature pending. See follow-up GitHub issue.", Colors.Yellow);
    }

    public void ExecuteManagePresets()
    {
        var vm = _vmTracker.Current;
        if (vm == null)
        {
            DisplayMessage("No Order of Battle screen detected.", Colors.Red);
            return;
        }

        var elements = new List<InquiryElement>
        {
            new InquiryElement("save", "Save Current Layout as Preset", null),
        };

        foreach (var preset in _presetService.Presets)
        {
            elements.Add(new InquiryElement("load:" + preset.Id,
                $"Load: {preset.Name}", null));
        }
        foreach (var preset in _presetService.Presets)
        {
            elements.Add(new InquiryElement("delete:" + preset.Id,
                $"Delete: {preset.Name}", null));
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            "Formation Presets",
            "Save / load / delete OOB hero-to-formation assignments.",
            elements,
            isExitShown: true,
            maxSelectableOptionCount: 1,
            minSelectableOptionCount: 0,
            affirmativeText: "OK",
            negativeText: "Cancel",
            affirmativeAction: list => HandleManageSelection(list, vm),
            negativeAction: _ => { }));
    }

    private void HandleManageSelection(List<InquiryElement> selected, OrderOfBattleVM vm)
    {
        if (selected == null || selected.Count == 0) return;
        var id = selected[0].Identifier as string;
        if (string.IsNullOrEmpty(id)) return;

        if (id == "save") { ShowSavePrompt(vm); return; }

        if (id.StartsWith("load:"))
        {
            var presetId = id.Substring("load:".Length);
            var preset = _presetService.GetPresetById(presetId);
            if (preset != null)
            {
                // Phase-1 stub. Codex review #36 P2 — Load does not yet apply assignments
                // back to OrderOfBattleVM. Full apply requires a boundary adapter for OOBVM
                // mutation. Tracked as follow-up.
                DisplayMessage($"Preset \"{preset.Name}\" — Load is a Phase-1 stub (apply-to-OOB not yet wired).", Colors.Yellow);
            }
            UpdatePresetsButtonText();
            return;
        }

        if (id.StartsWith("delete:"))
        {
            var presetId = id.Substring("delete:".Length);
            var preset = _presetService.GetPresetById(presetId);
            if (preset != null && _presetService.DeletePreset(presetId))
                DisplayMessage($"Preset \"{preset.Name}\" deleted.", Colors.Yellow);
            UpdatePresetsButtonText();
            return;
        }
    }

    private void ShowSavePrompt(OrderOfBattleVM vm)
    {
        InformationManager.ShowTextInquiry(new TextInquiryData(
            "Save Formation Preset",
            "Enter a name for this preset:",
            isAffirmativeOptionShown: true,
            isNegativeOptionShown: true,
            affirmativeText: "Save",
            negativeText: "Cancel",
            affirmativeAction: name => SaveCurrent(name, vm),
            negativeAction: () => { },
            shouldInputBeObfuscated: false));
    }

    private void SaveCurrent(string name, OrderOfBattleVM vm)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            DisplayMessage("Preset name cannot be empty.", Colors.Red);
            return;
        }
        // Phase-1 stub. Codex review #36 P2 — Save persists a name-only HoNFormationPreset.
        // The HeroFormationAssignments / CaptainHeroIds / FormationClasses dicts stay empty
        // until the full capture path (reflection on OrderOfBattleVM._allHeroes + per-
        // formation Heroes collection) is implemented. Until then, presets persist by name
        // only and the user-visible "saved" state is honest about the stub status via the
        // delete + load message text.
        var preset = new HoNFormationPreset(name.Trim());
        var result = _presetService.SavePreset(preset);
        switch (result)
        {
            case SaveResult.Saved:
                DisplayMessage($"Preset \"{preset.Name}\" saved.", Colors.Yellow);
                break;
            case SaveResult.LimitReached:
                DisplayMessage("Preset limit reached. Delete one before saving.", Colors.Red);
                break;
            case SaveResult.NameInUse:
                DisplayMessage($"A preset named \"{preset.Name}\" already exists.", Colors.Red);
                break;
            default:
                DisplayMessage("Could not save preset.", Colors.Red);
                break;
        }
        UpdatePresetsButtonText();
    }

    private void UpdatePresetsButtonText()
    {
        var count = _presetService.Presets.Count;
        PresetsButtonText = count > 0 ? $"Presets ({count})" : "Presets";
    }

    private static void DisplayMessage(string text, Color color)
    {
        InformationManager.DisplayMessage(new InformationMessage(text, color));
    }
}
