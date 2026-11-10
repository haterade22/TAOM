using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FactionUI.Campaign;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Titles;
using TAOM.Client.Features.Realms.Popups;

namespace TAOM.Client.Features.Realms.Encyclopedia;

/// <summary>
/// The page of one title (RealmsEncyclopediaTitlePage.xml), every rank alike: its banner, name and rank, the holder by
/// right (portrait, linked), the seat (the fief of a fief title, else the realm's capital or the holder's best fief beneath
/// it), the liege title and the vassal titles (each opening its own page), claims, laws, offices, the chronicle and the
/// player's title actions (buy a claim, usurp, grant, revoke, form, restore, appoint, dismiss) with their price or the
/// reason they are refused. An action rebuilds the page once it is done.
/// </summary>
[EncyclopediaViewModel(typeof(RealmsTitle))]
internal sealed class TitleEncyclopediaPageVM : EncyclopediaContentPageVM
{
    private readonly string _titleId;
    private string _nameText = string.Empty;
    private string _rankText = string.Empty;
    private string _holderText = string.Empty;
    private bool _hasHolder;
    private HeroVM? _holder;
    private BannerImageIdentifierVM? _banner;
    private TitleSeatVM? _seat;
    private bool _hasSeat;
    private string _seatHeading = string.Empty;
    private bool _hasVassals;
    private bool _hasActions;
    private bool _hasChronicle;
    private bool _finalized;

    public TitleEncyclopediaPageVM(EncyclopediaPageArgs args) : base(args)
    {
        _titleId = (args.Obj as RealmsTitle)?.StringId ?? string.Empty;
        InfoRows = new MBBindingList<TitleRowVM>();
        Vassals = new MBBindingList<TitleLinkVM>();
        Actions = new MBBindingList<TitleActionVM>();
        Chronicle = new MBBindingList<TitleRowVM>();
        RefreshValues();
    }

    [DataSourceProperty] public string HolderHeading => Text("{=realms_ui_holder}Held by right");
    [DataSourceProperty] public string VacantText => Text("{=realms_page_vacant}Vacant");
    [DataSourceProperty] public string VassalsHeading => Text("{=realms_ui_vassals}Vassal titles");
    [DataSourceProperty] public string NoVassalsText => Text("{=realms_page_no_vassals}No titles below it.");
    [DataSourceProperty] public string NoSeatText => Text("{=realms_page_no_seat}No fief of its own.");
    [DataSourceProperty] public string ActionsHeading => Text("{=realms_page_actions}Your actions");
    [DataSourceProperty] public string NoActionsText => Text("{=realms_page_no_actions}Nothing you can do with this title now.");
    [DataSourceProperty] public string ChronicleHeading => Text("{=realms_ui_chronicle}Chronicle");
    [DataSourceProperty] public string NoChronicleText => Text("{=realms_page_no_chronicle}No holders recorded yet.");
    [DataSourceProperty] public string TreeButtonText => Text("{=realms_page_tree}Title tree");

    [DataSourceProperty] public MBBindingList<TitleRowVM> InfoRows { get; }
    [DataSourceProperty] public MBBindingList<TitleLinkVM> Vassals { get; }
    [DataSourceProperty] public MBBindingList<TitleActionVM> Actions { get; }
    [DataSourceProperty] public MBBindingList<TitleRowVM> Chronicle { get; }

