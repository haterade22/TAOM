using System;
using System.Collections.Generic;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>How a title is drawn: the realm's large pennant, a pennant with its text below, or a compact row.</summary>
internal enum TitleTreeCardKind
{
    Root,
    Node,
    Row,
}

/// <summary>A rectangle in canvas or card pixels.</summary>
internal readonly struct TitleTreeRect
{
    public TitleTreeRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
}

/// <summary>One title on the canvas, in canvas pixels from the top left, and the index of the card drawn above it.</summary>
internal sealed class TitleTreeCardPlacement<T>
{
    public TitleTreeCardPlacement(T item, TitleTreeCardKind kind, int parent, float x, float y, float width, float height)
    {
        Item = item;
        Kind = kind;
        Parent = parent;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public T Item { get; }
    public TitleTreeCardKind Kind { get; }

    /// <summary>The index of the card this one hangs from, or -1 for the realm.</summary>
    public int Parent { get; }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
}

/// <summary>
/// One straight piece of connector line, drawn behind the cards. It lies on the path from the realm down to every card
/// whose index is in [<see cref="LitFrom"/>, <see cref="LitTo"/>), so it is lit while one of them is selected.
/// </summary>
internal readonly struct TitleTreeSegment
{
    public TitleTreeSegment(float x, float y, float width, float height, int litFrom, int litTo)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        LitFrom = litFrom;
        LitTo = litTo;
    }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
    public int LitFrom { get; }
    public int LitTo { get; }

    public bool Lights(int selected) => selected >= LitFrom && selected < LitTo;
}

/// <summary>
/// The laid-out tree: cards in pre-order (a parent, then its vassals' subtrees), so every subtree is one run of indices;
/// the connector segments; and the canvas extents.
/// </summary>
internal sealed class TitleTreeCanvas<T>
{
    public TitleTreeCanvas(IReadOnlyList<TitleTreeCardPlacement<T>> cards, IReadOnlyList<TitleTreeSegment> segments,
        float width, float height, int focusIndex)
    {
        Cards = cards;
        Segments = segments;
        Width = width;
        Height = height;
        FocusIndex = focusIndex;
    }

    public IReadOnlyList<TitleTreeCardPlacement<T>> Cards { get; }
    public IReadOnlyList<TitleTreeSegment> Segments { get; }
    public float Width { get; }
    public float Height { get; }

    /// <summary>The index in <see cref="Cards"/> of the title the tree was opened at, or -1.</summary>
    public int FocusIndex { get; }

    /// <summary>
    /// The card of the selected title's liege, or -1: none for the realm, and none for lands, which hang under a vacant
    /// realm without being its vassals.
    /// </summary>
    public int LiegeOf(int index, Func<T, bool> isLands)
    {
        var card = Cards[index];
        return isLands(card.Item) ? -1 : card.Parent;
    }

    /// <summary>The cards of the selected title's vassals, in the tree's order (highest rank first); lands are left out.</summary>
    public IReadOnlyList<int> SubordinatesOf(int index, Func<T, IReadOnlyList<T>> childrenOf, Func<T, bool> isLands)
    {
        var result = new List<int>();
        foreach (var child in childrenOf(Cards[index].Item))
        {
            if (isLands(child))
                continue;
            for (int i = index + 1; i < Cards.Count; i++)
            {
                if (Cards[i].Parent == index && EqualityComparer<T>.Default.Equals(Cards[i].Item, child))
                {
                    result.Add(i);
                    break;
                }
            }
        }
        return result;
    }
}

/// <summary>
/// Lays the title tree out top-down (#1255): the realm at the top centre, its great titles as pennants in a row below, and
/// further down each title's vassals. A vassal with vassals of its own, or an only vassal, is a pennant with its text
/// below, so a chain of titles runs straight down; the other vassals are compact rows stacked under their liege, at most
/// <see cref="RowsPerStack"/> to a stack and evened out (as <c>TitleTreeLayout.Split</c> does). Every title is kept,
/// however wide or deep the tree: a tree narrower than the viewport is centred in it, a larger one grows the canvas,
/// which the popup scrolls. Pure, so the preview and the tests run it without the game.
/// </summary>
internal static class TitleTreeCanvasLayout
{
    /// <summary>The popup's tree viewport; RealmsTitleTreePopup.xml sizes its scroll panel to match.</summary>
    public const float ViewportWidth = 1048f;
    public const float ViewportHeight = 790f;

    public const float Padding = 8f;
    public const float LineThickness = 2f;
    public const int RowsPerStack = 5;

