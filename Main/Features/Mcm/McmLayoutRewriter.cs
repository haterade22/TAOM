using System;
using System.Collections.Generic;
using System.Xml;

namespace TAOM.Features.Mcm;

/// <summary>
/// Pure XML transform that repairs MCM's mod-options screen, which renders bottom-to-top.
///
/// Background: Bannerlord v1.4.0 fixed a long-standing engine bug where
/// <c>LayoutMethod="VerticalBottomToTop"</c> actually rendered top-to-bottom. MCM v5.11.4's
/// options-screen prefabs (<c>SettingsView</c>, <c>SettingsPropertyGroupView</c>,
/// <c>ModOptionsView</c>, <c>ModOptionsPageView</c>) were authored against the old engine and
/// still carry <c>LayoutImp.LayoutMethod="VerticalBottomToTop"</c>, so on v1.4.5 the entire
/// settings list renders inverted (group headers below their members, members in reverse Order,
/// groups in reverse GroupOrder).
///
/// MCM ships these prefabs as EMBEDDED resources (inside <c>Bannerlord.MBOptionScreen.v1.4.x.dll</c>)
/// and registers them through <c>WidgetFactoryManager.CreateAndRegister(name, XmlDocument)</c>, which
/// builds them via UIExtenderEx's <c>LoadFromDocument</c> reverse-patch — a path that bypasses the
/// <c>ProcessMovie</c> step where <c>[PrefabExtension]</c> patches are applied. That is why a
/// UIExtenderEx <c>PrefabExtensionSetAttributePatch</c> on these prefabs is a silent no-op; the fix
/// must mutate the <see cref="XmlDocument"/> on MCM's actual load path (see Patch41_McmLayoutFix).
///
/// This class is the testable core: given the prefab name and its document, it flips every
/// reversed layout attribute in place. It is null-safe and idempotent (re-running flips nothing).
/// </summary>
public static class McmLayoutRewriter
{
    private const string ReversedValue = "VerticalBottomToTop";
    private const string CorrectValue = "VerticalTopToBottom";

    // Both spellings the engine recognises for a ListPanel/StackLayout direction. MCM's prefabs use
    // LayoutImp.LayoutMethod; StackLayout.LayoutMethod is handled defensively (no-op if absent).
    private static readonly string[] LayoutAttributeNames =
    {
        "LayoutImp.LayoutMethod",
        "StackLayout.LayoutMethod",
    };

    // The (bare) names MCM passes to WidgetFactoryManager.CreateAndRegister. Only these four carry
    // VerticalBottomToTop ListPanels (verified against the embedded MCM.UI.GUI.Prefabs.*.xml
    // resources). Scoping by name keeps the rewrite off any other mod's embedded prefabs.
    private static readonly HashSet<string> McmLayoutPrefabNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SettingsView",
            "SettingsPropertyGroupView",
            "ModOptionsView_MCM",
            "ModOptionsView",
            "ModOptionsPageView",
        };

    /// <summary>True if <paramref name="name"/> is one of MCM's reversed options prefabs (case-insensitive, ".xml" optional).</summary>
    public static bool IsMcmLayoutPrefab(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var bare = name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            ? name.Substring(0, name.Length - 4)
            : name;
        return McmLayoutPrefabNames.Contains(bare);
    }

    /// <summary>
    /// Flips every <c>VerticalBottomToTop</c> layout attribute to <c>VerticalTopToBottom</c> in an MCM
    /// options prefab. Returns the number of attributes flipped (0 = not an MCM prefab, null doc, or
    /// nothing to flip). Does not throw on null input.
    /// </summary>
    public static int FlipMcmLayout(string name, XmlDocument doc)
    {
        if (doc == null || !IsMcmLayoutPrefab(name)) return 0;

        var elements = doc.SelectNodes("//*");
        if (elements == null) return 0;

        int flipped = 0;
        foreach (XmlNode node in elements)
        {
            if (!(node is XmlElement el)) continue;
            foreach (var attr in LayoutAttributeNames)
            {
                if (el.GetAttribute(attr) == ReversedValue)
                {
                    el.SetAttribute(attr, CorrectValue);
                    flipped++;
                }
            }
        }

        return flipped;
    }
}
