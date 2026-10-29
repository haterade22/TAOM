using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CrashReport;
using TAOM.Features.ServerSettings;

namespace TAOM.Tests.Features.ServerSettings;

/// <summary>
/// The schema is reflection over the four MCM settings classes, so these gates fail the moment someone adds a
/// setting of a shape the server file cannot carry, instead of the setting silently being unsettable.
/// </summary>
[TestClass]
public class ServerSettingsSchemaTests
{
    private static readonly Type[] Classes =
    {
        typeof(TaomSettings), typeof(BattleLoadDiagnosticsSettings),
        typeof(BlowDiagnosticsSettings), typeof(CrashReportSettings),
    };

    // Same definition as SettingsFingerprintTests.SettableProperties.
    private static IEnumerable<PropertyInfo> SettableProperties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

    [TestCleanup]
    public void Cleanup() => ServerSettingsSource.ResetForTests();

    [TestMethod]
    public void EverySettableProperty_HasExactlyOneKnownKind()
    {
        var bad = new List<string>();
        foreach (var type in Classes)
        {
            foreach (var p in SettableProperties(type))
            {
                var kinds = p.GetCustomAttributes(inherit: true)
                    .Select(a => a.GetType().Name)
                    .Where(n => n is "SettingPropertyBoolAttribute" or "SettingPropertyIntegerAttribute"
                        or "SettingPropertyFloatingIntegerAttribute" or "SettingPropertyTextAttribute"
                        or "SettingPropertyDropdownAttribute" or "SettingPropertyButtonAttribute")
                    .ToList();
                var parked = Parked.Contains(type.Name + "." + p.Name);
                if (parked ? kinds.Count != 0 : kinds.Count != 1)
                    bad.Add(type.Name + "." + p.Name + " has " + kinds.Count + " kind attributes"
                            + (parked ? " but is listed as parked" : ""));
            }
        }

        Assert.AreEqual(0, bad.Count, string.Join("; ", bad));
    }

    // Settable properties whose MCM attribute stack is commented out (ShaderPrecompilation is parked, TaomSettings.cs).
    // MCM neither loads nor saves them, so the file does not either. Un-parking restores the attributes and removes the row.
    private static readonly HashSet<string> Parked = new(StringComparer.Ordinal)
    {
        "TaomSettings.EnableShaderPrecompilation",
        "TaomSettings.EnableShaderPrecompileScenePasses",
    };

    [TestMethod]
    public void ParkedDescriptors_AreExactlyTheListedPair()
    {
        var parked = ServerSettingsSchema.All.Where(d => d.Kind == ServerSettingKind.Parked)
            .Select(d => d.Owner.Name + "." + d.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(Parked.OrderBy(n => n, StringComparer.Ordinal).ToList(), parked);
        Assert.IsFalse(ServerSettingsSchema.All.Single(d => d.Name == "EnableShaderPrecompilation").IsSettableFromFile);
    }

    [TestMethod]
    public void SchemaCount_EqualsFingerprintReflection_ForEachClass()
    {
        foreach (var type in Classes)
        {
            var expected = SettableProperties(type).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var actual = ServerSettingsSchema.All.Where(d => d.Owner == type)
                .Select(d => d.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            CollectionAssert.AreEqual(expected, actual, type.Name);
        }

        Assert.AreEqual(503, ServerSettingsSchema.All.Count);
    }

    [TestMethod]
    public void NoDescriptor_IsUnsupported()
    {
        var unsupported = ServerSettingsSchema.All.Where(d => d.Kind == ServerSettingKind.Unsupported)
            .Select(d => d.Name).ToList();

        Assert.AreEqual(0, unsupported.Count, "unsupported setting shapes: " + string.Join(", ", unsupported));
    }

    [TestMethod]
    public void EveryCompiledDefault_PassesItsOwnValidation()
    {
        var failures = new List<string>();
        foreach (var d in ServerSettingsSchema.All.Where(d => d.IsSettableFromFile))
        {
            var fresh = new ServerSettingsSet();
            var reason = ServerSettingsReader.Validate(d, ServerSettingsDefaultsRenderer.DefaultToken(d, fresh), out _);
            if (reason != null)
                failures.Add(d.Owner.Name + "." + d.Name + ": " + reason);
        }

        Assert.AreEqual(0, failures.Count, "defaults that fail their own validation:\n" + string.Join("\n", failures));
    }

    [TestMethod]
    public void DropdownDescriptors_CarryTheirOptions()
    {
        var dropdowns = ServerSettingsSchema.All.Where(d => d.Kind == ServerSettingKind.Dropdown).ToList();

        Assert.AreEqual(6, dropdowns.Count);
        Assert.IsTrue(dropdowns.All(d => d.Options.Count >= 2), "a dropdown has fewer than two options");
    }
}
