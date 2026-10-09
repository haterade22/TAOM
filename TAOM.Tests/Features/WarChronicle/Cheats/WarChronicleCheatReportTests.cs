using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarChronicle.Cheats;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle.Cheats;

[TestClass]
public class WarChronicleCheatReportTests
{
    private static readonly string[] Known = { "empire_w", "empire_s" };

    private static bool TryBuild(string[] args, out WarEffect effect, out string error, double now = 100d)
    {
        var ok = WarChronicleCheatReport.TryBuildEffect(args, now, Known, out var built, out error);
        effect = built!;
        return ok;
    }

    [TestMethod]
    public void TryBuildEffect_ValidArguments_BuildsAConsoleEffectEndingInDays()
    {
        var ok = TryBuild(new[] { "empire_w", "VolunteerRate", "0.15", "10" }, out var effect, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual("console", effect.SourceId);
        Assert.AreEqual("empire_w", effect.KingdomId);
        Assert.AreEqual(WarEffectKind.VolunteerRate, effect.Kind);
        Assert.AreEqual(0.15f, effect.Magnitude);
        Assert.AreEqual(100d + 240d, effect.EndTimeHours);
    }

    [TestMethod]
    public void TryBuildEffect_KindIsCaseInsensitive_AndNegativeMagnitudeIsAllowed()
    {
        var ok = TryBuild(new[] { "empire_s", "prisonerescape", "-0.5", "2.5" }, out var effect, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(WarEffectKind.PrisonerEscape, effect.Kind);
        Assert.AreEqual(-0.5f, effect.Magnitude);
        Assert.AreEqual(100d + 60d, effect.EndTimeHours);
    }

    [TestMethod]
    public void TryBuildEffect_ADecimalCommaOnAGermanMachine_IsNotReadAsAThousandsSeparator()
    {
        var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var ok = TryBuild(new[] { "empire_w", "VolunteerRate", "0.15", "10" }, out var effect, out var error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(0.15f, effect.Magnitude);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(5)]
    public void TryBuildEffect_WrongArgumentCount_ReportsUsage(int count)
    {
        var args = new string[count];
        for (var i = 0; i < count; i++)
            args[i] = "x";

        Assert.IsFalse(TryBuild(args, out _, out var error));
        StringAssert.Contains(error, "taom.war_effect_add");
    }

    [TestMethod]
    public void TryBuildEffect_UnknownKingdom_IsRefused()
    {
        Assert.IsFalse(TryBuild(new[] { "gondor", "VolunteerRate", "0.1", "5" }, out _, out var error));
        StringAssert.Contains(error, "gondor");
    }

    [TestMethod]
    public void TryBuildEffect_NoKingdomsAtAll_IsRefused()
    {
        var ok = WarChronicleCheatReport.TryBuildEffect(
            new[] { "empire_w", "VolunteerRate", "0.1", "5" }, 0d, new string[0], out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "empire_w");
    }

    [DataTestMethod]
    [DataRow("Volunteer")]
    [DataRow("1")]
    [DataRow("")]
    [DataRow("GarrisonRecruit")]
    public void TryBuildEffect_UnknownKind_IsRefusedNotDefaulted(string kind)
    {
        Assert.IsFalse(TryBuild(new[] { "empire_w", kind, "0.1", "5" }, out _, out var error));
        StringAssert.Contains(error, "VolunteerRate");
        StringAssert.Contains(error, "PrisonerEscape");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("abc")]
    [DataRow("")]
    [DataRow("1.5")]
    [DataRow("-1.01")]
    public void TryBuildEffect_UnusableMagnitude_IsRefused(string magnitude)
    {
        Assert.IsFalse(TryBuild(new[] { "empire_w", "VolunteerRate", magnitude, "5" }, out _, out var error));
        StringAssert.Contains(error, "agnitude");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("0")]
    [DataRow("-3")]
    [DataRow("366")]
    [DataRow("abc")]
    public void TryBuildEffect_UnusableDays_IsRefused(string days)
    {
        Assert.IsFalse(TryBuild(new[] { "empire_w", "VolunteerRate", "0.1", days }, out _, out var error));
        StringAssert.Contains(error, "ays");
    }

    [TestMethod]
    public void TryBuildEffect_ANonFiniteClock_IsRefused()
    {
        Assert.IsFalse(TryBuild(new[] { "empire_w", "VolunteerRate", "0.1", "5" }, out _, out var error, double.NaN));
        StringAssert.Contains(error, "clock");
    }

    [TestMethod]
    public void FormatEffects_None_SaysSo()
    {
        var text = WarChronicleCheatReport.FormatEffects(new List<WarEffect>(), 100d, (_, _) => 1f);

        StringAssert.Contains(text, "No active war effects");
    }

    [TestMethod]
    public void FormatEffects_ListsEachEffectAndTheMultiplierPerAffectedKingdom()
    {
        var effects = new List<WarEffect>
        {
            new WarEffect("rally:empire_w", "empire_w", WarEffectKind.VolunteerRate, 0.1f, 340d),
            new WarEffect("console", "empire_s", WarEffectKind.PrisonerEscape, 0.5f, 220d),
            new WarEffect("event", "empire_w", WarEffectKind.PrisonerEscape, 0.2f, 100d),
        };

        var text = WarChronicleCheatReport.FormatEffects(effects, 100d, (k, kind) =>
            k == "empire_w" && kind == WarEffectKind.VolunteerRate ? 1.1f : 1f);

        StringAssert.Contains(text, "3 active");
        StringAssert.Contains(text, "rally:empire_w");
        StringAssert.Contains(text, "empire_w VolunteerRate +0.100");
        StringAssert.Contains(text, "ends in 10.0 days");
        StringAssert.Contains(text, "ended");
        StringAssert.Contains(text, "empire_w: VolunteerRate x1.10, PrisonerEscape x1.00");
        StringAssert.Contains(text, "empire_s: VolunteerRate x1.00, PrisonerEscape x1.00");
    }

    [TestMethod]
    public void FormatAdded_IsTheGoldenLine_UnderAGermanThreadCulture()
    {
        var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var text = WarChronicleCheatReport.FormatAdded(
                new WarEffect("console", "empire_w", WarEffectKind.VolunteerRate, 0.25f, 340d), 1f, 1.25f);

            Assert.AreEqual(
                "[WarEffects] empire_w VolunteerRate +0.250 from \"console\" until campaign hour 340: multiplier x1.00 -> x1.25",
                text);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    // ---- FormatRally (the rally_status command) ----

    private static RallyStatusRow Row(string id, int points, int? baseline, float? loss, int tier, bool eligible) =>
        new RallyStatusRow { KingdomId = id, Points = points, Baseline = baseline, Loss = loss, Tier = tier, Eligible = eligible };

    [TestMethod]
    public void FormatRally_ListsEachKingdomWithPointsBaselineLossTierAndEligibility()
    {
        var text = WarChronicleCheatReport.FormatRally(
            new List<RallyStatusRow> { Row("empire_w", 6, 8, 0.25f, 1, true) }, rallyOn: true);

        StringAssert.Contains(text, "[Rally] On");
        StringAssert.Contains(text, "[Rally]   empire_w: 6 / 8, loss 0.250, tier 1, eligible yes");
    }

    [TestMethod]
    public void FormatRally_NoBaseline_PrintsNaForBaselineAndLoss()
    {
        var text = WarChronicleCheatReport.FormatRally(
            new List<RallyStatusRow> { Row("new_k", 4, null, null, 0, false) }, rallyOn: true);

        StringAssert.Contains(text, "[Rally]   new_k: 4 / na, loss na, tier 0, eligible no");
    }

    [TestMethod]
    public void FormatRally_RallyOff_SaysTiersAreStillTrackedAndNothingIsWritten()
    {
        var text = WarChronicleCheatReport.FormatRally(
            new List<RallyStatusRow> { Row("a", 1, 2, 0.5f, 2, true) }, rallyOn: false);

        StringAssert.Contains(text, "[Rally] Off");
        StringAssert.Contains(text, "no effects are written");
    }

    [TestMethod]
    public void FormatRally_SortsByKingdomId()
    {
        var text = WarChronicleCheatReport.FormatRally(
            new List<RallyStatusRow> { Row("zeta", 1, 1, 0f, 0, true), Row("alpha", 1, 1, 0f, 0, true) }, rallyOn: true);

        Assert.IsTrue(text.IndexOf("alpha", System.StringComparison.Ordinal) < text.IndexOf("zeta", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void FormatRally_NoRows_SaysSo()
    {
        StringAssert.Contains(WarChronicleCheatReport.FormatRally(new List<RallyStatusRow>(), rallyOn: true), "No kingdoms");
        StringAssert.Contains(WarChronicleCheatReport.FormatRally(null!, rallyOn: true), "No kingdoms");
    }

    [TestMethod]
    public void FormatRally_UsesTheInvariantCulture()
    {
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var text = WarChronicleCheatReport.FormatRally(
                new List<RallyStatusRow> { Row("a", 6, 8, 0.25f, 1, true) }, rallyOn: true);

            StringAssert.Contains(text, "loss 0.250");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
