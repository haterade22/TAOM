using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>A themed-screen button that scrolls <see cref="ScrollPanelWidget"/> until
/// <see cref="TargetWidget"/> sits at its top (#704, Kysaro's).</summary>
public class ScrollShortcutButton : ButtonWidget
{
    private const float TopOffset = 20f;
    private const float Duration = 0.3f;

    public ScrollShortcutButton(UIContext context)
        : base(context)
    {
    }

    [Editor(false)]
    public ScrollablePanel? ScrollPanelWidget { get; set; }

    [Editor(false)]
    public Widget? TargetWidget { get; set; }

    protected override void HandleClick()
    {
        base.HandleClick();
        var panel = ScrollPanelWidget;
        var target = TargetWidget;
        var inner = panel?.InnerPanel;
        var clip = panel?.ClipRect;
        var scrollbar = panel?.VerticalScrollbar;
        if (panel == null || target == null || inner == null || clip == null || scrollbar == null)
            return;

        var targetTop = target.GlobalPosition.Y - TopOffset;
        var innerHeight = inner.Size.Y + inner.ScaledMarginTop + inner.ScaledMarginBottom;
        var innerTop = inner.GlobalPosition.Y;
        var innerScrollEnd = innerTop + innerHeight - clip.Size.Y;
        var fraction = innerScrollEnd == innerTop ? 0f : (targetTop - innerTop) / (innerScrollEnd - innerTop);
        fraction = Math.Max(0f, Math.Min(1f, fraction));
        panel.SetVerticalScrollTarget(scrollbar.MinValue + (scrollbar.MaxValue - scrollbar.MinValue) * fraction, Duration);
    }
}
