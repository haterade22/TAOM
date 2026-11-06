using System;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The clan page (approved option C): the roster panel (native Members, Wars and Blood Feuds lists
/// and the clan lore behind tabs, six a page), the Fiefs gallery (three a page), and the clan
/// leader's own 3D model. The galleries page over the native lists the page has just built, in
/// native order, and never finalize them. The leader model is this mixin's own
/// <see cref="HeroViewModel"/>, filled from <c>Clan.Leader</c> as the native hero page fills its
/// own; a whole <c>EncyclopediaHeroPageVM</c> is never built for it.
/// </summary>
[ViewModelMixin("Refresh")]
internal sealed class EncyclopediaClanGalleryMixin : BaseViewModelMixin<EncyclopediaClanPageVM>
{
    internal const int MembersPerPage = 6;
    internal const int FiefsPerPage = 3;
    private bool _hasLeaderPortrait;

    [DataSourceProperty] public ClanRosterSectionsVM<HeroVM, EncyclopediaFactionVM> RosterSections { get; }
    [DataSourceProperty] public RelationshipGalleryVM<KingdomFiefCardVM> FiefGallery { get; }
    [DataSourceProperty] public HeroViewModel LeaderCharacter { get; }
    [DataSourceProperty] public string HistoryTitleText { get; }

    [DataSourceProperty]
    public bool HasLeaderPortrait
    {
        get => _hasLeaderPortrait;
        set
        {
            if (_hasLeaderPortrait == value) return;
            _hasLeaderPortrait = value;
            OnPropertyChangedWithValue(value, nameof(HasLeaderPortrait));
        }
    }

    public EncyclopediaClanGalleryMixin(EncyclopediaClanPageVM page) : base(page)
    {
        string empty = new TextObject("{=taom_ency_relations_empty}None known").ToString();
        RosterSections = new ClanRosterSectionsVM<HeroVM, EncyclopediaFactionVM>(empty, MembersPerPage);
        FiefGallery = new RelationshipGalleryVM<KingdomFiefCardVM>(empty, FiefsPerPage);
        LeaderCharacter = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
        HistoryTitleText = new TextObject("{=taom_ency_kingdom_history}History").ToString();
        // UIExtenderEx replays the native constructor's Refresh after creating this mixin.
    }

    public override void OnRefresh()
    {
        var page = ViewModel;
        if (page == null) return;
        RosterSections.SetSections(page.Members, page.MembersText, page.Enemies, page.EnemiesText,
            page.BloodFeuds, page.BloodFeudsText, page.InformationText, page.InfoText);
        FiefGallery.SetItems(page.Settlements.Select(s => new KingdomFiefCardVM(s)).ToList(),
            Enumerable.Empty<KingdomFiefCardVM>(), page.SettlementsText);
        // FillFrom returns early for a child or hidden hero, which would leave the reused model
        // showing nothing or the previous clan's leader: only a valid adult leader is filled.
        var leader = (page.Obj as Clan)?.Leader;
        HasLeaderPortrait = page.Leader != null && !page.Leader.IsChild && leader != null && FillLeader(leader);
    }

    // The native hero page's fill (v1.5.6 EncyclopediaHeroPageVM.Refresh): characteristic idle,
    // civilian for notables, and the horse, harness and weapon slots stripped.
    private bool FillLeader(Hero leader)
    {
        try
        {
            LeaderCharacter.FillFrom(leader, -1, leader.IsNotable, useCharacteristicIdleAction: true);
            if (LeaderCharacter.IsHidden) return false;
            LeaderCharacter.SetEquipment(EquipmentIndex.ArmorItemEndSlot, default);
            LeaderCharacter.SetEquipment(EquipmentIndex.HorseHarness, default);
            LeaderCharacter.SetEquipment(EquipmentIndex.NumAllWeaponSlots, default);
            return true;
        }
        catch (Exception e)
        {
            CampaignPresentation.LogError("Could not build the clan leader portrait", e);
            return false;
        }
    }

    public override void OnFinalize()
    {
        RosterSections.OnFinalize();
        FiefGallery.OnFinalize();
        LeaderCharacter.OnFinalize();
    }
}
