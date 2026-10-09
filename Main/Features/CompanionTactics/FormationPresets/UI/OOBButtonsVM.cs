using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;
using SaveResult = TAOM.Features.CompanionTactics.FormationPresets.Models.SaveResult;

namespace TAOM.Features.CompanionTactics.FormationPresets.UI;

/// <summary>
/// View model for the OOB Save / Load preset overlay. Bound by OOBButtonsOverlay.xml.
/// Boundary class — captures the active OrderOfBattleVM from the tracker and runs CRUD
/// inquiries on top of <see cref="IFormationPresetService"/>. Save reads the layout and Load
/// applies it through <see cref="IOOBPresetApplier"/>.
/// </summary>
public sealed class OOBButtonsVM : ViewModel
{
    private readonly IFormationPresetService _presetService;
    private readonly IOrderOfBattleVMTracker _vmTracker;
    private readonly IOOBCaptainAutoAssigner _captainAutoAssigner;
    private readonly IOOBPresetApplier _presetApplier;
    private readonly IModLogger _logger;

    private bool _isVisible;
    private string _presetsButtonText = string.Empty;
    private string _assignHeroesText = string.Empty;

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

    [DataSourceProperty]
    public string AssignHeroesText
    {
        get => _assignHeroesText;
        set
        {
            if (_assignHeroesText != value)
            {
                _assignHeroesText = value;
                OnPropertyChangedWithValue(value, nameof(AssignHeroesText));
            }
        }
    }

    public OOBButtonsVM(
        IFormationPresetService presetService,
        IOrderOfBattleVMTracker vmTracker,
        IOOBCaptainAutoAssigner captainAutoAssigner,
        IOOBPresetApplier presetApplier,
        IModLogger logger)
    {
        _presetService = presetService;
        _vmTracker = vmTracker;
        _captainAutoAssigner = captainAutoAssigner;
        _presetApplier = presetApplier;
        _logger = logger;
        RefreshButtonTexts();
        IsVisible = true;
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        RefreshButtonTexts();
    }

    private void RefreshButtonTexts()
    {
        AssignHeroesText = new TextObject("{=taom_oob_assign_heroes}Assign Heroes").ToString();
        UpdatePresetsButtonText();
    }

    public void ExecuteAssignCharacters()
    {
        var vm = _vmTracker.Current;
        if (vm == null)
        {
            DisplayMessage(NoScreenText(), Colors.Red);
            return;
        }
        DisplayMessage(AutoAssignMessage(_captainAutoAssigner.AssignCaptains(vm)), Colors.Yellow);
    }

    private static string NoScreenText() =>
        new TextObject("{=taom_oob_no_screen}No Order of Battle screen detected.").ToString();

    private static string AutoAssignMessage(AutoAssignResult result) => result?.Status switch
    {
        AutoAssignStatus.Assigned => new TextObject("{=taom_oob_autoassign_done}Captains assigned: {COUNT}.")
            .SetTextVariable("COUNT", result.AssignedCount).ToString(),
        AutoAssignStatus.NotGeneral => new TextObject(
            "{=taom_oob_autoassign_not_general}Only the general of this battle can assign heroes.").ToString(),
        _ => new TextObject("{=taom_oob_autoassign_none}No hero suits an open captain slot.").ToString(),
    };

    public void ExecuteManagePresets()
    {
        var vm = _vmTracker.Current;
        if (vm == null)
        {
            DisplayMessage(NoScreenText(), Colors.Red);
            return;
        }
        // Vanilla's accept-captain path does extra work for a player who is not the general, so a preset is never applied then.
        if (!vm.IsPlayerGeneral)
        {
            DisplayMessage(new TextObject("{=taom_oob_preset_not_general}Only the general of this battle can use presets.").ToString(), Colors.Red);
            return;
        }

        var elements = new List<InquiryElement>
        {
            new InquiryElement("save", new TextObject("{=taom_oob_preset_save_option}Save Current Layout as Preset").ToString(), null),
        };

        foreach (var preset in _presetService.Presets)
        {
            elements.Add(new InquiryElement("load:" + preset.Id,
                new TextObject("{=taom_oob_preset_load_option}Load: {NAME}")
                    .SetTextVariable("NAME", PlainName(preset.Name)).ToString(), null));
        }
        foreach (var preset in _presetService.Presets)
        {
            elements.Add(new InquiryElement("delete:" + preset.Id,
                new TextObject("{=taom_oob_preset_delete_option}Delete: {NAME}")
                    .SetTextVariable("NAME", PlainName(preset.Name)).ToString(), null));
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            new TextObject("{=taom_oob_presets_title}Formation Presets").ToString(),
            new TextObject("{=taom_oob_presets_description}Save, load or delete a layout of formation types, captains and hero troops.").ToString(),
            elements,
            isExitShown: true,
            maxSelectableOptionCount: 1,
            minSelectableOptionCount: 0,
            affirmativeText: OkText(),
            negativeText: CancelText(),
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
            if (preset != null) LoadPreset(preset, vm);
            UpdatePresetsButtonText();
            return;
        }

        if (id.StartsWith("delete:"))
        {
            var presetId = id.Substring("delete:".Length);
            var preset = _presetService.GetPresetById(presetId);
            if (preset != null && _presetService.DeletePreset(presetId))
                DisplayMessage(new TextObject("{=taom_oob_preset_deleted}Preset \"{NAME}\" deleted.")
                    .SetTextVariable("NAME", PlainName(preset.Name)).ToString(), Colors.Yellow);
            UpdatePresetsButtonText();
            return;
        }
    }

