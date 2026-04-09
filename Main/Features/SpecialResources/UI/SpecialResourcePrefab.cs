using System;
using System.Collections.Generic;
using System.Reflection;
using TAOM.Core.UI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace TAOM.Features.SpecialResources.UI;

// Replaces the IconBrushWidget inside the BottomInfoBar (SecondaryInfoItems) item template
// with our SpecialResourceSpriteWidget, which dynamically loads resource sprites.
// Normal icons fall through to IconBrushWidget base behavior; only "special_resource"
// triggers the custom sprite lookup.
internal static class SpecialResourcePrefab
{
    private static readonly FieldInfo s_typeField =
        typeof(WidgetTemplate).GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo s_attributesField =
        typeof(WidgetTemplate).GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Register()
    {
        WidgetPrefabPatcher.Register("MapBar", PatchMapBar);
    }

    private static void PatchMapBar(WidgetPrefab prefab)
    {
        var bottomInfoBar = FindDescendantById(prefab.RootTemplate, "BottomInfoBar");
        if (bottomInfoBar == null)
        {
            TaleWorlds.Library.Debug.Print(
                "[TAOM] SpecialResourcePrefab: BottomInfoBar not found in MapBar prefab",
                0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            return;
        }

        // ItemTemplate → HintWidget (DefaultItemTemplate)
        var itemTemplateUsage = bottomInfoBar.GetExtensionData<ItemTemplateUsage>();
        if (itemTemplateUsage?.DefaultItemTemplate == null)
        {
            TaleWorlds.Library.Debug.Print(
                "[TAOM] SpecialResourcePrefab: BottomInfoBar ItemTemplate not found",
                0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            return;
        }

        // HintWidget → Children[0] = ListPanel → Children[0] = IconBrushWidget
        var hintWidget = itemTemplateUsage.DefaultItemTemplate;
        if (hintWidget.ChildCount < 1)
            return;

        var listPanel = hintWidget.GetChildAt(0);
        if (listPanel.ChildCount < 1)
            return;

        var iconWidget = listPanel.GetChildAt(0);
        if (iconWidget.Type != "IconBrushWidget")
        {
            TaleWorlds.Library.Debug.Print(
                $"[TAOM] SpecialResourcePrefab: Expected IconBrushWidget at BottomInfoBar item[0][0], got '{iconWidget.Type}'",
                0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            return;
        }

        // Replace the type — SpecialResourceSpriteWidget extends IconBrushWidget so all
        // existing attributes (IconBrush, IconID, UseStylesFromSourceIcon, size policies) remain valid.
        s_typeField.SetValue(iconWidget, "SpecialResourceSpriteWidget");

        // Update size to 33×33 to match the original UIExtenderEx patch intent.
        SetAttributeValue(iconWidget, "SuggestedWidth", "33");
        SetAttributeValue(iconWidget, "SuggestedHeight", "33");
    }

    private static void SetAttributeValue(WidgetTemplate template, string key, string value)
    {
        var attrs = (Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>>)
            s_attributesField.GetValue(template);

        foreach (var kvp in attrs)
        {
            if (kvp.Value.TryGetValue(key, out var attr))
            {
                attr.Value = value;
                return;
            }
        }
    }

    /// <summary>
    /// Depth-first search for a WidgetTemplate whose Id matches the given value.
    /// Searches the regular children tree only (not ItemTemplate extension data).
    /// </summary>
    private static WidgetTemplate FindDescendantById(WidgetTemplate root, string id)
    {
        for (var i = 0; i < root.ChildCount; i++)
        {
            var child = root.GetChildAt(i);
            if (child.Id == id)
                return child;

            var found = FindDescendantById(child, id);
            if (found != null)
                return found;
        }
        return null;
    }
}