    public const float RootWidth = 320f;
    public const float RootHeight = 168f;
    public const float NodeWidth = 210f;
    public const float NodeHeight = 172f;
    public const float RowWidth = 250f;
    public const float RowHeight = 72f;
    public const float RowGap = 8f;

    /// <summary>Between neighbouring subtrees.</summary>
    public const float SiblingGap = 28f;

    /// <summary>From a card's bottom to the top of the cards below it; the connector bar runs at its middle.</summary>
    public const float LevelGap = 28f;

    /// <param name="heightOf">
    /// How tall a title's card must be for its text (<see cref="TitleTreeTextFit.CardHeight"/>); a card is never shorter than
    /// its kind's height. A taller row pushes the rows below it down; a taller pennant node drops its vassals and its connector.
    /// </param>
    public static TitleTreeCanvas<T> Build<T>(T root, Func<T, IReadOnlyList<T>> childrenOf, Func<T, bool> isFocus,
        Func<T, TitleTreeCardKind, float>? heightOf = null)
    {
        var tree = Item<T>.Node(root, TitleTreeCardKind.Root, childrenOf);
        float width = Math.Max(ViewportWidth, tree.SubtreeWidth + 2f * Padding);
        var state = new PlaceState<T>(isFocus, heightOf);
        Place(tree, (width - tree.SubtreeWidth) / 2f, Padding, -1, state);
        float height = Math.Max(ViewportHeight, state.Bottom + Padding);
        return new TitleTreeCanvas<T>(state.Cards, state.Segments, width, height, state.FocusIndex);
    }

    /// <summary>
    /// Where a card of this height holds its pennant, at the encyclopedia pennant's 408:664 aspect: at the top of a pennant
    /// node, centred down a row (whose text is centred too).
    /// </summary>
    public static TitleTreeRect PennantOf(TitleTreeCardKind kind, float height) => kind switch
    {
        TitleTreeCardKind.Root => new TitleTreeRect((RootWidth - 60f) / 2f, 0f, 60f, 98f),
        TitleTreeCardKind.Node => new TitleTreeRect((NodeWidth - 54f) / 2f, 0f, 54f, 88f),
        _ => new TitleTreeRect(0f, (height - 65f) / 2f, 40f, 65f),
    };

    private static float WidthOf(TitleTreeCardKind kind) =>
        kind == TitleTreeCardKind.Root ? RootWidth : kind == TitleTreeCardKind.Node ? NodeWidth : RowWidth;

    /// <summary>A card's height when its text needs no more.</summary>
    public static float DefaultHeightOf(TitleTreeCardKind kind) =>
        kind == TitleTreeCardKind.Root ? RootHeight : kind == TitleTreeCardKind.Node ? NodeHeight : RowHeight;

    /// <summary>Places a pennant node or a stack with its left edge at <paramref name="left"/>; returns its card range.</summary>
    private static (float Centre, float Top, int From, int To) Place<T>(Item<T> item, float left, float top, int parent,
        PlaceState<T> state)
    {
        int from = state.Cards.Count;
        if (item.Rows != null)
        {
            float y = top;
            foreach (var row in item.Rows)
            {
                float rowHeight = state.HeightOf(row, TitleTreeCardKind.Row);
                state.Add(new TitleTreeCardPlacement<T>(row, TitleTreeCardKind.Row, parent, left, y, RowWidth, rowHeight));
                y += rowHeight + RowGap;
            }
            return (left + RowWidth / 2f, top, from, state.Cards.Count);
        }

        float ownWidth = WidthOf(item.Kind);
        float ownHeight = state.HeightOf(item.Title, item.Kind);
        float centre = left + item.SubtreeWidth / 2f;
        state.Add(new TitleTreeCardPlacement<T>(item.Title, item.Kind, parent, centre - ownWidth / 2f, top, ownWidth, ownHeight));
        if (item.Items.Count == 0)
            return (centre, top, from, from + 1);

        float childTop = top + ownHeight + LevelGap;
        float childLeft = centre - item.ChildrenWidth / 2f;
        var placed = new List<(float Centre, float Top, int From, int To)>(item.Items.Count);
        foreach (var child in item.Items)
        {
            placed.Add(Place(child, childLeft, childTop, from, state));
            childLeft += child.SubtreeWidth + SiblingGap;
        }
        Connect(centre, top + ownHeight, from, state.Cards.Count, placed, state.Segments);
        return (centre, top, from, state.Cards.Count);
    }

