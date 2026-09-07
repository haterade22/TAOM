using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CoopInterop;
using TAOM.Features.CrashReport;

namespace TAOM.Tests.Features.CoopInterop;

// The co-op docs record that two peers with different MCM settings simulate differently with no
// warning, and that a fingerprint handshake was "designed but not built". These pin the built one.
//
// What a fingerprint has to get right, and what each test holds it to:
//   1. Same settings -> same code, every time. Reflection does not guarantee member order, so the
//      canonical text is sorted; without that the hash would drift between runs.
//   2. Different settings -> different code, and the RIGHT GROUP named. A global-only answer sends
//      a player through 175 checkboxes.
//   3. A locale must not fake a mismatch. 0.25f renders "0,25" under es-ES: two peers with identical
//      settings would diverge on the decimal separator alone. This is the failure that would have
//      made the feature worse than nothing.
//   4. An excluded setting must not move the hash — otherwise the warning fires on a nameplate
//      distance and gets ignored, which is the doc linter's 29-false-positives failure again.
//   5. Every property on every REAL settings class is accounted for. Classification is
//      include-by-default, so "is it relevant or excluded?" is true of everything and answers
//      nothing — the guard that can actually fail is the pinned per-class count: adding a setting
//      anywhere moves a number here and forces whoever added it to decide, and the docs quote the
//      same numbers.
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

    /// <summary>
    /// A second settings class contributing to the SAME groups as <see cref="FakeSettings"/>.
    /// Order-independence is only testable when two objects both put properties in one group —
    /// pairing TaomSettings with a diagnostics class proves nothing, because the diagnostics class
    /// contributes none.
    /// </summary>
    private sealed class SecondFakeSettings
    {
        [SettingPropertyGroup("Battle Tactics")]
        public bool EnableSkirmishers { get; set; } = true;

        [SettingPropertyGroup("World")]
        public int CaravanCount { get; set; } = 7;
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
    public void EverySettingsClass_HasItsSplitPinned()
    {
        // "Relevant OR excluded" is true of every property by construction — classification is
        // include-by-default, so that assertion passes for a setting nobody has ever looked at and
        // guards nothing. The count is the guard that can fail: add a setting anywhere below and
        // one of these numbers moves, which is the moment to decide what it is. The same numbers
        // are quoted in docs/features/coop-interop.md.
        AssertSplit(typeof(TaomSettings), reflected: 220, covered: 175);
        AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 7, covered: 0);
        AssertSplit(typeof(BlowDiagnosticsSettings), reflected: 1, covered: 0);
        AssertSplit(typeof(CrashReportSettings), reflected: 6, covered: 0);
    }

    [TestMethod]
    public void EveryDocQuotingTheSettingsCounts_AgreesWithReflection()
    {
        // Two docs quote these numbers and only one used to be pinned, so
        // bannerlord-together-compat.md sat on "167 settings, 112 simulation-relevant" while
        // coop-interop.md said 173/116 — and the two contradicted each other in a sentence that
        // links one to the other. Nothing caught it: lint_docs.py cannot see semantic drift.
        // Deep-review 2026-08-13 finding 6.
        var totals = new[]
        {
            typeof(TaomSettings), typeof(BattleLoadDiagnosticsSettings),
            typeof(BlowDiagnosticsSettings), typeof(CrashReportSettings),
        };
        var reflected = totals.Sum(t => SettableProperties(t).Count());
        var relevant = totals.Sum(t => SettableProperties(t).Count(CoopSettingsRelevance.IsSimulationRelevant));

        foreach (var relative in new[]
        {
            @"docs\features\coop-interop.md",
            @"docs\features\bannerlord-together-compat.md",
        })
        {
            var path = Path.Combine(RepoRoot, relative);
            Assert.IsTrue(File.Exists(path), $"{relative} not found at {path}");
            var text = File.ReadAllText(path);

            Assert.IsTrue(
                text.Contains($"**{reflected}**") || text.Contains($" {reflected} MCM settings"),
                $"{relative} does not quote the current total of {reflected} MCM settings. "
                + "Update it, or drop the number from that doc so only one file owns it.");
            Assert.IsTrue(
                text.Contains($"**{relevant} are simulation-relevant**")
                || text.Contains($"**{relevant} of them are\n  simulation-relevant**"),
                $"{relative} does not quote the current simulation-relevant count of {relevant}.");
            Assert.IsFalse(
                text.Contains("112 are simulation-relevant") && relevant != 112,
                $"{relative} still quotes a stale simulation-relevant count.");
        }
    }

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static void AssertSplit(Type settingsType, int reflected, int covered)
    {
        var props = SettableProperties(settingsType).ToList();
        Assert.AreEqual(reflected, props.Count,
            $"{settingsType.Name} exposes {props.Count} settings, not {reflected} — a setting was "
            + "added or removed; classify it in CoopSettingsRelevance and update this number and "
            + "docs/features/coop-interop.md");
        Assert.AreEqual(covered, props.Count(CoopSettingsRelevance.IsSimulationRelevant),
            $"{settingsType.Name}'s simulation-relevant count changed");
        Assert.AreEqual(reflected - covered, props.Count(p => CoopSettingsRelevance.IsExcluded(p.Name)),
            $"{settingsType.Name}: covered + excluded must account for every setting");
    }

    private static System.Collections.Generic.IEnumerable<PropertyInfo> SettableProperties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

    [TestMethod]
    public void NoExcludedName_IsDead()
    {
        // An exclusion list is silent when it is wrong: a typo'd or removed name excludes nothing
        // and reads as though someone considered that setting. Every name must still name a real
        // property on one of the four classes.
        var classes = new[]
        {
            typeof(TaomSettings), typeof(BattleLoadDiagnosticsSettings),
            typeof(BlowDiagnosticsSettings), typeof(CrashReportSettings),
        };

        var dead = CoopSettingsRelevance.ExcludedNames()
            .Where(n => classes.All(t =>
                t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance) == null))
            .ToList();

        Assert.AreEqual(0, dead.Count,
            "excluded names that match no property on any settings class: " + string.Join(", ", dead));
    }

    [TestMethod]
    public void NoTwoSettingsClasses_ShareAPropertyName()
    {
        // ComputeAcross keys the canonical text on the property name alone, which is what keeps a
        // single-object fingerprint byte-identical to before that overload existed. Two classes
        // sharing a name inside one group would put two settings on indistinguishable lines. This
        // fails first so the decision is made deliberately rather than discovered as a collision.
        var byName = new[]
            {
                typeof(TaomSettings), typeof(BattleLoadDiagnosticsSettings),
                typeof(BlowDiagnosticsSettings), typeof(CrashReportSettings),
            }
            .SelectMany(t => SettableProperties(t).Select(p => new { t, p.Name }))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key + " on " + string.Join("/", g.Select(x => x.t.Name)))
            .ToList();

        Assert.AreEqual(0, byName.Count, "settings names shared across classes: " + string.Join(", ", byName));
    }

    [TestMethod]
    public void AddingTheDiagnosticsClasses_ChangesNoHash()
    {
        // The whole justification for widening the call site: every diagnostics property is
        // excluded, so covering those classes is a pure safety-net addition. If this ever fails,
        // one of them became simulation-relevant and every peer's fingerprint just changed.
        var settingsOnly = SettingsFingerprint.Compute(new TaomSettings());
        var allFour = SettingsFingerprint.ComputeAcross(
            new TaomSettings(), new BattleLoadDiagnosticsSettings(),
            new BlowDiagnosticsSettings(), new CrashReportSettings());

        Assert.AreEqual(settingsOnly.Global, allFour.Global, "the three diagnostics classes moved the hash");
        Assert.AreEqual(settingsOnly.Covered, allFour.Covered, "the three diagnostics classes added coverage");
    }

    [TestMethod]
    public void ComputeAcross_IgnoresArgumentOrder()
    {
        // Two objects both contributing to "Battle Tactics" and "World" — without the sort, the
        // canonical text would follow the argument order and the two peers' codes would differ
        // over nothing. Real settings classes cannot show this: only TaomSettings contributes.
        var forwards = SettingsFingerprint.ComputeAcross(new FakeSettings(), new SecondFakeSettings());
        var backwards = SettingsFingerprint.ComputeAcross(new SecondFakeSettings(), new FakeSettings());

        Assert.AreEqual(forwards.Global, backwards.Global, "argument order changed the fingerprint");
        Assert.AreEqual(forwards.Covered, backwards.Covered);
        // FakeSettings: EnableFlanking, ChargeDistance, BanditDensity, Ungrouped (MapFigureScale
        // and SmartCavalryDebug are excluded, DisplayName is read-only). Plus the second class's 2.
        Assert.AreEqual(6, forwards.Covered, "expected 4 relevant on FakeSettings + 2 on the second");
    }

    [TestMethod]
    public void ComputeAcross_SkipsNulls()
    {
        // MCM may not have constructed a diagnostics page when the co-op gate fires. A null must
        // neither throw on a diagnostic path nor cost the coverage of the objects that are there.
        var report = SettingsFingerprint.ComputeAcross(new TaomSettings(), null, new CrashReportSettings());

        Assert.AreEqual(175, report.Covered, "a null entry cost coverage");
        Assert.AreEqual(SettingsFingerprint.Compute(new TaomSettings()).Global, report.Global);
    }

    [TestMethod]
    public void EveryPageUnavailable_IsNotReportedAsAgreement()
    {
        // Reachable, not hypothetical. MCM 5.12.1 declares
        //     public static T? Instance =>
        //         BaseSettingsProvider.Instance?.GetSettings(...) as T;
        // — a nullable return, a null-conditional on the provider and an `as` cast, so a page read
        // before MCM's provider is up yields null rather than throwing. Skipping those nulls leaves
        // nothing to hash, and SHA-256 of the empty string is a constant: two such peers produce
        // the SAME code and read it as "we agree".
        var a = SettingsFingerprint.ComputeAcross(null, null, null, null);
        var b = SettingsFingerprint.ComputeAcross(null, null, null, null);

        Assert.AreEqual(0, a.Covered, "sanity: nothing was measured");
        Assert.IsFalse(a.Matches(b),
            "a fingerprint over zero settings must not answer 'equal' — it measured nothing, " +
            "and 'cannot tell' is not 'agree'");
    }

    [TestMethod]
    public void APartialRead_StaysConclusive_ButCountsWhatItMissed()
    {
        // One unreadable page must not disarm the whole report — the settings that WERE read can
        // still catch a divergence. It must be counted, though, or "partial" reads as "complete".
        var partial = SettingsFingerprint.ComputeAcross(new TaomSettings(), null, null);
        var whole = SettingsFingerprint.ComputeAcross(new TaomSettings());

        Assert.IsTrue(partial.IsConclusive, "settings were read; the report stands");
        Assert.AreEqual(2, partial.Unavailable, "both nulls must be counted");
        Assert.AreEqual(0, whole.Unavailable);
        Assert.IsTrue(partial.Matches(whole), "the pages that were read still hash the same");
    }

    [TestMethod]
    public void TheLogAdapter_SaysAReadWasPartial()
    {
        var log = new RecordingLogger();
        SettingsFingerprintLog.WriteAcross(log, new TaomSettings(), null, null, null);

        Assert.IsTrue(log.Lines.Any(l => l.Contains("partial")),
            "a report missing 3 of 4 pages must say so; got: " + string.Join(" | ", log.Lines));
    }

    [TestMethod]
    public void TheLogAdapter_SaysSoWhenItMeasuredNothing()
    {
        // The line must not promise that comparing it is meaningful when there was nothing to
        // compare. This is the same rule as the linter's no-models guard: a check that reports
        // clean without having looked is worse than no check.
        var log = new RecordingLogger();
        SettingsFingerprintLog.WriteAcross(log, null, null, null, null);

        Assert.IsTrue(
            log.Lines.Any(l => l.Contains("could not read") || l.StartsWith("WARN")),
            "expected the log to say it read no settings; got: " + string.Join(" | ", log.Lines));
    }

    [TestMethod]
    public void TheRealSettings_HashWithoutThrowing_AndCoverMostOfThem()
    {
        var report = SettingsFingerprint.Compute(new TaomSettings());
        // Pinned, not a floor: the docs quote this number, and a change here means someone added
        // or reclassified a setting and the docs need the same edit.
        Assert.AreEqual(175, report.Covered,
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

    /// <summary>
    /// A property whose GETTER succeeds but whose RENDER throws must cost one line, not the hash.
    ///
    /// This is the live shape, not a hypothetical: MCM's <c>Dropdown&lt;T&gt;.SelectedValue</c> is
    /// an unchecked <c>List&lt;T&gt;</c> indexer over a persisted <c>SelectedIndex</c>, and
    /// <c>TaomSettings.CaravanWarTradePolicy</c> is a live <c>Dropdown&lt;string&gt;</c> on no
    /// exclusion list. An index left out of range by a removed option throws from inside Render.
    /// Guarding only the getter let that one property destroy the whole fingerprint.
    /// </summary>
    [TestMethod]
    public void ARenderThatThrows_CostsOneLineNotTheWholeFingerprint()
    {
        var report = SettingsFingerprint.Compute(new RenderExplodingSettings());

        Assert.IsFalse(string.IsNullOrEmpty(report.Global), "the fingerprint must still be produced");
        Assert.AreEqual(2, report.Covered, "both properties still count as covered");
    }

    /// <summary>Named to hit Render's Dropdown branch, and throws exactly where MCM's would.</summary>
    private sealed class ExplodingDropdown
    {
        public string SelectedValue { get { throw new ArgumentOutOfRangeException("SelectedIndex"); } }
    }

    private sealed class RenderExplodingSettings
    {
        public bool Fine { get; set; } = true;
        public ExplodingDropdown Policy { get; set; } = new ExplodingDropdown();
    }
}