    private void LoadPreset(HoNFormationPreset preset, OrderOfBattleVM vm)
    {
        var result = _presetApplier.Apply(vm, preset);
        DisplayMessage(new TextObject("{=taom_oob_preset_loaded}Preset \"{NAME}\" loaded. Formation types: {CLASSES}, captains: {CAPTAINS}, hero troops: {TROOPS}.")
            .SetTextVariable("NAME", PlainName(preset.Name))
            .SetTextVariable("CLASSES", result.ClassesSet)
            .SetTextVariable("CAPTAINS", result.CaptainsPlaced)
            .SetTextVariable("TROOPS", result.TroopsPlaced).ToString(), Colors.Yellow);
        if (result.Skipped > 0)
            DisplayMessage(new TextObject("{=taom_oob_preset_skipped}Saved assignments that do not fit this battle: {COUNT}.")
                .SetTextVariable("COUNT", result.Skipped).ToString(), Colors.Yellow);
    }

    private void ShowSavePrompt(OrderOfBattleVM vm)
    {
        InformationManager.ShowTextInquiry(new TextInquiryData(
            new TextObject("{=taom_oob_preset_save_title}Save Formation Preset").ToString(),
            new TextObject("{=taom_oob_preset_save_prompt}Enter a name for this preset:").ToString(),
            isAffirmativeOptionShown: true,
            isNegativeOptionShown: true,
            affirmativeText: new TextObject("{=taom_oob_preset_save_button}Save").ToString(),
            negativeText: CancelText(),
            affirmativeAction: name => SaveCurrent(name, vm),
            negativeAction: () => { },
            shouldInputBeObfuscated: false));
    }

    private void SaveCurrent(string name, OrderOfBattleVM vm)
    {
        name = PlainName(name).Trim();
        if (name.Length == 0)
        {
            DisplayMessage(new TextObject("{=taom_oob_preset_name_empty}Preset name cannot be empty.").ToString(), Colors.Red);
            return;
        }
        var preset = _presetApplier.Capture(vm, name);
        var result = _presetService.SavePreset(preset);
        switch (result)
        {
            case SaveResult.Saved:
                var counts = FormationPresetLayout.CountOf(preset);
                DisplayMessage(new TextObject("{=taom_oob_preset_saved}Preset \"{NAME}\" saved. Formation types: {CLASSES}, captains: {CAPTAINS}, hero troops: {TROOPS}.")
                    .SetTextVariable("NAME", PlainName(preset.Name))
                    .SetTextVariable("CLASSES", counts.Classes)
                    .SetTextVariable("CAPTAINS", counts.Captains)
                    .SetTextVariable("TROOPS", counts.Troops).ToString(), Colors.Yellow);
                break;
            case SaveResult.LimitReached:
                DisplayMessage(new TextObject("{=taom_oob_preset_limit}Preset limit reached. Delete one before saving.").ToString(), Colors.Red);
                break;
            case SaveResult.NameInUse:
                DisplayMessage(new TextObject("{=taom_oob_preset_name_in_use}A preset named \"{NAME}\" already exists.")
                    .SetTextVariable("NAME", PlainName(preset.Name)).ToString(), Colors.Red);
                break;
            default:
                DisplayMessage(new TextObject("{=taom_oob_preset_save_failed}Could not save the preset.").ToString(), Colors.Red);
                break;
        }
        UpdatePresetsButtonText();
    }

    private void UpdatePresetsButtonText()
    {
        var count = _presetService.Presets.Count;
        PresetsButtonText = count > 0
            ? new TextObject("{=taom_oob_presets_button_count}Presets ({COUNT})").SetTextVariable("COUNT", count).ToString()
            : new TextObject("{=taom_oob_presets_button}Presets").ToString();
    }

    // Vanilla's own str_ok and str_cancel (Native global_strings.xml and module_strings.xml), which the game already translates.
    private static string OkText() => new TextObject("{=oHaWR73d}Ok").ToString();

    private static string CancelText() => new TextObject("{=3CpNUnVl}Cancel").ToString();

    // The text processor reads a TextObject variable's string value again as markup, so braces in a preset name would
    // render as empty text. A new name loses them at save; a name saved by an older build loses them here, at display.
    private static string PlainName(string name) =>
        (name ?? string.Empty).Replace("{", string.Empty).Replace("}", string.Empty);

    private static void DisplayMessage(string text, Color color)
    {
        InformationManager.DisplayMessage(new InformationMessage(text, color));
    }
}
