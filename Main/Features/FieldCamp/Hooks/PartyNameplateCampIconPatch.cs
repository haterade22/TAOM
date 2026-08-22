using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TAOM.Features.FieldCamp.Domain;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate;

namespace TAOM.Features.FieldCamp.Hooks;

/// <summary>
/// Postfix on <see cref="PartyPlayerNameplateWidget"/>.<c>UpdateNameplatesVisibility(float)</c>
/// (protected override, called every frame for the player nameplate): injects a 54x54 icon child
/// widget above the nameplate showing the current camp type, and hides it when no camp stands.
///
/// This namespace is PatchShield-excluded (see Patch38 next to it in SubModule.cs), so the body
/// is written to be structurally unable to throw: one try/catch around everything, a null guard
/// on every step, and swallowed failures, because an escaped exception here would ride a
/// per-frame UI callback with no managed backstop. The no-camp path is also allocation-free
/// (indexed child scan, no LINQ, no boxing) since it runs every frame for the whole campaign.
///
/// Service handle arrives once via <see cref="Initialize"/> at module load (the Patch38 pattern);
/// resolving from IoC per call would put a container lookup on a per-frame path.
///
/// The source kept a <c>Dictionary&lt;Widget, CampType&gt;</c> memo that held a strong reference
/// to every nameplate widget it ever saw, leaking them for the process lifetime. Here the memo is
/// a <see cref="ConditionalWeakTable{TKey, TValue}"/> (dead widgets fall out with the GC) and
/// <see cref="Reset"/> drops the whole table at session end (net472's table has no public Clear).
/// </summary>
[HarmonyPatch(typeof(PartyPlayerNameplateWidget), "UpdateNameplatesVisibility")]
[HarmonyPatchCategory("Patch74_FieldCampNameplateIcon")]
public static class PartyNameplateCampIconPatch
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

    private static ICampService? _campService;

    // Replaced wholesale by Reset (net472's table has no public Clear); the field always holds a
    // live instance, so a Reset racing the postfix can at worst lose one frame's memo.
    private static ConditionalWeakTable<Widget, IconMemo> _memo =
        new ConditionalWeakTable<Widget, IconMemo>();

    private static readonly ConditionalWeakTable<Widget, IconMemo>.CreateValueCallback CreateMemo =
        _ => new IconMemo();

    /// <summary>Called once from FieldCampIoC at container build time.</summary>
    public static void Initialize(ICampService campService)
    {
        _campService = campService;
    }

    /// <summary>Session-end hook (wired from the campaign behavior): drops the per-widget memo so
    /// nothing from a dead UI context survives into the next campaign.</summary>
    public static void Reset()
    {
        _memo = new ConditionalWeakTable<Widget, IconMemo>();
    }

    [HarmonyPostfix]
    public static void Postfix(PartyPlayerNameplateWidget __instance)
    {
        try
        {
            var service = _campService;
            if (service == null || __instance == null)
                return;

            // The head group is the widget cluster the nameplate hangs from; the speed text is
            // the fallback anchor when a template variant ships without a head group.
            Widget? anchor = __instance.HeadGroupWidget ?? (Widget?)__instance.SpeedTextWidget;
            if (anchor == null)
                return;

            var camp = service.PlayerCamp;
            if (camp == null)
            {
                // Fast path, every frame with no camp: just hide an icon if one was ever created.
                var existing = FindIcon(anchor);
                if (existing != null)
                    existing.IsVisible = false;
                return;
            }

            var icon = FindIcon(anchor) ?? CreateIcon(anchor);
            if (icon == null)
                return;

            ApplySprite(icon, camp.TypeEnum);
            icon.IsVisible = true;
        }
        catch
        {
            // PatchShield-excluded namespace: swallowing IS the crash guard. A broken icon is
            // cosmetic; an exception here is a CTD.
        }
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
