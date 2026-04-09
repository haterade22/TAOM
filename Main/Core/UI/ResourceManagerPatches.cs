using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace TAOM.Core.UI;

/// <summary>
/// Patches BrushFactory and WidgetFactory so the game finds TAOM's custom brushes
/// and widget types. Replicates the essential functionality from UIExtenderEx's
/// BrushFactoryManager and WidgetFactoryManager.
///
/// Without these patches, the JIT inlines several small methods in the factory
/// classes, which prevents Harmony hooks from intercepting them. The BlankTranspiler
/// forces re-compilation without inlining.
/// </summary>
[HarmonyPatchCategory("Patch_ResourceManager")]
internal static class ResourceManagerPatches
{
    // --- Custom Brush Support ---

    private static readonly Dictionary<string, Brush> _customBrushes = new();

    public static void RegisterBrush(Brush brush)
    {
        _customBrushes[brush.Name] = brush;
    }

    // Prefix on BrushFactory.GetBrush — if vanilla doesn't have it, check our custom brushes
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BrushFactory), nameof(BrushFactory.GetBrush))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool GetBrushPrefix(string name, IReadOnlyDictionary<string, Brush> ____brushes, ref Brush __result)
    {
        if (____brushes.ContainsKey(name) || !_customBrushes.ContainsKey(name))
            return true; // let vanilla handle it

        __result = _customBrushes[name];
        return false;
    }

    // Postfix on BrushFactory.Brushes getter — append custom brushes to enumeration
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BrushFactory), nameof(BrushFactory.Brushes), MethodType.Getter)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void GetBrushesPostfix(ref IEnumerable<Brush> __result)
    {
        if (_customBrushes.Count > 0)
            __result = __result.Concat(_customBrushes.Values);
    }

    // --- Custom Widget Type Support ---

    private static readonly Dictionary<string, Type> _builtinTypes = new();

    public static void RegisterWidgetType(Type widgetType)
    {
        _builtinTypes[widgetType.Name] = widgetType;
    }

    // Prefix on WidgetFactory.CreateBuiltinWidget — handle TAOM custom widget types
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WidgetFactory), nameof(WidgetFactory.CreateBuiltinWidget))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool CreateBuiltinWidgetPrefix(UIContext context, string typeName, ref Widget __result)
    {
        if (!_builtinTypes.TryGetValue(typeName, out var type))
            return true;

        var ctor = type.GetConstructor(new[] { typeof(UIContext) });
        if (ctor == null)
            return true;

        __result = (Widget)ctor.Invoke(new object[] { context });
        return false;
    }

    // Postfix on WidgetFactory.GetWidgetTypes — include TAOM types in enumeration
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WidgetFactory), nameof(WidgetFactory.GetWidgetTypes))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void GetWidgetTypesPostfix(ref IEnumerable<string> __result)
    {
        if (_builtinTypes.Count > 0)
            __result = __result.Concat(_builtinTypes.Keys);
    }

    // --- Anti-Inlining Transpilers ---
    // These force the JIT to not inline certain small methods, which is required
    // for Harmony prefix/postfix hooks to work. Without them, the hooks are bypassed.

    [HarmonyTranspiler]
    [HarmonyPatch("TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinition", "GetValue")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<CodeInstruction> ConstantDefinition_GetValue_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions;

    [HarmonyTranspiler]
    [HarmonyPatch("TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions", "SetWidgetAttributeFromString")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<CodeInstruction> SetWidgetAttributeFromString_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions;

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIContext), "GetBrush")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<CodeInstruction> UIContext_GetBrush_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions;

    [HarmonyTranspiler]
    [HarmonyPatch("TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions", "ConvertObject")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<CodeInstruction> ConvertObject_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions;

    [HarmonyTranspiler]
    [HarmonyPatch("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate", "CreateWidgets")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<CodeInstruction> CreateWidgets_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions;

    [HarmonyTranspiler]
    [HarmonyPatch("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate", "OnRelease")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<CodeInstruction> OnRelease_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions;

    [HarmonyTranspiler]
    [HarmonyPatch("TaleWorlds.GauntletUI.Data.GauntletMovie", "LoadMovie")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static IEnumerable<CodeInstruction> LoadMovie_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions;
}
