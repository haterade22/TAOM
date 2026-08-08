using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FieldCommission;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

/// <summary>
/// Pins the MCM-over-JSON merge for Battlefield Promotions.
///
/// The provider decorates the JSON <see cref="FieldCommissionConfigProvider"/> and satisfies the SAME
/// <see cref="IFieldCommissionConfigProvider"/> interface, so every existing consumer picks up live MCM
/// values with no constructor change. <see cref="FieldCommissionSettingsProvider.Merge"/> is a pure
/// seam — none of these tests need MCM loaded, which is what makes the fail direction testable at all
/// (<c>TaomSettings.Instance</c> is null in the MSTest host).
///
/// Two invariants matter most and are each pinned below:
///   1. A player with MCM absent and a player with MCM at its shipped default must get the SAME
///      behaviour — so every compiled MCM default has to equal its JSON counterpart.
///   2. <c>GetConfig()</c> runs on the campaign tick, so an unchanged snapshot must return the cached
///      instance rather than allocating a fresh config (and a fresh race-name list) every frame.
/// </summary>
[TestClass]
public class FieldCommissionSettingsProviderTests
{
    private static FieldCommissionConfig JsonDefaults() => new FieldCommissionConfig
    {
        Enabled = true,
        RatioThreshold = 1.3f,
        MeritPerKill = 1,
        MeritThreshold = 8,
        RetainerAllowance = 0,
        MaxOffersPerBattle = 1,
        Diagnostics = false,
        SkillPointsPerLevel = 5,
        AllowedRaceNames = new List<string> { "human", "dwarf", "elf" },
    };

    private static FieldCommissionMcmSnapshot Empty() => default;

    [TestMethod]
    public void Merge_NoMcmValues_ReturnsJsonValuesUnchanged()
    {
        var json = JsonDefaults();

        var merged = FieldCommissionSettingsProvider.Merge(json, Empty());

        Assert.IsTrue(merged.Enabled);
        Assert.AreEqual(1.3f, merged.RatioThreshold, 0.0001f);
        Assert.AreEqual(1, merged.MeritPerKill);
        Assert.AreEqual(8, merged.MeritThreshold);
        Assert.AreEqual(0, merged.RetainerAllowance);
        Assert.AreEqual(1, merged.MaxOffersPerBattle);
    }

    [TestMethod]
    public void Merge_McmEnabledFalse_OverridesJsonEnabledTrue()
    {
        // The whole point of the master switch: JSON says on, the player says off, the player wins.
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(enabled: false, null, null, null, null, null));

