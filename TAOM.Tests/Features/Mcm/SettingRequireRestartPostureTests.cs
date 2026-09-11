using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CrashReport;

namespace TAOM.Tests.Features.Mcm;

/// <summary>
/// MCM's <c>BaseSettingPropertyAttribute</c> constructor defaults <c>requireRestart</c> to TRUE. With
/// it true, pressing Done on a changed setting raises "Game Needs to Restart"; Yes saves and quits,
/// and Cancel is an EMPTY delegate followed by <c>return</c> (decompiled
/// <c>ModOptionsVM.ExecuteDone</c>, MBOptionScreen v1.4.5), so the change is never written to
/// <c>TAOM.json</c>. It still applies for the rest of the session because the undo stack writes
/// through to the live instance as the slider moves, which is exactly what makes it look like it
/// took, until the next launch reverts it. Player-reported for Troop Weight 2026-09-06 and for
/// Bandit Scaling 2026-09-11 (#559).
///
/// Every TAOM setting is read live through <c>TaomSettings.Instance</c> (no Harmony category is
/// gated on a setting at apply time), so the honest posture is <c>RequireRestart = false</c>
/// everywhere, and a new setting that omits the flag is a bug this test catches. The allowlist
/// holds two kinds of exception: a setting whose consumer is parked (commented out in SubModule.cs),
/// where a restart does not help either but flipping the flag would promise an effect that does not
/// exist; and the two CrashReport gates that SubModule.OnSubModuleLoad reads once to decide whether
/// to install the patches at all, where a restart genuinely is the only way to turn them on.
///
/// MCMv5.dll is a runtime-only dependency of the test project, so the attribute is read by name
/// rather than by type, the same way MCM's own <c>BasePropertyDefinitionWrapper</c> reads it.
/// </summary>
[TestClass]
public class SettingRequireRestartPostureTests
{
    private static readonly IReadOnlyDictionary<string, string> RestartAllowlist = new Dictionary<string, string>
    {
        [nameof(TaomSettings.EnableNativeSkinFixes)] = "PARKED 2026-07-08: the install call is commented out in SubModule.cs, the toggle drives nothing",
        [nameof(CrashReportSettings.EnableCrashCapture)] = "gates PatchCategory(Patch37_CrashReport) in OnSubModuleLoad: off is live, on needs a launch",
        [nameof(CrashReportSettings.EnableNativeToManagedCapture)] = "gates Native2ManagedPatcher.AttachAll in OnSubModuleLoad: installed once at launch",
    };

    private static readonly Type[] SettingsClasses =
    {
        typeof(TaomSettings),
        typeof(BattleLoadDiagnosticsSettings),
        typeof(CrashReportSettings),
        typeof(BlowDiagnosticsSettings),
    };

    [TestMethod]
    public void EveryValueSetting_IsReadLive_SoRequireRestartIsFalse()
    {
        var offenders = new List<string>();
        var seen = 0;

        foreach (var type in SettingsClasses)
        foreach (var (property, attribute) in ValueSettingAttributes(type))
        {
            seen++;
            var requireRestart = ReadRequireRestart(attribute);
            if (!requireRestart) continue;
            if (RestartAllowlist.ContainsKey(property.Name)) continue;
            offenders.Add($"{type.Name}.{property.Name}");
        }

        Assert.IsTrue(seen > 200, $"expected to see every TAOM setting, saw {seen}; the attribute filter is wrong");
        Assert.AreEqual(0, offenders.Count,
            "These settings omit RequireRestart = false, so MCM prompts for a restart and discards the change on Cancel:\n  "
            + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void RestartAllowlist_NamesOnlyRealSettings_ThatStillRequireRestart()
    {
        var all = SettingsClasses
            .SelectMany(ValueSettingAttributes)
            .ToDictionary(pair => pair.Item1.Name, pair => pair.Item2);

        foreach (var entry in RestartAllowlist)
        {
            Assert.IsTrue(all.TryGetValue(entry.Key, out var attribute),
                $"allowlist names '{entry.Key}', which is no longer a setting: remove the entry ({entry.Value})");
            Assert.IsTrue(ReadRequireRestart(attribute),
                $"allowlist names '{entry.Key}', which already has RequireRestart = false: remove the entry ({entry.Value})");
        }
    }

    private static IEnumerable<(PropertyInfo, Attribute)> ValueSettingAttributes(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var attribute in property.GetCustomAttributes())
            {
                var name = attribute.GetType().Name;
                if (!name.StartsWith("SettingProperty", StringComparison.Ordinal)) continue;
                // The group attribute carries no RequireRestart; a button has no value to persist.
                if (name == "SettingPropertyGroupAttribute" || name == "SettingPropertyButtonAttribute") continue;
                yield return (property, attribute);
            }
        }
    }

    private static bool ReadRequireRestart(Attribute attribute)
    {
        var prop = attribute.GetType().GetProperty("RequireRestart", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(prop, $"{attribute.GetType().FullName} has no RequireRestart property; MCM's attribute shape changed");
        return (bool)prop.GetValue(attribute);
    }
}
