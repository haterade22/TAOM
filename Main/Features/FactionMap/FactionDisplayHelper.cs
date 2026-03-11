using System;
using System.Collections.Generic;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionMap.ViewModels;

namespace TAOM.Features.FactionMap;

public static class FactionDisplayHelper
{
    public static void ApplyResult(FactionSelectionVM vm, FactionSelectionResult result)
    {
        ClearLists(vm);

        if (!result.Found) return;

        vm.SelectedFactionName = result.FactionName;
        vm.SelectedFactionDesc = result.Description;
        vm.SelectedFactionPlayable = result.Playable;
        vm.SelectedHasCulture = result.HasCulture;
        vm.HasSelection = true;

        foreach (var trait in result.Traits)
            vm.FactionTraits.Add(new FactionTraitItemVM(trait));
        foreach (var bonus in result.Bonuses)
            vm.FactionBonuses.Add(new FactionBonusItemVM(bonus.Text, bonus.Positive));
        foreach (var perk in result.Perks)
            vm.FactionPerks.Add(new FactionPerkItemVM(perk.Name, perk.Description));
        foreach (var s in result.Strengths)
            vm.FactionStrengths.Add(new FactionBonusItemVM("+ " + s, true));
        foreach (var w in result.Weaknesses)
            vm.FactionWeaknesses.Add(new FactionBonusItemVM("- " + w, false));

        vm.OnPropertyChanged(nameof(FactionSelectionVM.HasStrengths));
        vm.OnPropertyChanged(nameof(FactionSelectionVM.HasWeaknesses));

        if (result.SpecialUnit != null && !string.IsNullOrEmpty(result.SpecialUnit.Name))
        {
            vm.SpecialUnitName = result.SpecialUnit.Name;
            vm.SpecialUnitDesc = result.SpecialUnit.Description;
            vm.HasSpecialUnit = true;
        }
        else
        {
            vm.SpecialUnitName = "";
            vm.SpecialUnitDesc = "";
            vm.HasSpecialUnit = false;
        }

        vm.SelectedFactionSide = result.Side;
        vm.Difficulty = result.Difficulty;
        vm.DifficultyText = result.DifficultyText;
        vm.FactionImageId = result.ImageId;
        vm.FactionColorHex = result.DarkPanelHex;
        vm.FactionAccentColorHex = result.AccentColorHex;
        vm.BannerPosX = result.BannerPosX;
        vm.BannerPosY = result.BannerPosY;
        vm.BannerColorHex = result.BannerColorHex;
        vm.BannerSide = result.BannerSide;
        vm.BannerImage = result.BannerImage;
    }

    public static void LoadAllLandmarks(FactionSelectionVM vm, ILandmarkService landmarkService)
    {
        vm.AllLandmarks.Clear();
        foreach (var landmark in landmarkService.GetCapitals())
            vm.AllLandmarks.Add(new LandmarkItemVM(landmark));
    }

    public static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7)
            return new Color(1f, 1f, 1f, 1f);
        hex = hex.TrimStart('#');
        try
        {
            float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float a = hex.Length >= 8 ? int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f : 1f;
            return new Color(r, g, b, a);
        }
        catch { return new Color(1f, 1f, 1f, 1f); }
    }

    public static void ShowHoverTooltip(HoverStateChange change)
    {
        if (change.ShouldShow)
        {
            var color = ParseColor(change.ColorHex);
            var props = new List<TooltipProperty>
            {
                new TooltipProperty(change.FactionName, "", 0, color, false, TooltipProperty.TooltipPropertyFlags.Title)
            };
            InformationManager.ShowTooltip(typeof(List<TooltipProperty>), props);
        }
        else
        {
            InformationManager.HideTooltip();
        }
    }

    public static void Finalize(FactionSelectionVM vm)
    {
        InformationManager.HideTooltip();
        ClearLists(vm);
        vm.AllLandmarks.Clear();
    }

    private static void ClearLists(FactionSelectionVM vm)
    {
        vm.FactionTraits.Clear();
        vm.FactionBonuses.Clear();
        vm.FactionPerks.Clear();
        vm.FactionStrengths.Clear();
        vm.FactionWeaknesses.Clear();
        vm.FactionLandmarks.Clear();
    }
}