        Assert.IsFalse(merged.Enabled);
    }

    [TestMethod]
    public void Merge_McmEnabledTrue_OverridesJsonEnabledFalse()
    {
        // Kills a "the switch can only turn things off" implementation — it must pass the choice
        // through in both directions, otherwise a JSON-disabled feature could never be re-enabled.
        var json = JsonDefaults();
        json.Enabled = false;

        var merged = FieldCommissionSettingsProvider.Merge(
            json, new FieldCommissionMcmSnapshot(enabled: true, null, null, null, null, null));

        Assert.IsTrue(merged.Enabled);
    }

    [TestMethod]
    public void Merge_McmRatioThreshold_OverridesJson()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, ratioThreshold: 2.5f, null, null, null, null));

        Assert.AreEqual(2.5f, merged.RatioThreshold, 0.0001f);
    }

    [TestMethod]
    public void Merge_McmRatioThresholdNaN_FallsBackToJsonValue()
    {
        // NaN comparisons are all false, so an unguarded clamp would pass NaN straight through into
        // IsBattleEligible and silently make every battle ineligible. Non-finite must revert.
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, ratioThreshold: float.NaN, null, null, null, null));

        Assert.AreEqual(1.3f, merged.RatioThreshold, 0.0001f);
    }

    [TestMethod]
    public void Merge_McmRatioThresholdInfinity_FallsBackToJsonValue()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(),
            new FieldCommissionMcmSnapshot(null, ratioThreshold: float.PositiveInfinity, null, null, null, null));

        Assert.AreEqual(1.3f, merged.RatioThreshold, 0.0001f);
    }

    [TestMethod]
    public void Merge_McmRatioThresholdAboveMax_ClampsToMax()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, ratioThreshold: 99f, null, null, null, null));

        Assert.AreEqual(FieldCommissionSettingsProvider.MaxRatioThreshold, merged.RatioThreshold, 0.0001f);
    }

    [TestMethod]
    public void Merge_McmRatioThresholdBelowMin_ClampsToMin()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, ratioThreshold: -5f, null, null, null, null));

        Assert.AreEqual(FieldCommissionSettingsProvider.MinRatioThreshold, merged.RatioThreshold, 0.0001f);
    }

    [TestMethod]
    public void Merge_McmMeritPerKillBelowMin_ClampsToMin()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, null, meritPerKill: 0, null, null, null));

        Assert.AreEqual(FieldCommissionSettingsProvider.MinMeritPerKill, merged.MeritPerKill);
    }

    [TestMethod]
    public void Merge_McmMeritThresholdBelowMin_ClampsToMin()
    {
        // A zero threshold would make `available / threshold` a divide-by-zero in the offer loop.
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, null, null, meritThreshold: 0, null, null));

        Assert.AreEqual(FieldCommissionSettingsProvider.MinMeritThreshold, merged.MeritThreshold);
    }

    [TestMethod]
    public void Merge_McmRetainerAllowanceNegative_ClampsToZero()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, null, null, null, retainerAllowance: -3, null));

        Assert.AreEqual(0, merged.RetainerAllowance);
    }

    [TestMethod]
    public void Merge_McmMaxOffersPerBattleBelowMin_ClampsToMin()
    {
        // 0 would silently disable promotions while the master switch still reads "on" — the one
        // value that must never reach the offer loop.
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, null, null, null, null, maxOffersPerBattle: 0));

        Assert.AreEqual(FieldCommissionSettingsProvider.MinMaxOffersPerBattle, merged.MaxOffersPerBattle);
    }

    [TestMethod]
    public void Merge_McmMaxOffersPerBattle_OverridesJson()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, null, null, null, null, maxOffersPerBattle: 4));

        Assert.AreEqual(4, merged.MaxOffersPerBattle);
    }

    [TestMethod]
    public void Merge_NoMcmValue_DoesNotClampTheJsonValueThroughSliderBounds()
    {
        // The two surfaces validate on different terms. `ratioThreshold: 0` is explicitly legal in
        // the JSON provider ("never eligible"), but the MCM slider floors at 0.1 — so running the
        // JSON value through the slider's clamp would hand back 0.1 and quietly switch the feature
        // back on for lopsided wins. The clamp bounds the PLAYER's input, not the pack author's.
        var json = JsonDefaults();
        json.RatioThreshold = 0f;
        json.MeritThreshold = 500;   // legal in JSON (>= 1), far above the slider's max of 100

        var merged = FieldCommissionSettingsProvider.Merge(json, Empty());

        Assert.AreEqual(0f, merged.RatioThreshold, 0.0001f);
        Assert.AreEqual(500, merged.MeritThreshold);
    }

    [TestMethod]
    public void Merge_McmValuePresent_IsStillClampedToSliderBounds()
    {
        // The mirror: a hand-edited MCM json can carry anything, so the player's value IS bounded.
        var json = JsonDefaults();
        json.RatioThreshold = 0f;

        var merged = FieldCommissionSettingsProvider.Merge(
            json, new FieldCommissionMcmSnapshot(null, ratioThreshold: 99f, null, null, null, null));

        Assert.AreEqual(FieldCommissionSettingsProvider.MaxRatioThreshold, merged.RatioThreshold, 0.0001f);
    }

    [TestMethod]
    public void Merge_McmDiagnostics_OverridesJson()
    {
        var merged = FieldCommissionSettingsProvider.Merge(
            JsonDefaults(), new FieldCommissionMcmSnapshot(null, null, null, null, null, null, diagnostics: true));

        Assert.IsTrue(merged.Diagnostics);
    }

    [TestMethod]
    public void Merge_NoMcmDiagnostics_KeepsJsonValue()
    {
        var json = JsonDefaults();
        json.Diagnostics = true;

        var merged = FieldCommissionSettingsProvider.Merge(json, Empty());

        Assert.IsTrue(merged.Diagnostics);
    }

    [TestMethod]
    public void Merge_JsonOnlyFields_SurviveTheMerge()
    {
        // skillPointsPerLevel and allowedRaceNames have no MCM knob (advanced/JSON-only). A merge that
        // rebuilt the config from MCM alone would silently reset them to compiled defaults — which for
        // allowedRaceNames would re-admit races the pack author deliberately excluded.
        var json = JsonDefaults();
        json.SkillPointsPerLevel = 9;
        json.AllowedRaceNames = new List<string> { "dwarf" };

        var merged = FieldCommissionSettingsProvider.Merge(json, Empty());

        Assert.AreEqual(9, merged.SkillPointsPerLevel);
        CollectionAssert.AreEqual(new List<string> { "dwarf" }, merged.AllowedRaceNames);
    }

    [TestMethod]
    public void Merge_NullJsonConfig_ReturnsCompiledDefaults()
    {
        // Defensive: a decorated provider that somehow yields null must not NRE on the campaign tick.
        var merged = FieldCommissionSettingsProvider.Merge(null, Empty());

        Assert.IsNotNull(merged);
        Assert.IsTrue(merged.Enabled);
        Assert.AreEqual(new FieldCommissionConfig().MeritThreshold, merged.MeritThreshold);
    }

    [TestMethod]
    public void GetConfig_NoMcmInstance_ReturnsDecoratedJsonValues()
    {
        var json = JsonDefaults();
        json.MeritThreshold = 17;
        var inner = Substitute.For<IFieldCommissionConfigProvider>();
        inner.GetConfig().Returns(json);

        var sut = new FieldCommissionSettingsProvider(inner);

        Assert.AreEqual(17, sut.GetConfig().MeritThreshold);
    }

    [TestMethod]
    public void GetConfig_SnapshotUnchanged_ReturnsCachedInstance()
    {
        // GetConfig() is called from CampaignEvents.TickEvent. Allocating a config + a race-name list
        // per frame is the kind of cost that never shows up in a unit test but does in a profiler.
        var inner = Substitute.For<IFieldCommissionConfigProvider>();
        inner.GetConfig().Returns(JsonDefaults());
        var sut = new FieldCommissionSettingsProvider(inner);

        var first = sut.GetConfig();
        var second = sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetConfig_SnapshotChanged_RebuildsMergedConfig()
    {
        // The mirror of the caching test: a mid-session MCM change must take effect without a restart,
        // which is what lets the MCM properties honestly declare RequireRestart = false.
        var inner = Substitute.For<IFieldCommissionConfigProvider>();
        inner.GetConfig().Returns(JsonDefaults());
        var sut = new FieldCommissionSettingsProvider(inner);

        var before = sut.GetConfig(Empty());
        var after = sut.GetConfig(new FieldCommissionMcmSnapshot(enabled: false, null, null, null, null, null));

        Assert.IsTrue(before.Enabled);
        Assert.IsFalse(after.Enabled);
        Assert.AreNotSame(before, after);
    }

    [TestMethod]
    public void Snapshot_SameValues_AreEqual()
    {
        // The cache-invalidation key. If equality were reference-based the config would rebuild every
        // tick; if it ignored a field, that field's MCM knob would appear dead until a restart.
        var a = new FieldCommissionMcmSnapshot(true, 1.3f, 1, 8, 0, 1);
        var b = new FieldCommissionMcmSnapshot(true, 1.3f, 1, 8, 0, 1);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Snapshot_AnyFieldDiffers_AreNotEqual()
    {
        var baseline = new FieldCommissionMcmSnapshot(true, 1.3f, 1, 8, 0, 1);

        Assert.AreNotEqual(baseline, new FieldCommissionMcmSnapshot(false, 1.3f, 1, 8, 0, 1));
        Assert.AreNotEqual(baseline, new FieldCommissionMcmSnapshot(true, 1.4f, 1, 8, 0, 1));
        Assert.AreNotEqual(baseline, new FieldCommissionMcmSnapshot(true, 1.3f, 2, 8, 0, 1));
        Assert.AreNotEqual(baseline, new FieldCommissionMcmSnapshot(true, 1.3f, 1, 9, 0, 1));
        Assert.AreNotEqual(baseline, new FieldCommissionMcmSnapshot(true, 1.3f, 1, 8, 1, 1));
        Assert.AreNotEqual(baseline, new FieldCommissionMcmSnapshot(true, 1.3f, 1, 8, 0, 2));
        Assert.AreNotEqual(baseline, new FieldCommissionMcmSnapshot(true, 1.3f, 1, 8, 0, 1, diagnostics: true));
        Assert.AreNotEqual(baseline, default(FieldCommissionMcmSnapshot));
    }

    [TestMethod]
    public void CompiledMcmDefaults_MatchShippedJsonDefaults()
    {
        // Invariant 1 from the class doc. A player without MCM reads the JSON; a player with MCM at
        // its shipped default reads the compiled literal. If those two drift, the same install
        // behaves differently depending on whether MCM loaded — invisible in a test host that never
        // has MCM. Source-scanned rather than reflected because instantiating TaomSettings would drag
        // in the MCM assembly this host does not have.
        var settings = File.ReadAllText(RepoPath("Main", "Features", "TaomSettings.cs"));
        var json = File.ReadAllText(RepoPath(
            "Main", "_Module", "ModuleData", "field_commission", "field_commission_config.json"));

        Assert.IsTrue(
            settings.Contains("EnableFieldCommission"),
            "Sanity floor: TaomSettings.cs must contain the Battlefield Promotions properties — a " +
            "rename or path slip must fail loudly here, not let every Contains() pass vacuously.");

        AssertPairedDefault(settings, json, "EnableFieldCommission { get; set; } = true;", "\"enabled\": true");
        AssertPairedDefault(settings, json, "FieldCommissionRatioThreshold { get; set; } = 1.3f;", "\"ratioThreshold\": 1.3");
        AssertPairedDefault(settings, json, "FieldCommissionMeritPerKill { get; set; } = 1;", "\"meritPerKill\": 1");
        AssertPairedDefault(settings, json, "FieldCommissionMeritThreshold { get; set; } = 8;", "\"meritThreshold\": 8");
        AssertPairedDefault(settings, json, "FieldCommissionRetainerAllowance { get; set; } = 0;", "\"retainerAllowance\": 0");
        AssertPairedDefault(settings, json, "FieldCommissionMaxOffersPerBattle { get; set; } = 1;", "\"maxOffersPerBattle\": 1");
        AssertPairedDefault(settings, json, "EnableFieldCommissionDiagnostics { get; set; } = false;", "\"diagnostics\": false");
    }

    private static void AssertPairedDefault(string settings, string json, string mcmLiteral, string jsonLiteral)
    {
        Assert.IsTrue(
            settings.Contains(mcmLiteral),
            $"TaomSettings.cs no longer declares `{mcmLiteral}`. The MCM default and the JSON default " +
            $"`{jsonLiteral}` encode one decision — change both or neither.");
        Assert.IsTrue(
            json.Contains(jsonLiteral),
            $"field_commission_config.json no longer contains `{jsonLiteral}`, which must match the " +
            $"MCM default `{mcmLiteral}`.");
    }

    private static string RepoPath(params string[] parts)
    {
        // THREE levels: <repo>/TAOM.Tests/Features/FieldCommission -> Features -> TAOM.Tests -> <repo>.
        var repoRoot = Path.GetFullPath(Path.Combine(ThisFile(), "..", "..", ".."));
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
