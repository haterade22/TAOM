using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The kingdom page's horizontal galleries, three cards a page: the Clans panel (clans, wars,
/// alliances and trade agreements behind tabs) and the Fiefs panel. Both page independently over
/// the native lists the page has just built, in the native order.
/// </summary>
[ViewModelMixin("Refresh")]
internal sealed class EncyclopediaKingdomGalleryMixin : BaseViewModelMixin<EncyclopediaFactionPageVM>
{
    internal const int CardsPerPage = 3;

    [DataSourceProperty] public KingdomRelationSectionsVM<EncyclopediaFactionVM> ClanSections { get; }
    [DataSourceProperty] public RelationshipGalleryVM<KingdomFiefCardVM> FiefGallery { get; }
    [DataSourceProperty] public string HistoryTitleText { get; }

    public EncyclopediaKingdomGalleryMixin(EncyclopediaFactionPageVM page) : base(page)
    {
        string empty = new TextObject("{=taom_ency_relations_empty}None known").ToString();
        ClanSections = new KingdomRelationSectionsVM<EncyclopediaFactionVM>(empty, CardsPerPage);
        FiefGallery = new RelationshipGalleryVM<KingdomFiefCardVM>(empty, CardsPerPage);
        HistoryTitleText = new TextObject("{=taom_ency_kingdom_history}History").ToString();
        // UIExtenderEx replays the native constructor's Refresh after creating this mixin.
    }

    public override void OnRefresh()
    {
        var page = ViewModel;
        if (page == null) return;
        ClanSections.SetSections(page.Clans, page.ClansText, page.Enemies, page.EnemiesText,
            page.Alliances, page.AlliancesText, page.TradeAgreements, page.TradeAgreementsText);
        FiefGallery.SetItems(page.Settlements.Select(s => new KingdomFiefCardVM(s)).ToList(),
            Enumerable.Empty<KingdomFiefCardVM>(), page.SettlementsText);
    }

    public override void OnFinalize()
    {
        ClanSections.OnFinalize();
        FiefGallery.OnFinalize();
    }
}
