using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Common;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// Every settable property of the four settings classes, read once by reflection. "Settable" is the definition
/// <c>SettingsFingerprint</c> and <c>SettingsFingerprintTests.SettableProperties</c> use (public instance,
/// readable, writable, not indexed), so the file covers exactly what the co-op fingerprint can see.
/// </summary>
internal static class ServerSettingsSchema
{
    private static readonly Lazy<IReadOnlyList<ServerSettingDescriptor>> Cached = new(Build);
    private static readonly Lazy<IReadOnlyDictionary<string, ServerSettingDescriptor>> CachedByName =
        new(() => All.ToDictionary(d => d.Name, StringComparer.Ordinal));

    public static IReadOnlyList<ServerSettingDescriptor> All => Cached.Value;

    /// <summary>Ordinal, so a key that differs only in case is unknown.</summary>
    public static IReadOnlyDictionary<string, ServerSettingDescriptor> ByName => CachedByName.Value;

    private static IReadOnlyList<ServerSettingDescriptor> Build()
    {
        var defaults = new ServerSettingsSet();
        var list = new List<ServerSettingDescriptor>();
        foreach (var type in ServerSettingsSet.Classes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.MetadataToken);
            foreach (var p in properties)
                list.Add(Describe(p, defaults.For(p.DeclaringType!)));
        }

        return list;
    }

    private static ServerSettingDescriptor Describe(PropertyInfo p, object defaults)
    {
        var group = McmText.Plain(p.GetCustomAttribute<SettingPropertyGroupAttribute>(inherit: true)?.GroupName);
        var none = Array.Empty<string>();
        var kinds = p.GetCustomAttributes(inherit: true).OfType<BaseSettingPropertyAttribute>().ToList();
        if (kinds.Count == 0)
            return new ServerSettingDescriptor(p, ServerSettingKind.Parked, 0, 0, none, group);
        if (kinds.Count != 1)
            return new ServerSettingDescriptor(p, ServerSettingKind.Unsupported, 0, 0, none, group);

        var type = p.PropertyType;
        switch (kinds[0])
        {
            case SettingPropertyBoolAttribute when type == typeof(bool):
                return new ServerSettingDescriptor(p, ServerSettingKind.Bool, 0, 0, none, group);
            case SettingPropertyIntegerAttribute i when type == typeof(int):
                return new ServerSettingDescriptor(p, ServerSettingKind.Int, (double)i.MinValue, (double)i.MaxValue, none, group);
            case SettingPropertyFloatingIntegerAttribute f when type == typeof(float):
                return new ServerSettingDescriptor(p, ServerSettingKind.Float, (double)f.MinValue, (double)f.MaxValue, none, group);
            case SettingPropertyTextAttribute when type == typeof(string):
                return new ServerSettingDescriptor(p, ServerSettingKind.Text, 0, 0, none, group);
            case SettingPropertyDropdownAttribute when type == typeof(Dropdown<string>):
                var options = ((Dropdown<string>?)p.GetValue(defaults))?.ToList() ?? new List<string>();
                return new ServerSettingDescriptor(p, ServerSettingKind.Dropdown, 0, 0, options, group);
            case SettingPropertyButtonAttribute when type == typeof(Action):
                return new ServerSettingDescriptor(p, ServerSettingKind.Button, 0, 0, none, group);
            default:
                return new ServerSettingDescriptor(p, ServerSettingKind.Unsupported, 0, 0, none, group);
        }
    }
}
