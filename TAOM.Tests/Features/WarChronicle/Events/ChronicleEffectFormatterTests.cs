using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The event window's effects list from an outcome report: readable targets, the value the registry applies at the current
/// strength, one shared duration or one per row, and no section at all when nothing applied.
/// </summary>
[TestClass]
public class ChronicleEffectFormatterTests
{
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        ["isengard"] = "Isengard",
        ["empire"] = "Mordor",
        ["vlandia"] = "Rohan",
        ["empire_s"] = "Umbar",
    };

    private static EventWindowEffects? Format(ChronicleOutcomeReport? report, float strength = 1f, int momentumDays = 21) =>
        ChronicleEffectFormatter.Format(report, strength, momentumDays,
            raw => "L:" + raw.Substring(raw.IndexOf('}') + 1),
            days => "For " + days + " days",
            id => Names.TryGetValue(id, out var name) ? name : string.Empty);

    private static ChronicleAppliedEffect Row(string target, WarEffectKind kind, float magnitude, int days, params string[] kingdoms) =>
        new ChronicleAppliedEffect(target, kingdoms, kind, magnitude, days);

    private static ChronicleOutcomeReport Report(ChronicleAppliedMomentum? momentum, params ChronicleAppliedEffect[] rows) =>
        new ChronicleOutcomeReport(rows, momentum);

    [TestMethod]
    public void Format_NullReport_ReturnsNull() => Assert.IsNull(Format(null));

    [TestMethod]
    public void Format_EmptyReport_ReturnsNull() => Assert.IsNull(Format(ChronicleOutcomeReport.None));

    [TestMethod]
    public void Format_Blackgate_SharedDurationOnceAndRowsWithoutTheirOwn()
    {
        var effects = Format(Report(null,
            Row("free", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia"),
            Row("empire_s", WarEffectKind.PrisonerEscape, 0.2f, 15, "empire_s")))!;

        Assert.AreEqual(2, effects.Rows.Count);
        Assert.AreEqual("L:Free Peoples", effects.Rows[0].Target);
        Assert.AreEqual("L:Volunteer recruitment rate", effects.Rows[0].Label);
        Assert.AreEqual("+10%", effects.Rows[0].Value);
        Assert.IsNull(effects.Rows[0].Duration);
        Assert.AreEqual("Umbar", effects.Rows[1].Target);
        Assert.AreEqual("L:Additional escape roll", effects.Rows[1].Label);
        Assert.AreEqual("+20%", effects.Rows[1].Value);
        Assert.AreEqual("For 15 days", effects.Duration);
        Assert.AreEqual("L:Values are this event's contributions before caps, and the caps apply even to a single effect. "
            + "Your own clan's fiefs and lords are never affected. "
            + "L:The additional escape roll's chance is the shown percentage of a lord's usual escape chance.", effects.Note);
    }

    [TestMethod]
    public void Format_NoEscapeRow_NoteLeavesOutTheEscapeExplanation()
    {
        var effects = Format(Report(null, Row("vlandia", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia")))!;

        Assert.AreEqual("L:Values are this event's contributions before caps, and the caps apply even to a single effect. "
            + "Your own clan's fiefs and lords are never affected.", effects.Note);
    }

    [TestMethod]
    public void Format_ContributionAboveTheCap_ShowsTheUncappedContribution()
    {
        // The registry clamps volunteers to +50%; the row is this event's contribution, and the note says caps apply.
        var effects = Format(Report(null, Row("vlandia", WarEffectKind.VolunteerRate, 0.4f, 15, "vlandia")), strength: 2f)!;

        Assert.AreEqual("+80%", effects.Rows.Single().Value);
    }

    [TestMethod]
    public void Format_EvilTarget_IsTheShadowSideName()
    {
        var effects = Format(Report(null, Row("evil", WarEffectKind.VolunteerRate, -0.05f, 10, "isengard", "empire")))!;

        Assert.AreEqual("L:The Shadow", effects.Rows.Single().Target);
        Assert.AreEqual("-5%", effects.Rows.Single().Value);
    }

    [TestMethod]
    public void Format_AttackerTarget_JoinsTheReachedKingdomsNames()
    {
        var effects = Format(Report(null, Row("attacker", WarEffectKind.PrisonerEscape, 0.2f, 25, "isengard", "empire")))!;

        Assert.AreEqual("Isengard, Mordor", effects.Rows.Single().Target);
    }

    [TestMethod]
    public void Format_KingdomWithoutAName_FallsBackToItsId()
    {
        var effects = Format(Report(null, Row("defender", WarEffectKind.VolunteerRate, 0.15f, 25, "rivendell")))!;

        Assert.AreEqual("rivendell", effects.Rows.Single().Target);
    }

    [TestMethod]
    public void Format_MixedDurations_EachRowCarriesItsOwnAndNoSharedLine()
    {
        var effects = Format(Report(null,
            Row("vlandia", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia"),
            Row("isengard", WarEffectKind.VolunteerRate, -0.15f, 30, "isengard")))!;

        Assert.IsNull(effects.Duration);
        Assert.AreEqual("For 15 days", effects.Rows[0].Duration);
        Assert.AreEqual("For 30 days", effects.Rows[1].Duration);
    }

    [TestMethod]
    public void Format_StrengthTwo_DoublesTheShownValue()
    {
        var effects = Format(Report(null, Row("vlandia", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia")), strength: 2f)!;

        Assert.AreEqual("+20%", effects.Rows.Single().Value);
    }

    [TestMethod]
    public void Format_StrengthHalf_ShowsTheFractionalValue()
    {
        var effects = Format(Report(null, Row("vlandia", WarEffectKind.VolunteerRate, 0.15f, 15, "vlandia")), strength: 0.5f)!;

        Assert.AreEqual("+7.5%", effects.Rows.Single().Value);
    }

    [TestMethod]
    public void Format_StrengthZero_HidesTheTimedRows()
    {
        Assert.IsNull(Format(Report(null, Row("vlandia", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia")), strength: 0f));
    }

    [TestMethod]
    public void Format_StrengthNaN_UsesTheDefaultStrengthLikeTheRegistry()
    {
        var effects = Format(Report(null, Row("vlandia", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia")), strength: float.NaN)!;

        Assert.AreEqual("+10%", effects.Rows.Single().Value);
    }

    [TestMethod]
    public void Format_StrengthZeroWithMomentum_KeepsTheMomentumRowWithoutTheNote()
    {
        var effects = Format(Report(new ChronicleAppliedMomentum(MomentumSide.Free, 40),
            Row("vlandia", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia")), strength: 0f)!;

        var row = effects.Rows.Single();
        Assert.AreEqual("L:Free Peoples", row.Target);
        Assert.AreEqual("L:War of the Ring momentum", row.Label);
        Assert.AreEqual("+40", row.Value);
        Assert.IsNull(effects.Note);
    }

    [TestMethod]
    public void Format_MomentumSharingTheEffectsDuration_UsesTheSharedLine()
    {
        var effects = Format(Report(new ChronicleAppliedMomentum(MomentumSide.Evil, 30),
            Row("evil", WarEffectKind.VolunteerRate, 0.1f, 21, "isengard")), momentumDays: 21)!;

        Assert.AreEqual("For 21 days", effects.Duration);
        Assert.AreEqual("L:The Shadow", effects.Rows[1].Target);
        Assert.IsNull(effects.Rows[1].Duration);
    }

    [TestMethod]
    public void Format_MomentumWithAnotherDuration_GoesPerRow()
    {
        var effects = Format(Report(new ChronicleAppliedMomentum(MomentumSide.Free, 30),
            Row("free", WarEffectKind.VolunteerRate, 0.1f, 15, "vlandia")), momentumDays: 21)!;

        Assert.IsNull(effects.Duration);
        Assert.AreEqual("For 15 days", effects.Rows[0].Duration);
        Assert.AreEqual("For 21 days", effects.Rows[1].Duration);
    }

    [TestMethod]
    public void Format_MomentumDurationUnknown_NoDurationAnywhereForThatRow()
    {
        var effects = Format(Report(new ChronicleAppliedMomentum(MomentumSide.Free, 30)), momentumDays: 0)!;

        Assert.IsNull(effects.Duration);
        Assert.IsNull(effects.Rows.Single().Duration);
    }
}