    [DataSourceProperty]
    public string NameText
    {
        get => _nameText;
        set { if (value != _nameText) { _nameText = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public string RankText
    {
        get => _rankText;
        set { if (value != _rankText) { _rankText = value; OnPropertyChangedWithValue(value); } }
    }

    /// <summary>The holder styled by the title ("Aran Gil-galad"), or "Vacant".</summary>
    [DataSourceProperty]
    public string HolderText
    {
        get => _holderText;
        set { if (value != _holderText) { _holderText = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public bool HasHolder
    {
        get => _hasHolder;
        set { if (value != _hasHolder) { _hasHolder = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public HeroVM? Holder
    {
        get => _holder;
        set { if (value != _holder) { _holder = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public BannerImageIdentifierVM? Banner
    {
        get => _banner;
        set { if (value != _banner) { _banner = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public TitleSeatVM? Seat
    {
        get => _seat;
        set { if (value != _seat) { _seat = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public bool HasSeat
    {
        get => _hasSeat;
        set { if (value != _hasSeat) { _hasSeat = value; OnPropertyChangedWithValue(value); } }
    }

    /// <summary>"Fief" for a fief title, "Seat" above it.</summary>
    [DataSourceProperty]
    public string SeatHeading
    {
        get => _seatHeading;
        set { if (value != _seatHeading) { _seatHeading = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public bool HasVassals
    {
        get => _hasVassals;
        set { if (value != _hasVassals) { _hasVassals = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public bool HasActions
    {
        get => _hasActions;
        set { if (value != _hasActions) { _hasActions = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public bool HasChronicle
    {
        get => _hasChronicle;
        set { if (value != _hasChronicle) { _hasChronicle = value; OnPropertyChangedWithValue(value); } }
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        Refresh();
    }

    /// <summary>Rebuilds the page; also runs after an action, when the page may already have been left.</summary>
    public override void Refresh()
    {
        if (_finalized)
            return;
        IsLoadingOver = false;
        try
        {
            Build();
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] building the page of title {_titleId} threw: {ex}");
        }
        IsLoadingOver = true;
    }

    public override string GetName() => NameText;

    public override string GetNavigationBarURL()
    {
        var home = HyperlinkTexts.GetGenericHyperlinkText("Home", GameTexts.FindText("str_encyclopedia_home").ToString());
        var titles = HyperlinkTexts.GetGenericHyperlinkText($"ListPage-{nameof(RealmsTitle)}",
            new TextObject("{=realms_ency_titles}Titles").ToString());
        return $"{home} \\ {titles} \\ {GetName()}";
    }

    public void ExecuteOpenTree() => UiQueue.Post(() => RealmsPopups.OpenTree(_titleId));

    public override void OnFinalize()
    {
        base.OnFinalize();
        _finalized = true;
        Clear();
    }

    private void Build()
    {
        Clear();
        var b = TitleEncyclopediaPage.Behavior();
        var title = b?.Service.Catalog.Get(_titleId);
        if (b == null || title == null)
        {
            NameText = _titleId;
            RankText = string.Empty;
            HolderText = VacantText;
            return;
        }

        var catalog = b.Service.Catalog;
        var ledger = b.Service.Ledger;
        NameText = TitleNames.Of(title, b);
        RankText = RankLine(title);
        Banner = new BannerImageIdentifierVM(TitleVisuals.Banner(b, title), nineGrid: true);

        var holderId = ledger.HolderOf(title.Id);
        var holder = holderId != null ? Hero.Find(holderId) : null;
        HasHolder = holder != null;
        Holder = holder != null ? new HeroVM(holder, useCivilian: true) : null;
        HolderText = holderId != null ? TitleText.HolderName(b, title) : VacantText;

        var seat = SeatOf(b, title);
        SeatHeading = Text(title.SettlementId != null ? "{=realms_page_fief}Fief" : "{=realms_page_seat}Seat");
        HasSeat = seat != null;
        Seat = seat != null ? new TitleSeatVM(seat) : null;

        // Liege first: the line a title is read by.
        var liege = catalog.Get(title.ParentId);
        if (liege != null)
            InfoRows.Add(new TitleRowVM(Text("{=realms_ui_liege}Liege title"), TitleNames.Of(liege, b), TitlePage(liege.Id)));
        if (Realm(title.KingdomId) is { } kingdom && title.Rank != TitleRank.Kingdom)
            InfoRows.Add(new TitleRowVM(Text("{=realms_page_realm}Realm"), kingdom.Name.ToString(), Go(kingdom.EncyclopediaLink)));
        else if (title.Rank == TitleRank.Kingdom && Realm(title.KingdomId) is { } own)
            InfoRows.Add(new TitleRowVM(Text("{=realms_page_kingdom}Kingdom"), own.Name.ToString(), Go(own.EncyclopediaLink)));

        // Who actually has the land, when it is not the holder by right.
        var actual = TitleText.ActualRuler(title);
        if (actual.Known && actual.HeroId != holderId)
            InfoRows.Add(new TitleRowVM(
                Text(title.SettlementId != null ? "{=realms_ui_owner}Fief owned by" : "{=realms_ui_ruler}Realm ruled by"),
                TitleText.HolderName(actual.HeroId) + " " + Text("{=realms_ui_without_title}(without the title)"),
                HeroPage(actual.HeroId)));

        InfoRows.Add(new TitleRowVM(Text("{=realms_ui_laws}Laws"), TitleText.Laws(b.Service.EffectiveLaws(title.Id)), null));

        var claims = ledger.ClaimsOn(title.Id).Count;
        InfoRows.Add(claims == 0
            ? new TitleRowVM(Text("{=realms_ui_claims}Claims"), Text("{=realms_ui_none_cap}None"), null)
            : new TitleRowVM(Text("{=realms_ui_claims}Claims"), Count(claims == 1
                ? "{=realms_enc_claimant}1 claimant"
                : "{=realms_enc_claimants}{COUNT} claimants", claims), () => RealmsPopups.OpenClaimants(title.Id)));

        if (title.Rank == TitleRank.Kingdom && title.KingdomId != null)
            foreach (var office in b.Service.OfficesOfRealm(title.KingdomId))
            {
                var officer = ledger.OfficeHolderOf(office.Id);
                InfoRows.Add(new TitleRowVM(new TextObject(office.Name).ToString(), TitleText.HolderName(officer), HeroPage(officer)));
            }

        foreach (var vassal in TitleText.Sorted(b, catalog.ChildrenOf(title.Id)))
            Vassals.Add(new TitleLinkVM(TitleNames.Of(vassal, b), TitleText.HolderName(b, vassal), vassal.Id));
        HasVassals = Vassals.Count > 0;

        foreach (var option in ClaimActions.For(b, title, Refresh, null))
            Actions.Add(new TitleActionVM(option));
        HasActions = Actions.Count > 0;

        foreach (var entry in TitleText.Recent(b, title.Id, TitleText.ShownChronicle))
            Chronicle.Add(new TitleRowVM(string.Empty, TitleText.ChronicleLine(entry), HeroPage(entry.HeroId)));
        HasChronicle = Chronicle.Count > 0;
    }

    private void Clear()
    {
        InfoRows.Clear();
        Vassals.Clear();
        Actions.Clear();
        Chronicle.Clear();
        Holder?.OnFinalize();
        Holder = null;
        Banner?.OnFinalize();
        Banner = null;
        Seat?.OnFinalize();
        Seat = null;
        HasHolder = HasSeat = HasVassals = HasActions = HasChronicle = false;
    }

    /// <summary>"Duchy" or "County (town)", with the realm's name when the title has one: "Duchy, Gondor".</summary>
    private static string RankLine(TitleDefinition title)
    {
        var rank = TitleText.Rank(title.Rank);
        if (title.Rank == TitleRank.Kingdom || Realm(title.KingdomId) is not { } kingdom)
            return rank;
        var text = new TextObject("{=realms_page_rank_realm}{RANK}, {REALM}");
        text.SetTextVariable("RANK", rank);
        text.SetTextVariable("REALM", kingdom.Name);
        return text.ToString();
    }

    /// <summary>
    /// The fief of a fief title. Above that: the realm's capital for a kingdom title, else the highest fief beneath the
    /// title that its holder holds by right (a town before a castle before a village). Null when there is none.
    /// </summary>
    internal static Settlement? SeatOf(RealmsCampaign b, TitleDefinition title)
    {
        if (title.SettlementId != null)
            return Settlement.Find(title.SettlementId);
        if (title.Rank == TitleRank.Kingdom && title.KingdomId != null
            && b.Config.KingdomCapitals.TryGetValue(title.KingdomId, out var capital) && Settlement.Find(capital) is { } seat)
            return seat;
        var holder = b.Service.Ledger.HolderOf(title.Id);
        if (holder == null)
            return null;
        var best = b.Service.DescendantsOf(title.Id)
            .Where(t => t.SettlementId != null && b.Service.Ledger.HolderOf(t.Id) == holder)
            .OrderByDescending(t => t.Rank)
            .FirstOrDefault();
        return best != null ? Settlement.Find(best.SettlementId) : null;
    }

    internal static Action TitlePage(string titleId) => () => TitleVisuals.GoTo(RealmsTitle.LinkOf(titleId));

    private static Action? Go(string? link) => string.IsNullOrEmpty(link) ? null : () => TitleVisuals.GoTo(link);

    private static Action? HeroPage(string? heroId) => Go(heroId != null ? Hero.Find(heroId)?.EncyclopediaLink : null);

    private static Kingdom? Realm(string? kingdomId) =>
        kingdomId == null ? null : TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Kingdom>(kingdomId);

    private static string Text(string id) => new TextObject(id).ToString();

    private static string Count(string template, int count)
    {
        var text = new TextObject(template);
        text.SetTextVariable("COUNT", count);
        return text.ToString();
    }
}

/// <summary>The seat card: the settlement's encyclopedia picture and name (opening its page), and who owns it.</summary>
internal sealed class TitleSeatVM : ViewModel
{
    private readonly Settlement _settlement;

    public TitleSeatVM(Settlement settlement)
    {
        _settlement = settlement;
        NameText = settlement.Name?.ToString() ?? settlement.StringId;
        var kind = settlement.IsVillage ? "village" : settlement.IsCastle ? "castle" : "town";
        try
        {
            PictureFile = EncyclopediaPictureCache.ResolveSettlement(settlement.StringId, settlement.Culture?.StringId ?? "", kind);
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] the picture of {settlement.StringId} threw: {ex}");
            PictureFile = string.Empty;
        }
        var owner = settlement.Owner;
        var text = new TextObject("{=realms_page_owned_by}Owned by {OWNER}");
        text.SetTextVariable("OWNER", owner?.Name ?? new TextObject("{=realms_ui_none}none"));
        OwnerText = text.ToString();
    }

    [DataSourceProperty] public string NameText { get; }
    [DataSourceProperty] public string PictureFile { get; }
    [DataSourceProperty] public string OwnerText { get; }

    public void ExecuteLink() => UiQueue.Post(() => TitleVisuals.GoTo(_settlement.EncyclopediaLink));

    public void ExecuteOwnerLink()
    {
        var link = _settlement.Owner?.EncyclopediaLink;
        UiQueue.Post(() => TitleVisuals.GoTo(link));
    }

    public void ExecuteBeginHint() => InformationManager.ShowTooltip(typeof(Settlement), _settlement);

    public void ExecuteEndHint() => MBInformationManager.HideInformations();
}

/// <summary>A vassal title: its name (opening its own page) and its holder by right.</summary>
internal sealed class TitleLinkVM : ViewModel
{
    private readonly string _titleId;

    public TitleLinkVM(string name, string holder, string titleId)
    {
        NameText = name;
        HolderText = holder;
        _titleId = titleId;
    }

    [DataSourceProperty] public string NameText { get; }
    [DataSourceProperty] public string HolderText { get; }

    public void ExecuteLink() => UiQueue.Post(TitleEncyclopediaPageVM.TitlePage(_titleId));
}

/// <summary>
/// One of the player's actions on the title as a button: "Buy a claim" over "Pay 200 Castar", or a disabled one over the
/// reason it is refused, which its hint repeats in full.
/// </summary>
internal sealed class TitleActionVM : ViewModel
{
    private readonly ClaimOption _option;

    public TitleActionVM(ClaimOption option)
    {
        _option = option;
        LabelText = option.Label;
        DetailText = TitleRows.ActionValue(option);
    }

    [DataSourceProperty] public string LabelText { get; }
    [DataSourceProperty] public string DetailText { get; }
    [DataSourceProperty] public bool IsAvailable => _option.Enabled;

    public void ExecuteAction()
    {
        if (_option.Enabled)
            UiQueue.Post(_option.Run);
    }

    public void ExecuteBeginHint()
    {
        if (DetailText.Length > 0)
            MBInformationManager.ShowHint(DetailText);
    }

    public void ExecuteEndHint() => MBInformationManager.HideInformations();
}
