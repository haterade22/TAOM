using System;
using HarmonyLib;
using Bannerlord.UIExtenderEx;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionUI.Campaign;

// Campaign presentation stays behind the shared FactionUI movie hook.
internal static class CampaignPresentation
{
    private static UIExtender? _trees;
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
                EnsureEncyclopediaPatches();
                if (source is EncyclopediaUnitPageVM unit)
                    unit.NameText = EncyclopediaTroopNames.DisplayName(unit.NameText);
                if (source is EncyclopediaHeroPageVM || source is EncyclopediaUnitPageVM)
                    BindTree(source);
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

    private static void EnsureEncyclopediaPatches()
    {
        if (_trees != null)
            return;

        // Patching campaign tooltip methods at startup can run CampaignUIHelper before GameTexts.
        new Harmony("taom.factionui.encyclopedia")
            .PatchCategory(typeof(CampaignPresentation).Assembly, EncyclopediaTroopNames.PatchCategory);
        var extender = UIExtender.Create("TAOM.CampaignPresentation");
        extender.Register(new[] { typeof(EncyclopediaFamilyTreeMixin), typeof(EncyclopediaTroopTreeMixin) });
        extender.Enable();
        _trees = extender;
        LogInfo("Campaign encyclopedia bindings registered.");
    }

    private static void BindTree(ViewModel page)
    {
        // A page that opened the encyclopedia directly can predate the lazy registration.
        var runtime = AccessTools.Field(typeof(UIExtender), "_runtime").GetValue(_trees);
        var component = AccessTools.Field(runtime.GetType(), "ViewModelComponent").GetValue(runtime);
        AccessTools.Method(component.GetType(), "InitializeMixinsForVMInstance").Invoke(component, new object[] { page });
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
