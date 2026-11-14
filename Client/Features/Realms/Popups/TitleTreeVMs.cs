using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Titles;

using RealmsText = TAOM.Client.Features.Realms.TitleText;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>
/// One title of the tree popup: its banner as a TAOM encyclopedia pennant, the title name, the holder by right and the
/// count of folded village lordships, placed on the canvas at <see cref="X"/>, <see cref="Y"/>. RealmsTitleTreePopup.xml
/// draws it in the tree as the realm (<see cref="IsRoot"/>), a pennant node (<see cref="IsNode"/>) or a compact row
/// (<see cref="IsRow"/>), and again in the details panel as the selected title, its liege or one of its vassals.
/// Clicking it in the tree or as a liege or vassal selects it; clicking the selected title's name closes the tree and
/// opens the title's own encyclopedia page. Hovering in the tree shows rank and holder.
/// </summary>
internal sealed class TitleCardVM : ViewModel
{
    private readonly string _link;
    private readonly string _hint;
    private readonly Action<TitleCardVM> _select;
    private bool _isSelected;

    public TitleCardVM(RealmsCampaign b, TitleTreeCardPlacement<TitleTreeNode> place, Action<TitleCardVM> select)
    {
        _select = select;
        X = place.X;
        Y = place.Y;
        Width = place.Width;
        Height = place.Height;
        IsRoot = place.Kind == TitleTreeCardKind.Root;
        IsNode = place.Kind == TitleTreeCardKind.Node;
        IsRow = place.Kind == TitleTreeCardKind.Row;
        var pennant = TitleTreeCanvasLayout.PennantOf(place.Kind, place.Height);
        PennantX = pennant.X;
        PennantY = pennant.Y;
        PennantWidth = pennant.Width;
        PennantHeight = pennant.Height;
        var node = place.Item;
        var title = node.Title;
        ImageIdentifier = new BannerImageIdentifierVM(TitleVisuals.Banner(b, title), nineGrid: true);
        TitleText = TitleNames.Of(title, b);
        HolderText = RealmsText.HolderName(b, title);
        IsLands = node.IsLands;
        HasFolded = node.FoldedLordships > 0;
        FoldedText = HasFolded ? FoldedLordships(node.FoldedLordships) : string.Empty;
        _link = TitleVisuals.Link(title);
        _hint = $"{RealmsText.Rank(title.Rank)}\n{new TextObject("{=realms_ui_holder}Held by right")}: {HolderText}";
        if (IsLands)
            _hint += "\n" + new TextObject("{=realms_tree_lands}Lies under the realm above once it is formed.");
    }

    [DataSourceProperty] public float X { get; }
    [DataSourceProperty] public float Y { get; }
    [DataSourceProperty] public float Width { get; }
    [DataSourceProperty] public float Height { get; }
    [DataSourceProperty] public bool IsRoot { get; }
    [DataSourceProperty] public bool IsNode { get; }
    [DataSourceProperty] public bool IsRow { get; }
    [DataSourceProperty] public float PennantX { get; }
    [DataSourceProperty] public float PennantY { get; }
    [DataSourceProperty] public float PennantWidth { get; }
    [DataSourceProperty] public float PennantHeight { get; }

    [DataSourceProperty] public BannerImageIdentifierVM ImageIdentifier { get; }

    // The pennant binds these on the card itself, not through DataSource="{ImageIdentifier}": scoped to the identifier,
    // its Brush.GlobalAlphaFactor="@ContentAlpha" found no such property there and drew every banner empty.
    [DataSourceProperty] public string BannerId => ImageIdentifier.Id;
    [DataSourceProperty] public string BannerArgs => ImageIdentifier.AdditionalArgs;
    [DataSourceProperty] public string BannerProvider => ImageIdentifier.TextureProviderName;

    [DataSourceProperty] public string TitleText { get; }
    [DataSourceProperty] public string HolderText { get; }
    [DataSourceProperty] public bool HasFolded { get; }
    [DataSourceProperty] public string FoldedText { get; }

