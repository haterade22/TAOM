using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Realms.Domain.Titles;

/// <summary>One title in a <see cref="TitleTree"/> and the vassal titles drawn under it.</summary>
public sealed class TitleTreeNode
{
    public TitleTreeNode(TitleDefinition title, bool isFocus, int foldedLordships, IReadOnlyList<TitleTreeNode> children)
    {
        Title = title;
        IsFocus = isFocus;
        FoldedLordships = foldedLordships;
        Children = children;
    }

    public TitleDefinition Title { get; }

    /// <summary>True for the title the tree was opened at; the popup highlights it.</summary>
    public bool IsFocus { get; }

    /// <summary>Village lordships under this title that are counted instead of drawn.</summary>
    public int FoldedLordships { get; }

    public IReadOnlyList<TitleTreeNode> Children { get; }
}

/// <summary>
/// How the popup lays out one title's vassals to keep the tree narrow: vassals with vassals of their own are drawn as
/// branches side by side, and the rest (leaves) are packed into columns of compact rows placed after the branches.
/// </summary>
public static class TitleTreeLayout
{
    public const int RowsPerStack = 5;

    /// <summary>
    /// Splits <paramref name="children"/> into branches and stacks, each in the given order. Leaves fill as few stacks as
    /// <paramref name="rowsPerStack"/> allows, evened out so 6 leaves make two stacks of 3 rather than 5 and 1.
    /// </summary>
    public static (IReadOnlyList<TitleTreeNode> Branches, IReadOnlyList<IReadOnlyList<TitleTreeNode>> Stacks) Split(
        IReadOnlyList<TitleTreeNode> children, int rowsPerStack = RowsPerStack)
    {
        if (rowsPerStack < 1)
            throw new ArgumentOutOfRangeException(nameof(rowsPerStack));
        var branches = children.Where(c => c.Children.Count > 0).ToList();
        var leaves = children.Where(c => c.Children.Count == 0).ToList();
        var stacks = new List<IReadOnlyList<TitleTreeNode>>();
        if (leaves.Count == 0)
            return (branches, stacks);

        var count = (leaves.Count + rowsPerStack - 1) / rowsPerStack;
        var size = leaves.Count / count;
        var larger = leaves.Count % count;
        var at = 0;
        for (var i = 0; i < count; i++)
        {
            var take = size + (i < larger ? 1 : 0);
            stacks.Add(leaves.GetRange(at, take));
            at += take;
        }
        return (branches, stacks);
    }
}

/// <summary>
/// The whole de jure tree around one title: from its highest liege down to every castle and town. Village lordships are
/// folded into a count on their castle or town, except under the one holding the focus (the focus itself or one of its
/// lordships), so the focus is always drawn. Vassals are ordered highest rank first, then by base name.
/// </summary>
public sealed class TitleTree
{
    private TitleTree(TitleTreeNode root, string focusId)
    {
        Root = root;
        FocusId = focusId;
    }

    public TitleTreeNode Root { get; }
    public string FocusId { get; }

    /// <summary>Null when the catalog has no title with that id.</summary>
    public static TitleTree? Build(TitleCatalog catalog, string? focusId)
    {
        var focus = catalog.Get(focusId);
        if (focus == null)
            return null;
        var root = catalog.AncestorsOf(focus.Id).LastOrDefault() ?? focus;
        return new TitleTree(BuildNode(catalog, root, focus), focus.Id);
    }

    private static TitleTreeNode BuildNode(TitleCatalog catalog, TitleDefinition title, TitleDefinition focus)
    {
        var vassals = catalog.ChildrenOf(title.Id)
            .OrderByDescending(t => t.Rank)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        var lordships = vassals.Where(t => t.Rank == TitleRank.Lordship).ToList();
        var unfold = title.Id == focus.Id || lordships.Any(t => t.Id == focus.Id);
        var drawn = unfold ? vassals : vassals.Where(t => t.Rank != TitleRank.Lordship);
        return new TitleTreeNode(
            title,
            title.Id == focus.Id,
            unfold ? 0 : lordships.Count,
            drawn.Select(t => BuildNode(catalog, t, focus)).ToList());
    }
}
