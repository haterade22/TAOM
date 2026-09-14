using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate;

namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// The widget-writing half of <see cref="TaomSettlementPlateWidget"/>, extracted so the widget
/// stays a thin engine entry point (ADR-002). Decisions come from
/// <see cref="NameplateRelationPalette"/> and <see cref="PlateAlphaPolicy"/>; this class only
/// finds the item widget and pushes values onto the plate's children. Every method is null-safe
/// per child so a prefab that lost one element still paints the rest.
/// </summary>
internal static class SettlementPlatePresenter
{
    /// <summary>Walks up from <paramref name="start"/>'s parent for the item widget vanilla writes
    /// alpha and colour factor to. The prefab nests the plate two levels under it.</summary>
    internal static SettlementNameplateItemWidget? FindItemAncestor(Widget start, int maxDepth)
    {
        var parent = start.ParentWidget;
        for (var depth = 0; depth < maxDepth && parent != null; depth++)
        {
            if (parent is SettlementNameplateItemWidget item) return item;
            parent = parent.ParentWidget;
        }
        return null;
    }

    internal static void ApplyPalette(Widget? bar, Widget? frame, TextWidget? text, in NameplatePaletteEntry entry)
    {
        if (bar != null) bar.Color = entry.Bar;
        if (frame != null) frame.Color = entry.Frame;
        if (text != null) text.Brush.FontColor = entry.Text;
    }

    /// <summary>
    /// The plate's configured resting alpha, the anchor for the text curve (#596): the neutral
    /// opacity for a neutral plate, the coloured opacity for own, enemy and allied plates, and
    /// vanilla's 0.35 for a tracked plate (vanilla owns its 0.8), an unknown relation or no settings.
    /// </summary>
    internal static float RestingAlpha(INameplateRelationSettingsProvider? settings, int relationType, bool isTracked)
    {
        if (settings == null || isTracked) return PlateAlphaPolicy.VanillaMinimumPlateAlpha;
        switch (relationType)
        {
            case NameplateRelationPalette.Neutral:
                return settings.NeutralPlateAlpha;
            case NameplateRelationPalette.SameFaction:
            case NameplateRelationPalette.Enemy:
            case NameplateRelationPalette.Ally:
                return settings.RelationPlateAlpha;
            default:
                return PlateAlphaPolicy.VanillaMinimumPlateAlpha;
        }
    }

    /// <summary>
    /// Copies the item's alpha and colour factor onto the bar and frame when they changed, then
    /// drives everything else on the plate from the text alpha: the name text and banner brushes,
    /// the tracked ring, the party icons and the event icons. Those keep vanilla's full alpha at
    /// close range (text alpha is 1 whenever the plate sits at its resting value) and only follow
    /// the plate down under the distance fade, otherwise a far tracked settlement would leave an
    /// opaque ring floating over the map after its plate faded (Codex review, #591). Every write
    /// repeats whenever the destination disagrees, because vanilla rewrites the text, grid and
    /// event alphas each parallel update from its own lerp toward 1. A non-finite plate alpha
    /// leaves bar and frame at their last good value (the policy refuses it) and restores full
    /// text alpha.
    /// </summary>
    internal static void MirrorAlpha(
        SettlementNameplateItemWidget item,
        INameplateRelationSettingsProvider? settings, int relationType,
        Widget? bar, Widget? frame, TextWidget? text, MaskedTextureWidget? banner, Widget? ring,
        ref float lastAlpha, ref float lastColorFactor)
    {
        var alpha = item.AlphaFactor;
        var colorFactor = item.ColorFactor;
        if (PlateAlphaPolicy.TryTakeChange(alpha, colorFactor, ref lastAlpha, ref lastColorFactor))
        {
            if (bar != null) { bar.AlphaFactor = alpha; bar.ColorFactor = colorFactor; }
            if (frame != null) { frame.AlphaFactor = alpha; frame.ColorFactor = colorFactor; }
        }

        var root = item.ParentWidget as SettlementNameplateWidget;
        var textAlpha = PlateAlphaPolicy.TextAlphaFor(alpha, RestingAlpha(settings, relationType, root?.IsTracked ?? false));
        if (text != null && PlateAlphaPolicy.NeedsWrite(text.ReadOnlyBrush.GlobalAlphaFactor, textAlpha))
            text.Brush.GlobalAlphaFactor = textAlpha;
        if (banner != null && PlateAlphaPolicy.NeedsWrite(banner.ReadOnlyBrush.GlobalAlphaFactor, textAlpha))
            banner.Brush.GlobalAlphaFactor = textAlpha;
        if (ring != null && PlateAlphaPolicy.NeedsWrite(ring.AlphaFactor, textAlpha))
            ring.AlphaFactor = textAlpha;

        var parties = item.SettlementPartiesGridWidget;
        if (parties != null && PlateAlphaPolicy.NeedsWrite(parties.AlphaFactor, textAlpha))
            parties.SetGlobalAlphaRecursively(textAlpha);
        var events = root?.EventsListPanel;
        if (events != null && PlateAlphaPolicy.NeedsWrite(events.AlphaFactor, textAlpha))
            events.SetGlobalAlphaRecursively(textAlpha);
    }
}
