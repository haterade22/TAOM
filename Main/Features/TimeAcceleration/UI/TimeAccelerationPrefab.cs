using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.GauntletUI.PrefabSystem;
using TAOM.Core.UI;

namespace TAOM.Features.TimeAcceleration.UI;

/// <summary>
/// Registers MapBar prefab modifications with WidgetPrefabPatcher, replacing the
/// UIExtenderEx PrefabExtension classes that formerly handled this work.
///
/// Changes applied:
///   - CenterPanel:      SuggestedWidth → 500
///   - FastForwardButton: PositionXOffset → -105
///   - PlayButton:        PositionXOffset → -145
///   - PauseButton:       PositionXOffset → -185
///   - Inserts FastFastForwardButton as last child of CenterPanel (after PauseButton)
/// </summary>
internal static class TimeAccelerationPrefab
{
    // Reflected WidgetTemplate internals — accessed once, shared across patches.
    private static readonly FieldInfo s_attributesField =
        typeof(WidgetTemplate).GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo s_childrenField =
        typeof(WidgetTemplate).GetField("_children", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Register()
    {
        WidgetPrefabPatcher.Register("MapBar", ApplyPatch);
    }

    // -------------------------------------------------------------------------
    // Main patch action
    // -------------------------------------------------------------------------

    private static void ApplyPatch(WidgetPrefab prefab)
    {
        var root = prefab.RootTemplate;
        if (root == null)
            return;

        // Obtain the attribute-key structure from the root to clone into new templates.
        var keyStructure = CloneAttributeKeyStructure(root);

        // 1. Widen CenterPanel to make room for the extra button.
        var centerPanel = FindById(root, "CenterPanel");
        if (centerPanel != null)
            SetAttr(centerPanel, "SuggestedWidth", "500");

        // 2. Shift vanilla FastForward button left.
        var fastForward = FindById(root, "FastForwardButton");
        if (fastForward != null)
            SetAttr(fastForward, "PositionXOffset", "-105");

        // 3. Shift vanilla Play button left.
        var play = FindById(root, "PlayButton");
        if (play != null)
            SetAttr(play, "PositionXOffset", "-145");

        // 4. Shift vanilla Pause button left.
        var pause = FindById(root, "PauseButton");
        if (pause != null)
            SetAttr(pause, "PositionXOffset", "-185");

        // 5. Insert extra fast-forward button as last child of CenterPanel,
        //    placed after PauseButton (mirrors UIExtenderEx InsertType.Append on PauseButton).
        if (centerPanel != null)
        {
            var button = BuildButtonTemplate(keyStructure, prefab);
            var centerChildren = (List<WidgetTemplate>)s_childrenField.GetValue(centerPanel);
            centerChildren.Add(button);
        }
    }

    // -------------------------------------------------------------------------
    // Widget tree traversal
    // -------------------------------------------------------------------------

    private static WidgetTemplate FindById(WidgetTemplate node, string id)
    {
        if (node.Id == id)
            return node;

        for (int i = 0; i < node.ChildCount; i++)
        {
            var found = FindById(node.GetChildAt(i), id);
            if (found != null)
                return found;
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Attribute mutation on existing (already-loaded) WidgetTemplate nodes
    // -------------------------------------------------------------------------

    private static void SetAttr(WidgetTemplate template, string key, string value)
    {
        // The template's _attributes dict is populated by LoadAttributeCollection at load time,
        // so SetAttribute() works without pre-population.
        var keyTypeInstance = new WidgetAttributeKeyTypeAttribute();
        var valueTypeInstance = DefaultValueType.Instance;

        var attr = new WidgetAttributeTemplate
        {
            KeyType   = keyTypeInstance,
            ValueType = valueTypeInstance,
            Key       = keyTypeInstance.GetKeyName(key),
            Value     = valueTypeInstance.GetAttributeValue(value)
        };
        template.SetAttribute(attr);
    }

    // -------------------------------------------------------------------------
    // Build the FastFastForwardButton widget template
    // -------------------------------------------------------------------------

    private static WidgetTemplate BuildButtonTemplate(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> keyStructure,
        WidgetPrefab prefab)
    {
        var button = new WidgetTemplate("ButtonWidget");
        var buttonAttrs = CloneDictionary(keyStructure);
        s_attributesField.SetValue(button, buttonAttrs);

        AddAttribute(buttonAttrs, "Id",                    "FastFastForwardButton");
        AddAttribute(buttonAttrs, "WidthSizePolicy",        "Fixed");
        AddAttribute(buttonAttrs, "HeightSizePolicy",       "Fixed");
        AddAttribute(buttonAttrs, "SuggestedWidth",         "35");
        AddAttribute(buttonAttrs, "SuggestedHeight",        "24");
        AddAttribute(buttonAttrs, "HorizontalAlignment",    "Right");
        AddAttribute(buttonAttrs, "VerticalAlignment",      "Bottom");
        AddAttribute(buttonAttrs, "PositionXOffset",        "-62");
        AddAttribute(buttonAttrs, "PositionYOffset",        "-13");
        AddAttribute(buttonAttrs, "Brush",                  "MapBarFastForwardButton");
        AddAttribute(buttonAttrs, "IsSelected",             "@IsExtraFastForwardActive");
        AddAttribute(buttonAttrs, "Command.Click",          "ExecuteTimeControlChange");
        AddAttribute(buttonAttrs, "CommandParameter.Click", "2");
        AddAttribute(buttonAttrs, "GamepadNavigationIndex", "4");

        var hint = BuildHintTemplate(keyStructure);

        // Wire hint as child of button before SetRootTemplate so recursive walk covers it.
        var buttonChildren = (List<WidgetTemplate>)s_childrenField.GetValue(button);
        buttonChildren.Add(hint);

        // Propagate Prefab reference through the full subtree.
        button.SetRootTemplate(prefab);
        return button;
    }

    private static WidgetTemplate BuildHintTemplate(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> keyStructure)
    {
        var hint = new WidgetTemplate("HintWidget");
        var hintAttrs = CloneDictionary(keyStructure);
        s_attributesField.SetValue(hint, hintAttrs);

        AddAttribute(hintAttrs, "DataSource",         "{ExtraFastForwardHint}");
        AddAttribute(hintAttrs, "WidthSizePolicy",    "StretchToParent");
        AddAttribute(hintAttrs, "HeightSizePolicy",   "StretchToParent");
        AddAttribute(hintAttrs, "Command.HoverBegin", "ExecuteBeginHint");
        AddAttribute(hintAttrs, "Command.HoverEnd",   "ExecuteEndHint");
        AddAttribute(hintAttrs, "IsDisabled",         "true");

        return hint;
    }

    // -------------------------------------------------------------------------
    // Attribute helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a copy of the attribute-key structure from <paramref name="source"/> —
    /// a Dictionary&lt;Type, Dictionary&lt;string, WidgetAttributeTemplate&gt;&gt; with the
    /// same key types but empty inner dictionaries.
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

    private static Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> CloneDictionary(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> source)
    {
        var clone = new Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>>(source.Count);
        foreach (var kvp in source)
            clone[kvp.Key] = new Dictionary<string, WidgetAttributeTemplate>();
        return clone;
    }

    /// <summary>
    /// Adds a WidgetAttributeTemplate to the correct slot in <paramref name="attrs"/>,
    /// choosing the slot whose key-type instance accepts the serialized attribute name.
    /// Falls back gracefully with a log message if no key-type matches.
    /// </summary>
    private static void AddAttribute(
        Dictionary<Type, Dictionary<string, WidgetAttributeTemplate>> attrs,
        string serializedKey,
        string serializedValue)
    {
        foreach (var kvp in attrs)
        {
            var keyTypeInstance = GetOrCreateKeyTypeInstance(kvp.Key);
            if (keyTypeInstance == null || !keyTypeInstance.CheckKeyType(serializedKey))
                continue;

            var valueTypeInstance = GetValueTypeInstance(serializedValue);
            var template = new WidgetAttributeTemplate
            {
                KeyType   = keyTypeInstance,
                ValueType = valueTypeInstance,
                Key       = keyTypeInstance.GetKeyName(serializedKey),
                Value     = valueTypeInstance.GetAttributeValue(serializedValue)
            };
            kvp.Value[template.Key] = template;
            return;
        }

        TaleWorlds.Library.Debug.Print(
            $"[TAOM] TimeAccelerationPrefab: No key-type slot for attribute '{serializedKey}' — skipped.",
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
            // Key types without parameterless constructors are skipped.
        }
        return inst;
    }

    // Value type detection mirrors WidgetAttributeContext.GetValueType() order.
    // "@..." = property binding, "{...}" = DataSource binding path, else = default literal.
    private static WidgetAttributeValueType GetValueTypeInstance(string value)
    {
        if (value.StartsWith("@"))
            return BindingValueType.Instance;
        if (value.StartsWith("{"))
            return BindingPathValueType.Instance;
        return DefaultValueType.Instance;
    }

    private static class DefaultValueType
    {
        internal static readonly WidgetAttributeValueTypeDefault Instance =
            new WidgetAttributeValueTypeDefault();
    }

    private static class BindingValueType
    {
        internal static readonly WidgetAttributeValueType Instance = ResolveType(
            "TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBinding, TaleWorlds.GauntletUI.Data");
    }

    private static class BindingPathValueType
    {
        internal static readonly WidgetAttributeValueType Instance = ResolveType(
            "TaleWorlds.GauntletUI.Data.WidgetAttributeValueTypeBindingPath, TaleWorlds.GauntletUI.Data");
    }

    private static WidgetAttributeValueType ResolveType(string assemblyQualifiedName)
    {
        var type = Type.GetType(assemblyQualifiedName, throwOnError: false);
        if (type != null)
            return (WidgetAttributeValueType)Activator.CreateInstance(type);

        TaleWorlds.Library.Debug.Print(
            $"[TAOM] TimeAccelerationPrefab: Could not resolve {assemblyQualifiedName}",
            0, TaleWorlds.Library.Debug.DebugColor.Yellow);
        return new WidgetAttributeValueTypeDefault();
    }
}
