using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Reflection-based dump of every MCM-registered settings provider (TAOM + third-party).
// We don't take a direct dependency on MCM's settings API surface because (a) MCM's API
// changes between minor versions, and (b) third-party settings classes can throw inside
// their own getters. All reads are defensive.
//
// Strategy:
// 1. Scan loaded assemblies for types named "*Settings" derived from MCM's AttributeGlobalSettings<>
//    or AttributePerCampaignSettings<> base classes (resolved by name to avoid hard compile-time dep).
// 2. For each, locate the static `Instance` property and reflect over its public instance properties,
//    skipping MCM metadata properties (Id, DisplayName, FormatType, FolderName, SubFolder, SubGroupDelimiter, etc.).
// 3. Stringify each value safely.
public sealed class McmSettingsCollector
{
    private static readonly HashSet<string> SkipPropertyNames = new(StringComparer.Ordinal)
    {
        "Id", "DisplayName", "FormatType", "FolderName", "SubFolder", "SubGroupDelimiter",
        "UIVersion", "PropertyGroups", "OverrideDeepCloning", "MinimumGameVersion"
    };

    public McmSettingsSnapshot Collect()
    {
        var providers = new List<McmProviderSnapshot>();
        try
        {
            var settingsBase = ResolveMcmSettingsBaseTypes();
            if (settingsBase.Count == 0) return new McmSettingsSnapshot(Array.Empty<McmProviderSnapshot>());

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException tle) { types = tle.Types?.Where(t => t != null).Cast<Type>().ToArray() ?? Array.Empty<Type>(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!IsMcmSettingsType(type, settingsBase)) continue;

                    var settingsInstance = ResolveInstance(type);
                    if (settingsInstance == null) continue;
                    var entries = ExtractSettingEntries(settingsInstance, type);
                    providers.Add(new McmProviderSnapshot(
                        ProviderId: SafeReadString(() => (string?)type.GetProperty("Id")?.GetValue(settingsInstance)) ?? type.FullName ?? "(unknown)",
                        DisplayName: SafeReadString(() => (string?)type.GetProperty("DisplayName")?.GetValue(settingsInstance)) ?? type.Name,
                        AssemblyName: asm.GetName().Name ?? "(unknown)",
                        Settings: entries));
                }
            }
        }
        catch { /* whole-collector failure: return what we got */ }
        return new McmSettingsSnapshot(providers);
    }

    private static IReadOnlyList<Type> ResolveMcmSettingsBaseTypes()
    {
        var names = new[]
        {
            "MCM.Abstractions.Base.Global.AttributeGlobalSettings`1",
            "MCM.Abstractions.Base.PerCampaign.AttributePerCampaignSettings`1",
            "MCM.Abstractions.Base.Global.GlobalSettings`1",
            "MCM.Abstractions.Base.PerCampaign.PerCampaignSettings`1",
        };
        var list = new List<Type>();
        foreach (var n in names)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType(n, throwOnError: false);
                    if (t != null) { list.Add(t); break; }
                }
            }
            catch { }
        }
        return list;
    }

    private static bool IsMcmSettingsType(Type t, IReadOnlyList<Type> mcmBases)
    {
        try
        {
            var cur = t.BaseType;
            while (cur != null)
            {
                if (cur.IsGenericType)
                {
                    var def = cur.GetGenericTypeDefinition();
                    foreach (var b in mcmBases) if (b == def) return true;
                }
                cur = cur.BaseType;
            }
        }
        catch { }
        return false;
    }

    private static object? ResolveInstance(Type t)
    {
        try
        {
            var prop = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null);
        }
        catch { return null; }
    }

    private static IReadOnlyList<McmSettingEntry> ExtractSettingEntries(object instance, Type t)
    {
        var entries = new List<McmSettingEntry>();
        try
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead) continue;
                if (SkipPropertyNames.Contains(p.Name)) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                string value = "(read-failed)";
                try
                {
                    var v = p.GetValue(instance);
                    value = StringifyValue(v);
                }
                catch (Exception ex) { value = $"(read-failed: {ex.GetType().Name})"; }
                entries.Add(new McmSettingEntry(p.Name, value, p.PropertyType.FullName ?? p.PropertyType.Name));
            }
        }
        catch { }
        return entries.OrderBy(e => e.PropertyPath, StringComparer.Ordinal).ToList();
    }

    private static string StringifyValue(object? v)
    {
        if (v == null) return "(null)";
        if (v is string s) return s;
        if (v is float f) return f.ToString("R", CultureInfo.InvariantCulture);
        if (v is double d) return d.ToString("R", CultureInfo.InvariantCulture);
        if (v is IConvertible c) return c.ToString(CultureInfo.InvariantCulture);
        try { return v.ToString() ?? "(toString-null)"; }
        catch (Exception ex) { return $"(toString-failed: {ex.GetType().Name})"; }
    }

    private static string? SafeReadString(Func<string?> f) { try { return f(); } catch { return null; } }
}
