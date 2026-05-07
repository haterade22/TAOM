using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Features.EquipPresets.UI;

/// <summary>
/// Datasource for <c>PresetsOverlay.xml</c>. Exposes <see cref="ButtonText"/> +
/// <see cref="ExecuteOpenPresets"/>. The xaml-equivalent prefab binds the button click and
/// label only — all interaction logic lives here.
/// </summary>
public sealed class PresetsOverlayVM : ViewModel
{
    private readonly IEquipmentPresetService _service;
    private readonly IInventoryScreenAdapter _screen;
    private readonly IEquipPresetsSettingsProvider _settings;
    private readonly IModLogger _logger;

    private string _buttonText = "Presets";

    public PresetsOverlayVM(
        IEquipmentPresetService service,
        IInventoryScreenAdapter screen,
        IEquipPresetsSettingsProvider settings,
        IModLogger logger)
    {
        _service = service;
        _screen = screen;
        _settings = settings;
        _logger = logger;
    }

    [DataSourceProperty]
    public string ButtonText
    {
        get => _buttonText;
        set
        {
            if (value != _buttonText)
            {
                _buttonText = value;
                OnPropertyChangedWithValue(value, nameof(ButtonText));
            }
        }
    }

    /// <summary>Bound to <c>Command.Click</c> on the prefab. Opens the preset menu.</summary>
    public void ExecuteOpenPresets()
    {
        try
        {
            OpenPresetMenu();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EquipPresets] OpenPresetMenu failed: {ex.Message}");
        }
    }

    private void OpenPresetMenu()
    {
        if (!_settings.IsEnabled) return;
        if (!_screen.IsAvailable) return;
        var heroId = _screen.ActiveHeroStringId;
        if (string.IsNullOrEmpty(heroId))
        {
            DisplayInfo(new TextObject("{=taom_ep_no_hero}No active hero on the inventory screen.").ToString());
            return;
        }

        var presets = _service.GetPresets(heroId);
        var options = new List<InquiryElement>
        {
            new InquiryElement("save", new TextObject("{=taom_ep_save}Save New").ToString(), null, true,
                new TextObject("{=taom_ep_save_hint}Save current equipment as a new preset.").ToString()),
        };
        if (presets.Count > 0)
        {
            options.Add(new InquiryElement("load", new TextObject("{=taom_ep_load}Load").ToString(), null, true,
                new TextObject("{=taom_ep_load_hint}Equip a saved preset.").ToString()));
            options.Add(new InquiryElement("update", new TextObject("{=taom_ep_update}Update").ToString(), null, true,
                new TextObject("{=taom_ep_update_hint}Overwrite a saved preset with current equipment.").ToString()));
            options.Add(new InquiryElement("delete", new TextObject("{=taom_ep_delete}Delete").ToString(), null, true,
                new TextObject("{=taom_ep_delete_hint}Remove a preset.").ToString()));
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            new TextObject("{=taom_ep_title}Equipment Presets").ToString(),
            FormatHeader(heroId!, presets.Count),
            options, true, 1, 1,
            new TextObject("{=taom_ep_ok}OK").ToString(),
            new TextObject("{=taom_ep_cancel}Cancel").ToString(),
            chosen =>
            {
                if (chosen == null || chosen.Count == 0) return;
                var key = (string)chosen[0].Identifier;
                switch (key)
                {
                    case "save":   PromptSaveName(heroId!); break;
                    case "load":   PromptSelectPreset(heroId!, "load"); break;
                    case "update": PromptSelectPreset(heroId!, "update"); break;
                    case "delete": PromptSelectPreset(heroId!, "delete"); break;
                }
            },
            _ => { }));
    }

    private string FormatHeader(string heroStringId, int count) =>
        new TextObject("{=taom_ep_header}Hero: {HERO}  Presets: {COUNT}/{MAX}")
            .SetTextVariable("HERO", heroStringId)
            .SetTextVariable("COUNT", count)
            .SetTextVariable("MAX", _settings.MaxPresetsPerCharacter)
            .ToString();

    private void PromptSaveName(string heroStringId)
    {
        // H5 fix (Codex review 2026-05-07): the prompt copy promises "save current equipment as a
        // preset", so capture both battle AND civilian sets. The previous logic only captured
        // civilian when the screen was already in civilian mode — saving a civilian outfit while
        // viewing the civilian tab was silently snapshotting hidden battle gear too. Now: a single
        // preset always represents the hero's complete equipment (battle + civilian + mount).
        // Per-set Apply on Load is still gated by IncludesCivilianEquipment / IncludesMount.
        InformationManager.ShowTextInquiry(new TextInquiryData(
            new TextObject("{=taom_ep_save_title}Save Preset").ToString(),
            new TextObject("{=taom_ep_save_msg}Enter a name. The preset captures your current battle and civilian equipment, plus mount.").ToString(),
            true, true,
            new TextObject("{=taom_ep_ok}OK").ToString(),
            new TextObject("{=taom_ep_cancel}Cancel").ToString(),
            name =>
            {
                var outcome = _service.SaveCurrent(heroStringId, name, includeMount: true, includeCivilian: true);
                AnnounceSaveOutcome(outcome, name);
            },
            () => { }));
    }

    private void PromptSelectPreset(string heroStringId, string action)
    {
        var presets = _service.GetPresets(heroStringId);
        if (presets.Count == 0) return;
        var elements = new List<InquiryElement>(presets.Count);
        foreach (var p in presets)
        {
            elements.Add(new InquiryElement(p.Id, p.Name, null, true, FormatPresetSummary(p)));
        }

        var title = action switch
        {
            "load"   => new TextObject("{=taom_ep_select_load}Load Preset").ToString(),
            "update" => new TextObject("{=taom_ep_select_update}Update Preset").ToString(),
            "delete" => new TextObject("{=taom_ep_select_delete}Delete Preset").ToString(),
            _ => new TextObject("{=taom_ep_select}Select Preset").ToString(),
        };

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            title, string.Empty, elements, true, 1, 1,
            new TextObject("{=taom_ep_ok}OK").ToString(),
            new TextObject("{=taom_ep_cancel}Cancel").ToString(),
            chosen =>
            {
                if (chosen == null || chosen.Count == 0) return;
                var presetId = (string)chosen[0].Identifier;
                switch (action)
                {
                    case "load":   LoadAndAnnounce(heroStringId, presetId); break;
                    case "update": UpdateAndAnnounce(heroStringId, presetId); break;
                    case "delete": DeleteAndAnnounce(heroStringId, presetId); break;
                }
            },
            _ => { }));
    }

    private void LoadAndAnnounce(string heroStringId, string presetId)
    {
        var report = _service.LoadPresetWithReport(heroStringId, presetId);
        if (report.Disabled || report.NotFound || report.NoActiveHero)
        {
            DisplayInfo(new TextObject("{=taom_ep_load_failed}Load failed.").ToString());
            return;
        }
        _screen.RefreshActiveVM();
        if (report.HasIssues)
        {
            var msg = new TextObject("{=taom_ep_load_partial}Loaded {EQUIPPED} item(s). Missing items: {MISS_ITEMS}. Missing modifiers: {MISS_MODS}.")
                .SetTextVariable("EQUIPPED", report.EquippedCount)
                .SetTextVariable("MISS_ITEMS", report.MissingItems.Count)
                .SetTextVariable("MISS_MODS", report.MissingModifiers.Count)
                .ToString();
            InformationManager.DisplayMessage(new InformationMessage(msg, Colors.Yellow));
        }
        else
        {
            DisplayInfo(new TextObject("{=taom_ep_load_ok}Preset loaded ({COUNT} items).")
                .SetTextVariable("COUNT", report.EquippedCount).ToString());
        }
    }

    private void UpdateAndAnnounce(string heroStringId, string presetId)
    {
        var outcome = _service.UpdatePreset(heroStringId, presetId);
        DisplayInfo(outcome switch
        {
            UpdateOutcome.Updated     => new TextObject("{=taom_ep_updated}Preset updated.").ToString(),
            UpdateOutcome.NotFound    => new TextObject("{=taom_ep_notfound}Preset not found.").ToString(),
            UpdateOutcome.Disabled    => new TextObject("{=taom_ep_disabled}Equipment Presets are disabled in MCM.").ToString(),
            UpdateOutcome.NoActiveHero=> new TextObject("{=taom_ep_no_hero}No active hero on the inventory screen.").ToString(),
            _ => string.Empty,
        });
    }

    private void DeleteAndAnnounce(string heroStringId, string presetId)
    {
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_ep_delete_title}Delete Preset").ToString(),
            new TextObject("{=taom_ep_delete_msg}Are you sure you want to delete this preset?").ToString(),
            true, true,
            new TextObject("{=taom_ep_yes}Yes").ToString(),
            new TextObject("{=taom_ep_no}No").ToString(),
            () =>
            {
                var outcome = _service.DeletePreset(heroStringId, presetId);
                DisplayInfo(outcome switch
                {
                    DeleteOutcome.Deleted  => new TextObject("{=taom_ep_deleted}Preset deleted.").ToString(),
                    DeleteOutcome.NotFound => new TextObject("{=taom_ep_notfound}Preset not found.").ToString(),
                    DeleteOutcome.Disabled => new TextObject("{=taom_ep_disabled}Equipment Presets are disabled in MCM.").ToString(),
                    _ => string.Empty,
                });
            },
            () => { }));
    }

    private void AnnounceSaveOutcome(SaveOutcome outcome, string name)
    {
        switch (outcome)
        {
            case SaveOutcome.Saved:
                DisplayInfo(new TextObject("{=taom_ep_saved}Preset '{NAME}' saved.")
                    .SetTextVariable("NAME", name).ToString());
                break;
            case SaveOutcome.MaxReached:
                DisplayInfo(new TextObject("{=taom_ep_max}Max presets reached ({MAX}). Delete one to save a new preset.")
                    .SetTextVariable("MAX", _settings.MaxPresetsPerCharacter).ToString());
                break;
            case SaveOutcome.Disabled:
                DisplayInfo(new TextObject("{=taom_ep_disabled}Equipment Presets are disabled in MCM.").ToString());
                break;
            case SaveOutcome.NoActiveHero:
                DisplayInfo(new TextObject("{=taom_ep_no_hero}No active hero on the inventory screen.").ToString());
                break;
            case SaveOutcome.InvalidName:
                DisplayInfo(new TextObject("{=taom_ep_bad_name}Preset name cannot be empty.").ToString());
                break;
        }
    }

    private static string FormatPresetSummary(HoNEquipmentPreset p) =>
        new TextObject("{=taom_ep_psummary}Battle: {B}  Civilian: {C}  Mount: {M}")
            .SetTextVariable("B", p.Items.Count)
            .SetTextVariable("C", p.IncludesCivilianEquipment ? p.CivilianItems.Count : 0)
            .SetTextVariable("M", p.IncludesMount ? "yes" : "no")
            .ToString();

    private static void DisplayInfo(string msg) =>
        InformationManager.DisplayMessage(new InformationMessage(msg));
}
