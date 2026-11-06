using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>
/// The themed campaign and encyclopedia screens behind Patch95's movie hook, which reaches this class through
/// ICampaignPresentationAdapter (ADR-013: everything here names a client-only assembly). The two encyclopedia tree
/// mixins register with every other TAOM extension in ClientSubModule.OnGameStart; they stay inert until a themed
/// prefab binds their properties.
/// </summary>
internal static class CampaignPresentation
{
    private static bool _clockPatched;

    internal static string ModuleRoot => IoC.Resolve<FactionUIPaths>().Root;
    internal static void LogInfo(string message) => IoC.Resolve<IModLogger>().LogInfo("[FactionUI] " + message);
    internal static void LogError(string message, Exception error) => IoC.Resolve<IModLogger>().LogError($"[FactionUI] {message} {error}");

    internal static ViewModel? Bind(string movie, ViewModel? source)
    {
        switch (movie)
        {
            case "TAOMEncyclopediaBar":
            case "TAOMEncyclopediaHome":
            case "TAOMEncyclopediaItemList":
            case "TAOMEncyclopediaHeroPage":
            case "TAOMEncyclopediaUnitPage":
                EnableTroopNames();
                if (source is EncyclopediaUnitPageVM unit)
                    unit.NameText = EncyclopediaTroopNames.DisplayName(unit.NameText);
                if (movie == "TAOMEncyclopediaItemList" && source != null)
                    return new EncyclopediaListScreenVM(source);
                break;
            case "TAOMCharacterDeveloper" when source != null:
                return new CharacterScreenVM(source);
            case "TAOMMapBar" when source is MapBarVM map:
                if (!_clockPatched)
                {
                    new Harmony("taom.factionui.campaign-time")
                        .CreateClassProcessor(typeof(CampaignTimeHintPatch)).Patch();
                    _clockPatched = true;
                }
                CampaignTimeHintPatch.Enabled = true;
                map.MapInfo.IsInfoBarExtended = false;
                break;
        }

        return source;
    }

    // The TAOM.EncyclopediaTroopNames category is applied with the GameInit batch (SubModule.cs) and does nothing until
    // here, the first themed encyclopedia movie, where the category itself used to be applied.
    private static void EnableTroopNames()
    {
        if (EncyclopediaTroopNames.Active)
            return;

        EncyclopediaTroopNames.Active = true;
        LogInfo("Campaign encyclopedia troop names enabled.");
    }

    internal static void OnLoaded(GauntletMovieIdentifier movie, ViewModel? source)
    {
        var root = movie.Movie?.RootWidget;
        if (root == null)
            return;

        if (source is EncyclopediaListScreenVM list)
            foreach (var widget in root.FindChildrenWithType<EncyclopediaListScreenWidget>(true))
                widget.Model = list;
        else if (source is CharacterScreenVM character)
            foreach (var widget in root.FindChildrenWithType<CharacterScreenWidget>(true))
                widget.Model = character;
        else if (movie.MovieName == "TAOMEncyclopediaSettlementPage" && source is EncyclopediaSettlementPageVM page &&
            page.Obj is Settlement settlement && root.FindChild("SettlementIllustration", true) is EncyclopediaPictureWidget picture)
        {
            string kind = settlement.IsVillage ? "village" : settlement.IsCastle ? "castle" : "town";
            picture.PictureFile = EncyclopediaPictureCache.ResolveSettlement(settlement.StringId, settlement.Culture?.StringId ?? "", kind);
        }
        else if (EncyclopediaPageLayoutWidget.IsDetailMovie(movie.MovieName))
            EncyclopediaPageLayoutWidget.Attach(root, movie.MovieName == "EncyclopediaHeroPage");
    }
}