    /// <summary>The title shown in the details panel; at first the one the tree was opened at.</summary>
    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected)
                return;
            _isSelected = value;
            OnPropertyChangedWithValue(value, nameof(IsSelected));
        }
    }

    /// <summary>Drawn under a vacant titular realm without being its vassal yet (<see cref="TitleTreeNode.IsLands"/>): faded.</summary>
    [DataSourceProperty] public bool IsLands { get; }

    /// <summary>How opaque a lands card's pennant and text are.</summary>
    public const float LandsAlpha = 0.45f;

    /// <summary>
    /// The pennant's and text's <c>Brush.GlobalAlphaFactor</c>: <see cref="LandsAlpha"/> for a lands card, else 1. Brush
    /// widgets fade through their brush; <c>AlphaFactor</c> reaches only a widget's own sprite.
    /// </summary>
    [DataSourceProperty] public float ContentAlpha => IsLands ? LandsAlpha : 1f;

    public void ExecuteSelect() => _select(this);

    /// <summary>Closes the tree, then opens the title's page on the next tick, once the popup layer is gone.</summary>
    public void ExecuteLink()
    {
        var link = _link;
        UiQueue.Post(() =>
        {
            RealmsPopups.Close();
            UiQueue.Post(() => TitleVisuals.GoTo(link));
        });
    }

    public void ExecuteBeginHint() => MBInformationManager.ShowHint(_hint);

    public void ExecuteEndHint() => MBInformationManager.HideInformations();

    public override void OnFinalize()
    {
        base.OnFinalize();
        ImageIdentifier.OnFinalize();
    }

    internal static string FoldedLordships(int count)
    {
        var text = new TextObject(count == 1
            ? "{=realms_tree_lordship}+1 lordship"
            : "{=realms_tree_lordships}+{COUNT} lordships");
        text.SetTextVariable("COUNT", count);
        return text.ToString();
    }
}

/// <summary>One straight piece of connector line on the tree canvas, lit gold while it leads to the selected title.</summary>
internal sealed class TitleTreeLineVM : ViewModel
{
    private readonly TitleTreeSegment _segment;
    private bool _isLit;

    public TitleTreeLineVM(TitleTreeSegment segment) => _segment = segment;

    [DataSourceProperty] public float X => _segment.X;
    [DataSourceProperty] public float Y => _segment.Y;
    [DataSourceProperty] public float Width => _segment.Width;
    [DataSourceProperty] public float Height => _segment.Height;

    [DataSourceProperty]
    public bool IsLit
    {
        get => _isLit;
        private set
        {
            if (value == _isLit)
                return;
            _isLit = value;
            OnPropertyChangedWithValue(value, nameof(IsLit));
        }
    }

    internal void LightFor(int selected) => IsLit = _segment.Lights(selected);
}

/// <summary>
/// The data source of RealmsTitleTreePopup.xml (#1255): the highest title's name, the whole tree laid out top-down by
/// <see cref="TitleTreeCanvasLayout"/> as flat lists of cards and connector lines on one canvas of <see cref="CanvasWidth"/>
/// by <see cref="CanvasHeight"/>, which the popup scrolls, and the details of the <see cref="Selected"/> title: its liege
/// and its vassals. Every title of the tree is a card.
/// </summary>
internal sealed class TitleTreePopupVM : ViewModel
{
    private readonly Action _close;
    private readonly TitleTreeCanvas<TitleTreeNode> _canvas;
    private TitleCardVM _selected = null!;
    private bool _hasLiege;
    private bool _hasSubordinates;
    private int _selectionVersion;

