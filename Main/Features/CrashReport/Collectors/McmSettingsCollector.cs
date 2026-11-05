using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Reflection-based dump of TAOM's own MCM settings classes. We don't take a direct dependency on
// MCM's settings API surface because MCM's API changes between minor versions. All reads are defensive.
//
// Strategy:
// 1. Scan only TAOM's own assembly (TAOM.dll) for concrete types deriving from MCM's AttributeGlobalSettings<>,
//    AttributePerCampaignSettings<>, GlobalSettings<> or PerCampaignSettings<> (resolved by name, no compile-time dep).
// 2. Read only a class that declares its own static `Instance` property (TAOM's four), then
//    reflect over its public instance properties, skipping MCM metadata (Id, DisplayName, FormatType,
//    FolderName, SubFolder, SubGroupDelimiter, DiscoveryType, etc.).
// 3. Stringify each value safely.
//
// Third-party classes are skipped by design (#982). The ownership rule is the assembly check: a foreign assembly's
// types are never enumerated, so no third-party static constructor, constructor, Instance getter, Id or property
// getter runs inside crash handling (off the main thread), and none of its free-text settings reach a bundle the
// player uploads. DeclaredOnly on `Instance` is the second guard. The module list already names every mod and version.
public sealed class McmSettingsCollector
{
    private static readonly HashSet<string> SkipPropertyNames = new(StringComparer.Ordinal)
    {
        "Id", "DisplayName", "FormatType", "FolderName", "SubFolder", "SubGroupDelimiter",
        "UIVersion", "PropertyGroups", "OverrideDeepCloning", "MinimumGameVersion",
        "DiscoveryType"
    };

    public McmSettingsSnapshot Collect()
    {
        var providers = new List<McmProviderSnapshot>();
        try
        {
            var settingsBase = ResolveMcmSettingsBaseTypes();
            if (settingsBase.Count == 0) return new McmSettingsSnapshot(Array.Empty<McmProviderSnapshot>());

            var ownAssembly = typeof(McmSettingsCollector).Assembly;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Ownership gate (#982): a foreign assembly's types are never enumerated, so none of its settings classes are read.
                if (asm != ownAssembly) continue;

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

    // internal for TAOM.Tests (InternalsVisibleTo).
    internal static object? ResolveInstance(Type t)
    {
        try
        {
            // Second guard. The ownership rule is the TAOM.dll assembly check in Collect(). An inherited
            // GlobalSettings<T>.Instance would run `new T()` on a cold cache, so only a class that declares its own
            // Instance (TAOM's four) is read.
            var prop = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            return prop?.GetValue(null);
        }
        catch { return null; }
    }

    // internal for TAOM.Tests (InternalsVisibleTo).
    internal static IReadOnlyList<McmSettingEntry> ExtractSettingEntries(object instance, Type t)
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

    internal static string StringifyValue(object? v)
    {
        if (v == null) return "(null)";
        if (v is string s) return s;
        if (v is float f) return f.ToString("R", CultureInfo.InvariantCulture);
        if (v is double d) return d.ToString("R", CultureInfo.InvariantCulture);
        if (v is IConvertible c) return c.ToString(CultureInfo.InvariantCulture);
        // MCM's Dropdown<T> has no ToString override, so it would print its type name. Show the selection, the same
        // SelectedValue read SettingsFingerprint hashes; an index left out of range by a removed option throws there.
        var type = v.GetType();
        if (type.Name.IndexOf("Dropdown", StringComparison.Ordinal) >= 0 && type.GetProperty("SelectedValue") is { } selected)
        {
            try { return StringifyValue(selected.GetValue(v)); }
            catch (Exception ex) { return $"(dropdown-read-failed: {(ex.InnerException ?? ex).GetType().Name})"; }
        }
        try { return v.ToString() ?? "(toString-null)"; }
        catch (Exception ex) { return $"(toString-failed: {ex.GetType().Name})"; }
    }

    private static string? SafeReadString(Func<string?> f) { try { return f(); } catch { return null; } }
}
