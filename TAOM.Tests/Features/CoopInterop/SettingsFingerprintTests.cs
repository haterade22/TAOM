using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CoopInterop;

// The co-op docs record that two peers with different MCM settings simulate differently with no
// warning, and that a fingerprint handshake was "designed but not built". These pin the built one.
//
// What a fingerprint has to get right, and what each test holds it to:
//   1. Same settings -> same code, every time. Reflection does not guarantee member order, so the
//      canonical text is sorted; without that the hash would drift between runs.
//   2. Different settings -> different code, and the RIGHT GROUP named. A global-only answer sends
//      a player through 105 checkboxes.
//   3. A locale must not fake a mismatch. 0.25f renders "0,25" under es-ES: two peers with identical
//      settings would diverge on the decimal separator alone. This is the failure that would have
//      made the feature worse than nothing.
//   4. An excluded setting must not move the hash — otherwise the warning fires on a nameplate
//      distance and gets ignored, which is the doc linter's 29-false-positives failure again.
//   5. Every property on the REAL TaomSettings is classified. That test fails when someone adds a
//      setting without deciding whether it is simulation-relevant, which is the only way this stays
//      correct after today.
[TestClass]
public class SettingsFingerprintTests
{
    // ------------------------------------------------------------------ fixtures

    /// <summary>Stands in for MCM's attribute: read by name, so tests need no MCM reference.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class SettingPropertyGroupAttribute : Attribute
    {
        public SettingPropertyGroupAttribute(string groupName) => GroupName = groupName;
        public string GroupName { get; }
    }

    private sealed class FakeSettings
    {
        [SettingPropertyGroup("Battle Tactics")]
        public bool EnableFlanking { get; set; } = true;

        [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
        public float ChargeDistance { get; set; } = 0.25f;

        [SettingPropertyGroup("World")]
        public int BanditDensity { get; set; } = 100;

        // Excluded by CoopSettingsRelevance (Presentation).
        [SettingPropertyGroup("Map UI")]
        public float MapFigureScale { get; set; } = 1.0f;

        // Excluded (Instrumentation) — and deliberately filed under a gameplay group, the shape
        // that made the group-label pass wrong.
        [SettingPropertyGroup("Battle Tactics")]
        public bool SmartCavalryDebug { get; set; }

        public string Ungrouped { get; set; } = "x";

        // Read-only: computed, not a setting. Must never enter the hash.
        public string DisplayName => "TAOM";
    }

    private static SettingsFingerprint.FingerprintReport Of(Action<FakeSettings> mutate = null)
    {
        var s = new FakeSettings();
        mutate?.Invoke(s);
        return SettingsFingerprint.Compute(s);
    }

    // ------------------------------------------------------------------ 1. determinism

    [TestMethod]
    public void SameSettings_ProduceTheSameFingerprint()
    {
        Assert.AreEqual(Of().Global, Of().Global);
    }

    [TestMethod]
    public void ReadOnlyMembers_AreNotSettings_AndDoNotEnterTheHash()
    {
        // DisplayName has no setter; if it were hashed, Covered would count it.
        var r = Of();
        Assert.AreEqual(4, r.Covered, "expected the 4 relevant properties, not the read-only member");
    }

    [TestMethod]
    public void UngroupedProperties_LandInTheirOwnBucket()
    {
        CollectionAssert.Contains(Of().ByGroup.Keys.ToList(), SettingsFingerprint.Ungrouped);
    }

    // ------------------------------------------------------------------ 2. sensitivity

    [TestMethod]
    public void ChangingARelevantSetting_ChangesTheGlobalFingerprint()
    {
        Assert.AreNotEqual(Of().Global, Of(s => s.BanditDensity = 150).Global);
    }

    [TestMethod]
    public void ChangingARelevantSetting_NamesOnlyItsOwnGroup()
    {
        var divergent = Of().DivergentGroups(Of(s => s.BanditDensity = 150));
        CollectionAssert.AreEqual(new[] { "World" }, divergent.ToArray());
    }

    [TestMethod]
    public void SubgroupSettings_ReportUnderTheirRootGroup()
    {
        // "Battle Tactics/Smart Cavalry" must report as "Battle Tactics" — the screen a player opens.
        var divergent = Of().DivergentGroups(Of(s => s.ChargeDistance = 0.5f));
        CollectionAssert.AreEqual(new[] { "Battle Tactics" }, divergent.ToArray());
    }

    [TestMethod]
    public void IdenticalSettings_ReportNoDivergentGroups()
    {
        Assert.AreEqual(0, Of().DivergentGroups(Of()).Count);
        Assert.IsTrue(Of().Matches(Of()));
    }

    // ------------------------------------------------------------------ 3. culture