    public TitleTreePopupVM(RealmsCampaign b, TitleTree tree, Action close)
    {
        _close = close;
        Title = TitleNames.Of(tree.Root.Title, b);
        DoneText = new TextObject("{=taom_career_done}Done").ToString();
        SelectedLabel = new TextObject("{=taom_realms_tree_selected}Selected title").ToString();
        HolderLabel = new TextObject("{=taom_realms_tree_holder}Holder").ToString();
        LiegeLabel = new TextObject("{=taom_realms_tree_liege}Liege").ToString();
        SubordinatesLabel = new TextObject("{=taom_realms_tree_subordinates}Subordinate titles").ToString();

        _canvas = TitleTreeCanvasLayout.Build(tree.Root, n => n.Children, n => n.IsFocus,
            (n, kind) => TitleTreeTextFit.CardHeight(kind, TitleNames.Of(n.Title, b), RealmsText.HolderName(b, n.Title),
                n.FoldedLordships > 0 ? TitleCardVM.FoldedLordships(n.FoldedLordships) : null));
        CanvasWidth = _canvas.Width;
        CanvasHeight = _canvas.Height;
        Cards = new MBBindingList<TitleCardVM>();
        foreach (var place in _canvas.Cards)
            Cards.Add(new TitleCardVM(b, place, Select));
        Lines = new MBBindingList<TitleTreeLineVM>();
        foreach (var segment in _canvas.Segments)
            Lines.Add(new TitleTreeLineVM(segment));
        Liege = new MBBindingList<TitleCardVM>();
        Subordinates = new MBBindingList<TitleCardVM>();
        Select(Cards[Math.Max(0, _canvas.FocusIndex)]);
    }

    [DataSourceProperty] public string Title { get; }
    [DataSourceProperty] public string DoneText { get; }
    [DataSourceProperty] public string SelectedLabel { get; }
    [DataSourceProperty] public string HolderLabel { get; }
    [DataSourceProperty] public string LiegeLabel { get; }
    [DataSourceProperty] public string SubordinatesLabel { get; }
    [DataSourceProperty] public float CanvasWidth { get; }
    [DataSourceProperty] public float CanvasHeight { get; }
    [DataSourceProperty] public MBBindingList<TitleCardVM> Cards { get; }
    [DataSourceProperty] public MBBindingList<TitleTreeLineVM> Lines { get; }

    /// <summary>The selected title's liege: one card, or none for the realm and for lands.</summary>
    [DataSourceProperty] public MBBindingList<TitleCardVM> Liege { get; }

    /// <summary>The selected title's vassals, highest rank first; lands are not vassals.</summary>
    [DataSourceProperty] public MBBindingList<TitleCardVM> Subordinates { get; }

    [DataSourceProperty]
    public TitleCardVM Selected
    {
        get => _selected;
        private set
        {
            _selected = value;
            OnPropertyChangedWithValue(value, nameof(Selected));
        }
    }

    [DataSourceProperty]
    public bool HasLiege
    {
        get => _hasLiege;
        private set
        {
            _hasLiege = value;
            OnPropertyChangedWithValue(value, nameof(HasLiege));
        }
    }

    [DataSourceProperty]
    public bool HasSubordinates
    {
        get => _hasSubordinates;
        private set
        {
            _hasSubordinates = value;
            OnPropertyChangedWithValue(value, nameof(HasSubordinates));
        }
    }

    /// <summary>Counts selections; RealmsTitleTreeScrollablePanel brings the selected title into view when it changes.</summary>
    [DataSourceProperty]
    public int SelectionVersion
    {
        get => _selectionVersion;
        private set
        {
            _selectionVersion = value;
            OnPropertyChangedWithValue(value, nameof(SelectionVersion));
        }
    }

    public void ExecuteClose() => _close();

    private void Select(TitleCardVM card)
    {
        int index = Cards.IndexOf(card);
        if (index < 0)
            return;
        foreach (var other in Cards)
            other.IsSelected = other == card;
        foreach (var line in Lines)
            line.LightFor(index);

        Selected = card;
        Liege.Clear();
        int liege = _canvas.LiegeOf(index, n => n.IsLands);
        if (liege >= 0)
            Liege.Add(Cards[liege]);
        Subordinates.Clear();
        foreach (int vassal in _canvas.SubordinatesOf(index, n => n.Children, n => n.IsLands))
            Subordinates.Add(Cards[vassal]);
        HasLiege = Liege.Count > 0;
        HasSubordinates = Subordinates.Count > 0;
        SelectionVersion++;
    }

    public override void OnFinalize()
    {
        base.OnFinalize();
        foreach (var card in new List<TitleCardVM>(Cards))
            card.OnFinalize();
    }
}
