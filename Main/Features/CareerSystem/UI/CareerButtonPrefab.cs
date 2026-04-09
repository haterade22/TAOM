using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.GauntletUI.PrefabSystem;
using TAOM.Core.UI;

namespace TAOM.Features.CareerSystem.UI;

/// <summary>
/// Registers the career button prefab patch against CharacterDeveloper.xml
/// using WidgetPrefabPatcher instead of UIExtenderEx.
/// Equivalent to the old PrefabExtensionInsertPatch that appended a ButtonWidget
/// into the TopPanelParent widget.
/// </summary>
internal static class CareerButtonPrefab
{
    // Reflected WidgetTemplate internals — accessed once, shared across patches.
    private static readonly FieldInfo s_attributesField =
        typeof(WidgetTemplate).GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo s_childrenField =
        typeof(WidgetTemplate).GetField("_children", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Register()
    {
        WidgetPrefabPatcher.Register("CharacterDeveloper", ApplyPatch);
    }

    private static void ApplyPatch(WidgetPrefab prefab)
    {
        var root = prefab.RootTemplate;
        if (root == null)
            return;

        // Find the TopPanelParent widget and the attributes context we can clone from.
        WidgetTemplate topPanelParent = FindById(root, "TopPanelParent");
        if (topPanelParent == null)
        {
            TaleWorlds.Library.Debug.Print(
                "[TAOM] CareerButtonPrefab: Could not find TopPanelParent widget — button not injected.",
                0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            return;
        }

        // Clone the attribute-key structure from an existing template so our new
        // WidgetTemplate has the correct dictionary keys for SetAttributes() to work.
        var keyStructure = CloneAttributeKeyStructure(topPanelParent);

        // Build the ButtonWidget template.
        var button = BuildButtonTemplate(keyStructure);

        // Build the TextWidget child template.
        var text = BuildTextTemplate(keyStructure);

        // Wire text as a child of button (mirroring the <Children> block in XML),
        // BEFORE calling SetRootTemplate so the recursive walk covers text too.
        var buttonChildren = (List<WidgetTemplate>)s_childrenField.GetValue(button);
        buttonChildren.Add(text);

        // Propagate Prefab reference through the full subtree.
        button.SetRootTemplate(prefab);

        // Append button as a child of TopPanelParent (InsertType.Append equivalent).
        var parentChildren = (List<WidgetTemplate>)s_childrenField.GetValue(topPanelParent);
        parentChildren.Add(button);
    }

    /// <summary>
    /// Depth-first search for a WidgetTemplate with the given Id attribute.
    /// </summary>
    private static WidgetTemplate FindById(WidgetTemplate node, string id)
    {
        if (node.Id == id)
            return node;

        for (var i = 0; i < node.ChildCount; i++)
        {
            var found = FindById(node.GetChildAt(i), id);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Returns a copy of the attribute-key structure from <paramref name="source"/> —
    /// a Dictionary&lt;Type, Dictionary&lt;string, WidgetAttributeTemplate&gt;&gt; with the
    /// same key types but empty inner dictionaries. This gives new templates the same
    /// attribute slots the Gauntlet attribute-context registered.
    /// </summary>
    private static Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> CloneAttributeKeyStructure(
        WidgetTemplate source)
    {
        var orig = (Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>>)
            s_attributesField.GetValue(source);

        var clone = new Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>>(orig.Count);
        foreach (var kvp in orig)
            clone[kvp.Key] = new Dictionary<string, WidgetAttributeTemplate>();

        return clone;
    }

    /// <summary>
    /// Adds a <see cref="WidgetAttributeTemplate"/> to the correct slot in the attribute map,
    /// choosing the slot whose key-type instance accepts the given serialized attribute name.
    /// Falls back to the first slot if no key-type matches (should not happen for well-known attrs).
    /// </summary>
    private static void AddAttribute(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> attrs,
        string serializedKey,
        string serializedValue)
    {
        // Two-pass: first try specific (non-catch-all) key types, then fall back to catch-all.
        // This mirrors how WidgetAttributeContext.GetKeyType() works — specific types registered
        // before the catch-all WidgetAttributeKeyTypeAttribute.
        KeyValuePair<Type, Dictionary<string, WidgetAttributeTemplate>>? catchAllSlot = null;

        foreach (var kvp in attrs)
        {
            var keyTypeInstance = GetOrCreateKeyTypeInstance(kvp.Key);
            if (keyTypeInstance == null || !keyTypeInstance.CheckKeyType(serializedKey))
                continue;

            // WidgetAttributeKeyTypeAttribute is the catch-all — defer it.
            if (kvp.Key.Name == "WidgetAttributeKeyTypeAttribute")
            {
                catchAllSlot = kvp;
                continue;
            }

            var valueTypeInstance = GetValueTypeInstance(serializedValue);
            var template = new WidgetAttributeTemplate
            {
                KeyType = keyTypeInstance,
                ValueType = valueTypeInstance,
                Key = keyTypeInstance.GetKeyName(serializedKey),
                Value = valueTypeInstance.GetAttributeValue(serializedValue)
            };
            kvp.Value[template.Key] = template;
            return;
        }

        // No specific type matched — use the catch-all attribute slot.
        if (catchAllSlot.HasValue)
        {
            var keyTypeInstance = GetOrCreateKeyTypeInstance(catchAllSlot.Value.Key);
            var valueTypeInstance = GetValueTypeInstance(serializedValue);
            var template = new WidgetAttributeTemplate
            {
                KeyType = keyTypeInstance,
                ValueType = valueTypeInstance,
                Key = keyTypeInstance.GetKeyName(serializedKey),
                Value = valueTypeInstance.GetAttributeValue(serializedValue)
            };
            catchAllSlot.Value.Value[template.Key] = template;
            return;
        }

        TaleWorlds.Library.Debug.Print(
            $"[TAOM] CareerButtonPrefab: No key-type slot for attribute '{serializedKey}' — skipped.",
            0, TaleWorlds.Library.Debug.DebugColor.Yellow);
    }

    // Cache of key-type instances by concrete type.
    private static readonly Dictionary<Type, WidgetAttributeKeyType> s_keyTypeCache = new();

    private static WidgetAttributeKeyType GetOrCreateKeyTypeInstance(Type keyType)
    {
        if (s_keyTypeCache.TryGetValue(keyType, out var inst))
            return inst;
        try
        {
            inst = (WidgetAttributeKeyType)Activator.CreateInstance(keyType);
            s_keyTypeCache[keyType] = inst;
        }
        catch
        {
            // Some key types may not have parameterless constructors — skip.
        }
        return inst;
    }

    // Value type detection mirrors WidgetAttributeContext.GetValueType() order.
    // Binding values start with "@", everything else is Default.
    // We only need Default and Binding for our button attributes.
    private static WidgetAttributeValueType GetValueTypeInstance(string value)
    {
        if (value.StartsWith("@"))
            return BindingValueType.Instance;
        return DefaultValueType.Instance;
    }

    private static WidgetTemplate BuildButtonTemplate(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> keyStructure)
    {
        var template = new WidgetTemplate("ButtonWidget");
        var attrs = CloneDictionary(keyStructure);
        s_attributesField.SetValue(template, attrs);

        AddAttribute(attrs, "WidthSizePolicy", "Fixed");
        AddAttribute(attrs, "HeightSizePolicy", "Fixed");
        AddAttribute(attrs, "SuggestedWidth", "200");
        AddAttribute(attrs, "SuggestedHeight", "40");
        AddAttribute(attrs, "HorizontalAlignment", "Center");
        AddAttribute(attrs, "VerticalAlignment", "Top");
        AddAttribute(attrs, "MarginTop", "155");
        AddAttribute(attrs, "IsVisible", "@HasCareer");
        AddAttribute(attrs, "Command.Click", "ExecuteOpenCareerScreen");
        AddAttribute(attrs, "DoNotPassEventsToChildren", "true");
        AddAttribute(attrs, "UpdateChildrenStates", "true");
        AddAttribute(attrs, "Brush", "ButtonBrush1");

        return template;
    }

    private static WidgetTemplate BuildTextTemplate(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> keyStructure)
    {
        var template = new WidgetTemplate("TextWidget");
        var attrs = CloneDictionary(keyStructure);
        s_attributesField.SetValue(template, attrs);

        AddAttribute(attrs, "WidthSizePolicy", "StretchToParent");
        AddAttribute(attrs, "HeightSizePolicy", "StretchToParent");
        AddAttribute(attrs, "Text", "Career");
        AddAttribute(attrs, "Brush", "ButtonBrush1.Text");

        return template;
    }

    private static Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> CloneDictionary(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> source)
    {
        var clone = new Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>>(source.Count);
        foreach (var kvp in source)
            clone[kvp.Key] = new Dictionary<string, WidgetAttributeTemplate>();
        return clone;
    }

    // Lightweight singleton value-type instances to avoid repeated allocations.
    private static class DefaultValueType
    {
        internal static readonly TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault Instance =
            new TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault();
    }

    private static class BindingValueType
    {
        // WidgetAttributeValueTypeBinding lives in TaleWorlds.GauntletUI.Data.
        // Resolve it by name to avoid a direct reference to that assembly's type
        // in case the dependency chain differs between versions.
        internal static readonly WidgetAttributeValueType Instance = CreateBindingType();

        private static WidgetAttributeValueType CreateBindingType()
        {
            var type = Type.GetType(
                "TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding, TaleWorlds.GauntletUI.Data",
                throwOnError: false);
            if (type != null)
                return (WidgetAttributeValueType)Activator.CreateInstance(type);

            // Fallback: treat as default (binding won't work but won't crash).
            TaleWorlds.Library.Debug.Print(
                "[TAOM] CareerButtonPrefab: Could not resolve WidgetAttributeValueTypeBinding — IsVisible binding disabled.",
                0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            return new TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeValueTypeDefault();
        }
    }
}
