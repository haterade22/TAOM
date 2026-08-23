using System;
using System.Runtime.CompilerServices;
using TAOM.Features.FieldCamp.Domain;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FieldCamp.Hooks;

/// <summary>
/// The widget half of Patch74, extracted so <see cref="PartyNameplateCampIconPatch"/> stays a thin
/// guarded entry point (ADR-002): icon lookup/creation, sprite selection and the per-widget memo
/// all live here. Same never-throw posture as the patch (this namespace is PatchShield-excluded):
/// every method is null-guarded and the only throw surface, widget construction, is caught.
///
/// <para>The source kept a <c>Dictionary&lt;Widget, CampType&gt;</c> memo that held a strong
/// reference to every nameplate widget it ever saw, leaking them for the process lifetime. Here
/// the memo is a <see cref="ConditionalWeakTable{TKey, TValue}"/> (dead widgets fall out with the
/// GC) and <see cref="Reset"/> drops the whole table at session end (net472's table has no public
/// Clear).</para>
/// </summary>
internal static class CampNameplateIconPresenter
{
    internal const string IconWidgetId = "TaomFieldCampIcon";

    // Sprite ids from the vanilla atlases; a missing sprite degrades to an invisible icon, never
    // a throw. Rendering cannot be certified statically; the binding test pins the names only.
    internal const string LookoutSprite = "MapBar\\monocular_icon";
    internal const string AmbushSprite = "SPGeneral\\GameMenu\\ambush_icon";
    internal const string CampSprite = "MapIncidents\\party_camplife";

    private sealed class IconMemo
    {
        public CampType? Applied;
    }

    // Replaced wholesale by Reset (net472's table has no public Clear); the field always holds a
    // live instance, so a Reset racing the patch can at worst lose one frame's memo.
    private static ConditionalWeakTable<Widget, IconMemo> _memo =
        new ConditionalWeakTable<Widget, IconMemo>();

    private static readonly ConditionalWeakTable<Widget, IconMemo>.CreateValueCallback CreateMemo =
        _ => new IconMemo();

    /// <summary>Session-end hook: drops the per-widget memo so nothing from a dead UI context
    /// survives into the next campaign.</summary>
    internal static void Reset()
    {
        _memo = new ConditionalWeakTable<Widget, IconMemo>();
    }

    /// <summary>Hides the icon if one was ever created. The every-frame no-camp fast path:
    /// allocation-free (indexed child scan, no LINQ, no boxing).</summary>
    internal static void HideIcon(Widget anchor)
    {
        var existing = FindIcon(anchor);
        if (existing != null)
            existing.IsVisible = false;
    }

    /// <summary>Ensures the icon child exists under the anchor, applies the camp-type sprite
    /// (memoized) and shows it.</summary>
    internal static void ShowIcon(Widget anchor, CampType type)
    {
        var icon = FindIcon(anchor) ?? CreateIcon(anchor);
        if (icon == null)
            return;

        ApplySprite(icon, type);
        icon.IsVisible = true;
    }

    private static Widget? FindIcon(Widget parent)
    {
        int count = parent.ChildCount;
        for (int i = 0; i < count; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && string.Equals(child.Id, IconWidgetId, StringComparison.Ordinal))
                return child;
        }
        return null;
    }

    private static Widget? CreateIcon(Widget parent)
    {
        try
        {
            var icon = new Widget(parent.Context)
            {
                Id = IconWidgetId,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = 54f,
                SuggestedHeight = 54f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                PositionYOffset = -56f,
                IsVisible = false,
            };
            parent.AddChild(icon);
            return icon;
        }
        catch
        {
            return null;
        }
    }

    private static void ApplySprite(Widget icon, CampType type)
    {
        var memo = _memo.GetValue(icon, CreateMemo);
        if (memo.Applied == type)
            return;

        string? spriteName = SpriteFor(type);
        if (spriteName != null)
        {
            var sprite = icon.Context?.SpriteData?.GetSprite(spriteName);
            if (sprite != null)
                icon.Sprite = sprite;
        }

        // Memoized even when the sprite lookup failed: retrying an absent atlas entry every
        // frame would buy nothing and cost a string lookup per frame.
        memo.Applied = type;
    }

    private static string? SpriteFor(CampType type)
    {
        switch (type)
        {
            case CampType.Lookout:
                return LookoutSprite;
            case CampType.Ambush:
                return AmbushSprite;
            case CampType.Field:
            case CampType.Fortified:
                return CampSprite;
            default:
                return null;
        }
    }
}
