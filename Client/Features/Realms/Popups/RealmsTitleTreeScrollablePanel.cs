using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>
/// The title tree's scroll panel (#1255): the native troop-tree panel, with both scrollbars and mouse panning, that brings
/// the selected title's card into view after the canvas has its size, and again whenever <see cref="SelectionVersion"/>
/// changes (a liege or vassal picked in the details panel). The user scrolls freely in between.
/// </summary>
public class RealmsTitleTreeScrollablePanel : EncyclopediaTroopScrollablePanel
{
    private bool _scrolled;
    private int _selectionVersion;

    public RealmsTitleTreeScrollablePanel(UIContext context) : base(context) { }

    /// <summary>Bound to the popup's selection count: a new value scrolls to the newly selected card.</summary>
    public int SelectionVersion
    {
        get => _selectionVersion;
        set
        {
            if (value == _selectionVersion)
                return;
            _selectionVersion = value;
            _scrolled = false;
        }
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);
        if (_scrolled || InnerPanel == null || ClipRect == null || ClipRect.Size.X <= 0f || InnerPanel.Size.X <= 0f)
            return;
        var selected = FindSelected(InnerPanel);
        if (selected != null && selected.Size.X <= 0f)
            return;
        _scrolled = true;
        // 0.5 centres the card on each axis it is out of view on (ScrollablePanel.GetScrollYValueForWidget lerps from
        // top-aligned to bottom-aligned), so a deep title opens with its parents beside it rather than in a corner.
        if (selected != null)
            ScrollToChild(selected, new AutoScrollParameters(horizontalScrollTarget: 0.5f, verticalScrollTarget: 0.5f));
    }

    private static Widget? FindSelected(Widget parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is ButtonWidget { IsSelected: true } button)
                return button;
            var nested = FindSelected(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}