    [TestMethod]
    public void ADifferentDecimalSeparator_DoesNotFakeAMismatch()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            var english = Of().Global;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
            var spanish = Of().Global;
            Assert.AreEqual(english, spanish,
                "identical settings hashed differently across locales — the decimal separator leaked in");
        }
        finally { Thread.CurrentThread.CurrentCulture = original; }
    }

    [TestMethod]
    public void FloatsRenderRoundTripExact()
    {
        // The property that matters: the rendered text parses back to the same float, so two
        // peers holding the same value always produce the same bytes.
        foreach (var f in new[] { 0.1f, 0.25f, 1f / 3f, 1e-7f, 12345.678f })
        {
            var rendered = SettingsFingerprint.Render(f);
            Assert.AreEqual(f, float.Parse(rendered, CultureInfo.InvariantCulture),
                $"'{rendered}' did not round-trip");
        }
    }

    [TestMethod]
    public void NeighbouringFloats_DoNotCollapseOntoEachOther()
    {
        // A fixed-decimal format would render both of these "0.13" and hide a real difference.
        Assert.AreNotEqual(SettingsFingerprint.Render(0.125f), SettingsFingerprint.Render(0.126f));
    }

    // ------------------------------------------------------------------ 4. exclusions

    [TestMethod]
    public void ChangingAnExcludedPresentationSetting_LeavesTheFingerprintAlone()
    {
        Assert.AreEqual(Of().Global, Of(s => s.MapFigureScale = 4.0f).Global);
    }

    [TestMethod]
    public void ChangingAnExcludedDebugSetting_LeavesTheFingerprintAlone()
    {
        // Filed under Battle Tactics; excluded by name because it gates a log line.
        Assert.AreEqual(Of().Global, Of(s => s.SmartCavalryDebug = true).Global);
    }

    // ------------------------------------------------------------------ 5. coverage on the real type

    [TestMethod]
    public void EveryRealSetting_IsEitherRelevantOrExplicitlyExcluded()
    {
        // The guard that keeps this correct after today: adding a setting to TaomSettings without
        // deciding its co-op relevance fails here rather than shipping a silent divergence.
        var props = typeof(TaomSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToList();

        Assert.IsTrue(props.Count > 100, $"expected TaomSettings to expose 100+ settings, saw {props.Count}");

        foreach (var p in props)
        {
            var classified = CoopSettingsRelevance.IsSimulationRelevant(p)
                             || CoopSettingsRelevance.IsExcluded(p.Name);
            Assert.IsTrue(classified, $"'{p.Name}' is unclassified for co-op relevance");
        }
    }

    [TestMethod]
    public void TheRealSettings_HashWithoutThrowing_AndCoverMostOfThem()
    {
        var report = SettingsFingerprint.Compute(new TaomSettings());
        // Pinned, not a floor: the docs quote this number, and a change here means someone added
        // or reclassified a setting and the docs need the same edit.
        Assert.AreEqual(106, report.Covered,
            $"simulation-relevant settings changed — update docs/features/coop-interop.md too");
        Assert.AreEqual(64, report.Global.Length, "SHA-256 hex is 64 chars");
        Assert.IsTrue(report.ByGroup.Count > 5, "expected the fingerprint to span several groups");

        // MCM's base class exposes settable plumbing (SubFolder, SubGroupDelimiter, …). Hashing
        // any of it would make the code depend on MCM's version rather than on TAOM's settings.
        var inherited = typeof(TaomSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(CoopSettingsRelevance.IsSimulationRelevant)
            .Where(p => p.DeclaringType != typeof(TaomSettings))
            .Select(p => $"{p.DeclaringType?.Name}.{p.Name}")
            .ToList();
        Assert.AreEqual(0, inherited.Count,
            "inherited MCM members entered the fingerprint: " + string.Join(", ", inherited));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ComputeRejectsNull()
    {
        SettingsFingerprint.Compute(null);
    }

    // ------------------------------------------------------------------ 6. the log adapter

    private sealed class RecordingLogger : TAOM.Core.Logging.IModLogger
    {
        public readonly System.Collections.Generic.List<string> Lines = new System.Collections.Generic.List<string>();
        public void LogInfo(string m) => Lines.Add("INFO " + m);
        public void LogDebug(string m) => Lines.Add("DEBUG " + m);
        public void LogWarning(string m) => Lines.Add("WARN " + m);
        public void LogError(string m) => Lines.Add("ERROR " + m);
        public string? LogFilePath => null;   // in-memory double writes nowhere
        public void Dispose() { }
    }

    [TestMethod]
    public void TheLogAdapter_WritesTheGlobalCodeAndOneLinePerGroup()
    {
        var log = new RecordingLogger();
        SettingsFingerprintLog.Write(new FakeSettings(), log);

        Assert.IsTrue(log.Lines.Any(l => l.Contains("global=")), "expected a global line");
        // Three groups on the fake: Battle Tactics, World, (ungrouped). Map UI holds only an
        // excluded setting, so it must not appear at all.
        Assert.IsTrue(log.Lines.Any(l => l.Contains("Battle Tactics =")));
        Assert.IsTrue(log.Lines.Any(l => l.Contains("World =")));
        Assert.IsFalse(log.Lines.Any(l => l.Contains("Map UI =")),
            "a group whose settings are all excluded must not be reported");
    }

    [TestMethod]
    public void TheLogAdapter_SwallowsAFailingSettingsObject()
    {
        // A diagnostic that can take the session down is worse than no diagnostic.
        var log = new RecordingLogger();
        SettingsFingerprintLog.Write(new ExplodingSettings(), log);
        Assert.IsTrue(log.Lines.Any(l => l.StartsWith("INFO") || l.StartsWith("WARN")),
            "expected it to report something rather than throw");
    }

    private sealed class ExplodingSettings
    {
        public bool Fine { get; set; } = true;
        public int Boom { get { throw new InvalidOperationException("boom"); } set { } }
    }

    [TestMethod]
    public void TheLogAdapter_IgnoresNulls()
    {
        SettingsFingerprintLog.Write(null, new RecordingLogger());   // must not throw
        SettingsFingerprintLog.Write(new FakeSettings(), null);      // must not throw
    }
}
