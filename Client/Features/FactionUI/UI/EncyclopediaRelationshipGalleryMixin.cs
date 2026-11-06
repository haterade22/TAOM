using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.UI;

[ViewModelMixin("Refresh")]
internal sealed class EncyclopediaRelationshipGalleryMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
{
    [DataSourceProperty] public RelationshipGalleryVM<HeroVM> FriendsGallery { get; }
    [DataSourceProperty] public RelationshipGalleryVM<HeroVM> EnemiesGallery { get; }

    public EncyclopediaRelationshipGalleryMixin(EncyclopediaHeroPageVM page) : base(page)
    {
        string empty = new TextObject("{=taom_ency_relations_empty}None known").ToString();
        FriendsGallery = new RelationshipGalleryVM<HeroVM>(empty);
        EnemiesGallery = new RelationshipGalleryVM<HeroVM>(empty);
        // UIExtenderEx replays the native constructor's Refresh after creating this mixin.
    }

    public override void OnRefresh()
    {
        var page = ViewModel;
        if (page == null) return;
        FriendsGallery.SetItems(page.Allies, page.AdditionalAllies, page.AlliesText);
        EnemiesGallery.SetItems(page.Enemies, page.AdditionalEnemies, page.EnemiesText);
    }

    public override void OnFinalize()
    {
        FriendsGallery.OnFinalize();
        EnemiesGallery.OnFinalize();
    }
}
