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
/// count of folded village lordships. Bound both by the branch card and by the compact stack row in
/// RealmsTitleTreeNode.xml. Clicking closes the tree and opens the title's own encyclopedia page; hovering shows rank and holder.
/// </summary>
internal sealed class TitleCardVM : ViewModel
{
    private readonly string _link;
    private readonly string _hint;

    public TitleCardVM(RealmsCampaign b, TitleTreeNode node)
    {
        var title = node.Title;
        ImageIdentifier = new BannerImageIdentifierVM(TitleVisuals.Banner(b, title), nineGrid: true);
        TitleText = TitleNames.Of(title, b);
        HolderText = RealmsText.HolderName(b, title);
        IsSelected = node.IsFocus;
        IsLands = node.IsLands;
        HasFolded = node.FoldedLordships > 0;
        FoldedText = HasFolded ? FoldedLordships(node.FoldedLordships) : string.Empty;
        _link = TitleVisuals.Link(title);
        _hint = $"{RealmsText.Rank(title.Rank)}\n{new TextObject("{=realms_ui_holder}Held by right")}: {HolderText}";
        if (IsLands)
            _hint += "\n" + new TextObject("{=realms_tree_lands}Lies under the realm above once it is formed.");
    }

    [DataSourceProperty] public BannerImageIdentifierVM ImageIdentifier { get; }
    [DataSourceProperty] public string TitleText { get; }
    [DataSourceProperty] public string HolderText { get; }
    [DataSourceProperty] public bool HasFolded { get; }
    [DataSourceProperty] public string FoldedText { get; }
    [DataSourceProperty] public bool IsSelected { get; }

    /// <summary>Drawn under a vacant titular realm without being its vassal yet (<see cref="TitleTreeNode.IsLands"/>): faded.</summary>
    [DataSourceProperty] public bool IsLands { get; }

    /// <summary>How opaque a lands card's pennant and text are; the card itself and its hover stay as they are.</summary>
    public const float LandsAlpha = 0.45f;

    /// <summary>
    /// The pennant's and text's <c>Brush.GlobalAlphaFactor</c>: <see cref="LandsAlpha"/> for a lands card, else 1. Brush
    /// widgets fade through their brush; <c>AlphaFactor</c> reaches only a widget's own sprite.
    /// </summary>
    [DataSourceProperty] public float ContentAlpha => IsLands ? LandsAlpha : 1f;

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

    private static string FoldedLordships(int count)
    {
        var text = new TextObject(count == 1
            ? "{=realms_tree_lordship}+1 lordship"
            : "{=realms_tree_lordships}+{COUNT} lordships");
        text.SetTextVariable("COUNT", count);
        return text.ToString();
    }
}

/// <summary>
/// One item of the tree, bound by RealmsTitleTreeNode.xml: either a branch (one card in <see cref="Members"/>, its vassals
/// in <see cref="Branch"/>) or a stack (<see cref="IsStack"/>: up to <see cref="TitleTreeLayout.RowsPerStack"/> vassals
/// without vassals of their own, in <see cref="Rows"/>). <see cref="TitleTreeLayout.Split"/> chooses; stacks follow the
/// branches. The whole tree is built in the constructor, before the movie loads: the native tree widget draws its
/// connecting lines as list items are added and never redraws them.
/// </summary>
internal sealed class TitleTreeNodeVM : ViewModel
{
    public TitleTreeNodeVM(RealmsCampaign b, TitleTreeNode node)
    {
        Members = new MBBindingList<TitleCardVM> { new TitleCardVM(b, node) };
        Rows = new MBBindingList<TitleCardVM>();
        Branch = new MBBindingList<TitleTreeNodeVM>();
        var (branches, stacks) = TitleTreeLayout.Split(node.Children);
        foreach (var child in branches)
            Branch.Add(new TitleTreeNodeVM(b, child));
        foreach (var stack in stacks)
            Branch.Add(new TitleTreeNodeVM(b, stack));
    }

    private TitleTreeNodeVM(RealmsCampaign b, IReadOnlyList<TitleTreeNode> stack)
    {
        Members = new MBBindingList<TitleCardVM>();
        Rows = new MBBindingList<TitleCardVM>();
        Branch = new MBBindingList<TitleTreeNodeVM>();
        IsStack = true;
        foreach (var leaf in stack)
            Rows.Add(new TitleCardVM(b, leaf));
    }

    [DataSourceProperty] public MBBindingList<TitleCardVM> Members { get; }
    [DataSourceProperty] public MBBindingList<TitleCardVM> Rows { get; }
    [DataSourceProperty] public MBBindingList<TitleTreeNodeVM> Branch { get; }
    [DataSourceProperty] public bool IsStack { get; }

    public override void OnFinalize()
    {
        base.OnFinalize();
        foreach (var card in new List<TitleCardVM>(Members))
            card.OnFinalize();
        foreach (var row in new List<TitleCardVM>(Rows))
            row.OnFinalize();
        foreach (var child in new List<TitleTreeNodeVM>(Branch))
            child.OnFinalize();
    }
}

/// <summary>The data source of RealmsTitleTreePopup.xml: the highest title's name, the node tree and the close command.</summary>
internal sealed class TitleTreePopupVM : ViewModel
{
    private readonly Action _close;

    public TitleTreePopupVM(RealmsCampaign b, TitleTree tree, Action close)
    {
        _close = close;
        Title = TitleNames.Of(tree.Root.Title, b);
        Tree = new TitleTreeNodeVM(b, tree.Root);
    }

    [DataSourceProperty] public string Title { get; }
    [DataSourceProperty] public TitleTreeNodeVM Tree { get; }

    public void ExecuteClose() => _close();

    public override void OnFinalize()
    {
        base.OnFinalize();
        Tree.OnFinalize();
    }
}
