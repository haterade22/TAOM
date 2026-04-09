using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Library;

namespace TAOM.Core.UI;

/// <summary>
/// Stores mixin data on ViewModel instances using ConditionalWeakTable (no memory leaks).
/// Injects mixin properties/methods into the ViewModel's Gauntlet binding system.
/// </summary>
public static class ViewModelMixinSupport
{
    private static readonly ConditionalWeakTable<ViewModel, object> _mixinStore = new();

    private static readonly FieldInfo _propertiesAndMethodsField =
        AccessTools.Field(typeof(ViewModel), "_propertiesAndMethods");

    private static readonly Type _collectionType =
        typeof(ViewModel).GetNestedType("DataSourceTypeBindingPropertiesCollection", BindingFlags.NonPublic);

    private static readonly PropertyInfo _propertiesProperty =
        _collectionType?.GetProperty("Properties");

    private static readonly PropertyInfo _methodsProperty =
        _collectionType?.GetProperty("Methods");

    private static readonly ConstructorInfo _collectionCtor =
        _collectionType?.GetConstructor(new[] {
            typeof(Dictionary<string, PropertyInfo>),
            typeof(Dictionary<string, MethodInfo>)
        });

    public static TData GetOrCreate<TData>(ViewModel vm, Func<TData> factory) where TData : class
    {
        if (_mixinStore.TryGetValue(vm, out var existing))
            return (TData)existing;

        var data = factory();
        _mixinStore.Add(vm, data);
        return data;
    }

    public static bool TryGet<TData>(ViewModel vm, out TData data) where TData : class
    {
        if (_mixinStore.TryGetValue(vm, out var obj))
        {
            data = (TData)obj;
            return true;
        }
        data = default;
        return false;
    }

    public static void Remove(ViewModel vm)
    {
        _mixinStore.Remove(vm);
    }

    /// <summary>
    /// Injects mixin properties and methods into the ViewModel's Gauntlet binding dictionary.
    /// Clones the shared type-level dictionary into a per-instance one so mixin props
    /// don't leak to other ViewModel instances of the same type.
    /// </summary>
    public static void InjectBindings(ViewModel vm, object mixinInstance, Type mixinType)
    {
        if (_propertiesAndMethodsField == null || _collectionType == null)
            return;

        var collection = _propertiesAndMethodsField.GetValue(vm);
        if (collection == null)
            return;

        // Clone the shared dictionary into a per-instance copy
        var origProps = (Dictionary<string, PropertyInfo>)_propertiesProperty.GetValue(collection);
        var origMethods = (Dictionary<string, MethodInfo>)_methodsProperty.GetValue(collection);

        var newProps = new Dictionary<string, PropertyInfo>(origProps);
        var newMethods = new Dictionary<string, MethodInfo>(origMethods);

        // Inject mixin properties marked with [DataSourceProperty]
        foreach (var prop in mixinType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.GetCustomAttribute<DataSourceProperty>() != null)
            {
                newProps[prop.Name] = new WrappedPropertyInfo(prop, mixinInstance);
            }
        }

        // Inject mixin methods that start with "Execute" (Gauntlet command convention)
        foreach (var method in mixinType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (method.Name.StartsWith("Execute"))
            {
                newMethods[method.Name] = new WrappedMethodInfo(method, mixinInstance);
            }
        }

        // Replace the VM's binding collection with our expanded clone
        var newCollection = _collectionCtor.Invoke(new object[] { newProps, newMethods });
        _propertiesAndMethodsField.SetValue(vm, newCollection);
    }
}