    /// <summary>
    /// A stem down from the parent to the bar, a piece of bar from the stem to each vassal's centre and a drop into the
    /// vassal. The stem lights for any selected vassal below; each bar piece and drop only for its own vassal's subtree.
    /// </summary>
    private static void Connect(float parentCentre, float parentBottom, int parent, int end,
        List<(float Centre, float Top, int From, int To)> children, List<TitleTreeSegment> segments)
    {
        const float half = LineThickness / 2f;
        float bar = parentBottom + LevelGap / 2f;
        segments.Add(new TitleTreeSegment(parentCentre - half, parentBottom, LineThickness, bar - parentBottom + half, parent + 1, end));
        foreach (var child in children)
        {
            float distance = Math.Abs(child.Centre - parentCentre);
            if (distance >= 0.5f)
                segments.Add(new TitleTreeSegment(Math.Min(child.Centre, parentCentre) - half, bar - half,
                    distance + LineThickness, LineThickness, child.From, child.To));
            segments.Add(new TitleTreeSegment(child.Centre - half, bar - half, LineThickness, child.Top - bar + half,
                child.From, child.To));
        }
    }

    private sealed class PlaceState<T>
    {
        private readonly Func<T, bool> _isFocus;
        private readonly Func<T, TitleTreeCardKind, float>? _heightOf;

        public PlaceState(Func<T, bool> isFocus, Func<T, TitleTreeCardKind, float>? heightOf)
        {
            _isFocus = isFocus;
            _heightOf = heightOf;
        }

        public float HeightOf(T title, TitleTreeCardKind kind) =>
            Math.Max(DefaultHeightOf(kind), _heightOf?.Invoke(title, kind) ?? 0f);

        public List<TitleTreeCardPlacement<T>> Cards { get; } = new();
        public List<TitleTreeSegment> Segments { get; } = new();
        public int FocusIndex { get; private set; } = -1;
        public float Bottom { get; private set; }

        public void Add(TitleTreeCardPlacement<T> card)
        {
            if (FocusIndex < 0 && _isFocus(card.Item))
                FocusIndex = Cards.Count;
            Cards.Add(card);
            Bottom = Math.Max(Bottom, card.Y + card.Height);
        }
    }

    /// <summary>
    /// A pennant node with its vassals' items, or a stack of compact rows (<see cref="Rows"/> set), measured once:
    /// <see cref="SubtreeWidth"/> is the wider of its own card and its vassals side by side.
    /// </summary>
    private sealed class Item<T>
    {
        private Item(T title, TitleTreeCardKind kind, List<Item<T>> items, List<T>? rows, float subtreeWidth, float childrenWidth)
        {
            Title = title;
            Kind = kind;
            Items = items;
            Rows = rows;
            SubtreeWidth = subtreeWidth;
            ChildrenWidth = childrenWidth;
        }

        public T Title { get; }
        public TitleTreeCardKind Kind { get; }
        public List<Item<T>> Items { get; }
        public List<T>? Rows { get; }
        public float SubtreeWidth { get; }
        public float ChildrenWidth { get; }

        public static Item<T> Node(T title, TitleTreeCardKind kind, Func<T, IReadOnlyList<T>> childrenOf)
        {
            var children = childrenOf(title);
            var items = new List<Item<T>>();
            // The realm's great titles and an only vassal are pennants; otherwise vassals of vassals are, and leaves stack.
            bool allNodes = kind == TitleTreeCardKind.Root || children.Count == 1;
            var leaves = new List<T>();
            foreach (var child in children)
            {
                if (allNodes || childrenOf(child).Count > 0)
                    items.Add(Node(child, TitleTreeCardKind.Node, childrenOf));
                else
                    leaves.Add(child);
            }
            if (leaves.Count > 0)
            {
                int stacks = (leaves.Count + RowsPerStack - 1) / RowsPerStack;
                int size = leaves.Count / stacks;
                int larger = leaves.Count % stacks;
                int at = 0;
                for (int i = 0; i < stacks; i++)
                {
                    int take = size + (i < larger ? 1 : 0);
                    items.Add(new Item<T>(default!, TitleTreeCardKind.Row, new List<Item<T>>(), leaves.GetRange(at, take), RowWidth, 0f));
                    at += take;
                }
            }

            float childrenWidth = 0f;
            for (int i = 0; i < items.Count; i++)
                childrenWidth += items[i].SubtreeWidth + (i > 0 ? SiblingGap : 0f);
            return new Item<T>(title, kind, items, null, Math.Max(WidthOf(kind), childrenWidth), childrenWidth);
        }
    }
}
