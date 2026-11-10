using System;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Client.Features.Realms.Encyclopedia;

// The view model side of the title sections on the encyclopedia pages (see EncyclopediaTitlePrefabs.cs). Each mixin is
// refreshed with its page: UIExtenderEx runs OnRefresh after the page's own Refresh, which the page constructor calls,
// so the constructors here do not refresh. The page's object (the settlement, hero or kingdom) is EncyclopediaPageVM.Obj.
// Every property name starts with "Realms" so it cannot clash with the engine's or TAOM's own bindings. The rows open the
// title tree or claimants popups (UI/Popups) or another encyclopedia page.

internal static class EncyclopediaTitles
{
    /// <summary>The title behavior with its titles created, or null while titles are off or there is no campaign.</summary>
    public static RealmsCampaign? Behavior()
    {
        var b = RealmsCampaign.Current;
        return b != null && b.EnsureInitialized() ? b : null;
    }

    public static void LogOnce(ref bool logged, string site, Exception ex)
    {
        if (logged)
            return;
        logged = true;
        Debug.Print($"[TAOM.Realms] encyclopedia {site} threw (logged once per page): {ex}");
    }

    public static string Text(string id) => new TextObject(id).ToString();
}

/// <summary>
/// The state of a one-title section (settlement or kingdom page): the title, its name, its rows. Each mixin declares its
/// own bindings and forwards here, so UIExtenderEx sees every binding on the mixin type itself.
/// </summary>
internal sealed class TitleSection
{
    private readonly Action<bool, string> _changed;
    private bool _logged;

    public TitleSection(Action<bool, string> changed) => _changed = changed;

    public string? TitleId { get; private set; }
    public bool HasTitle { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MBBindingList<TitleRowVM> Rows { get; } = new();

    public void Refresh(string site, Func<RealmsCampaign, TitleDefinition?> find)
    {
        try
        {
            var b = EncyclopediaTitles.Behavior();
            var title = b != null ? find(b) : null;
            TitleId = title?.Id;
            if (title != null)
                TitleRows.ForTitle(Rows, b!, title, () => Refresh(site, find));
            else
                Rows.Clear();
            Set(title != null, title != null ? TitleNames.Of(title, b!) : string.Empty);
        }
        catch (Exception ex)
        {
            Set(false, Name);
            EncyclopediaTitles.LogOnce(ref _logged, site, ex);
        }
    }

    /// <summary>The title name: opens the title's own encyclopedia page.</summary>
    public void OpenTitle()
    {
        if (TitleId != null)
            UiQueue.Post(TitleRows.Page(TitleId));
    }

    private void Set(bool hasTitle, string name)
    {
        var changed = hasTitle != HasTitle || name != Name;
        HasTitle = hasTitle;
        Name = name;
        if (changed)
            _changed(hasTitle, name);
    }
}

/// <summary>The "Title" section under the owner on the settlement page.</summary>
[ViewModelMixin("Refresh")]
internal sealed class SettlementTitleMixin : BaseViewModelMixin<EncyclopediaSettlementPageVM>
{
    private readonly TitleSection _section;

    public SettlementTitleMixin(EncyclopediaSettlementPageVM viewModel) : base(viewModel)
    {
        RealmsTitleHeaderText = EncyclopediaTitles.Text("{=realms_enc_title}Title");
        _section = new TitleSection((has, name) =>
        {
            OnPropertyChangedWithValue(name, nameof(RealmsTitleName));
            OnPropertyChangedWithValue(has, nameof(RealmsHasTitle));
        });
    }

    [DataSourceProperty] public string RealmsTitleHeaderText { get; }
    [DataSourceProperty] public bool RealmsHasTitle => _section.HasTitle;
    [DataSourceProperty] public string RealmsTitleName => _section.Name;
    [DataSourceProperty] public MBBindingList<TitleRowVM> RealmsTitleRows => _section.Rows;

    public override void OnRefresh() => _section.Refresh("settlement page", b =>
        ViewModel?.Obj is Settlement settlement ? b.Service.Catalog.ForSettlement(settlement.StringId) : null);

    [DataSourceMethod]
    public void ExecuteRealmsOpenTitle() => _section.OpenTitle();
}

/// <summary>The realm title above the description on the kingdom page.</summary>
[ViewModelMixin("Refresh")]
internal sealed class KingdomTitleMixin : BaseViewModelMixin<EncyclopediaFactionPageVM>
{
    private readonly TitleSection _section;

    public KingdomTitleMixin(EncyclopediaFactionPageVM viewModel) : base(viewModel)
    {
        RealmsRealmTitleLabel = EncyclopediaTitles.Text("{=realms_enc_realm_title}Realm title");
        _section = new TitleSection((has, name) =>
        {
            OnPropertyChangedWithValue(name, nameof(RealmsTitleName));
            OnPropertyChangedWithValue(has, nameof(RealmsHasTitle));
        });
    }

    [DataSourceProperty] public string RealmsRealmTitleLabel { get; }
    [DataSourceProperty] public bool RealmsHasTitle => _section.HasTitle;
    [DataSourceProperty] public string RealmsTitleName => _section.Name;
    [DataSourceProperty] public MBBindingList<TitleRowVM> RealmsTitleRows => _section.Rows;

    public override void OnRefresh() => _section.Refresh("kingdom page", b =>
    {
        var kingdomId = (ViewModel?.Obj as Kingdom)?.StringId;
        return kingdomId == null ? null : b.Service.Catalog.All.FirstOrDefault(t => t.Rank == TitleRank.Kingdom && t.KingdomId == kingdomId);
    });

    [DataSourceMethod]
    public void ExecuteRealmsOpenTitle() => _section.OpenTitle();
}

/// <summary>The "Titles" section after the clan section on the hero page: titles held by right, then claims.</summary>
[ViewModelMixin("Refresh")]
internal sealed class HeroTitlesMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
{
    private bool _hasTitles;
    private bool _logged;

    public HeroTitlesMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
    {
        RealmsTitlesHeaderText = EncyclopediaTitles.Text("{=realms_enc_titles}Titles");
        RealmsTitleRows = new MBBindingList<TitleRowVM>();
    }

    [DataSourceProperty] public string RealmsTitlesHeaderText { get; }
    [DataSourceProperty] public MBBindingList<TitleRowVM> RealmsTitleRows { get; }

    [DataSourceProperty]
    public bool RealmsHasTitles
    {
        get => _hasTitles;
        set { if (_hasTitles != value) { _hasTitles = value; OnPropertyChangedWithValue(value, nameof(RealmsHasTitles)); } }
    }

    public override void OnRefresh()
    {
        try
        {
            var b = EncyclopediaTitles.Behavior();
            if (b != null && ViewModel?.Obj is Hero hero)
            {
                RealmsHasTitles = TitleRows.ForHero(RealmsTitleRows, b, hero);
                return;
            }
            RealmsTitleRows.Clear();
            RealmsHasTitles = false;
        }
        catch (Exception ex)
        {
            RealmsHasTitles = false;
            EncyclopediaTitles.LogOnce(ref _logged, "hero page", ex);
        }
    }
}
