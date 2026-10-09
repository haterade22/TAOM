using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.WarChronicle.Effects;

[TestClass]
public class WarEffectServiceTests
{
    private IWarChronicleSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private WarEffectService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IWarChronicleSettingsProvider>();
        _settings.WarEffectStrength.Returns(1f);
        _logger = Substitute.For<IModLogger>();
        _sut = new WarEffectService(_settings, _logger);
    }

    private static WarEffect Fx(string source = "s", string kingdom = "k",
        WarEffectKind kind = WarEffectKind.VolunteerRate, float magnitude = 0.1f, double end = 100d) =>
        new WarEffect(source, kingdom, kind, magnitude, end);

    [TestMethod]
    public void GetMultiplier_NoEffects_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void GetMultiplier_NullOrUnknownKingdom_ReturnsOne()
    {
        _sut.Apply(Fx());

        Assert.AreEqual(1f, _sut.GetMultiplier(null, WarEffectKind.VolunteerRate));
        Assert.AreEqual(1f, _sut.GetMultiplier("", WarEffectKind.VolunteerRate));
        Assert.AreEqual(1f, _sut.GetMultiplier("other", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void GetMultiplier_UndefinedKind_ReturnsOne()
    {
        _sut.Apply(Fx());

        Assert.AreEqual(1f, _sut.GetMultiplier("k", (WarEffectKind)99));
        Assert.AreEqual(1f, _sut.GetMultiplier("k", (WarEffectKind)(-1)));
    }

    [TestMethod]
    public void GetMultiplier_AfterApply_IsOnePlusMagnitude()
    {
        _sut.Apply(Fx(magnitude: 0.15f));

        Assert.AreEqual(1.15f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void GetMultiplier_OtherKindOfTheSameKingdom_IsUntouched()
    {
        _sut.Apply(Fx(magnitude: 0.15f));

        Assert.AreEqual(1f, _sut.GetMultiplier("k", WarEffectKind.PrisonerEscape));
    }

    [TestMethod]
    public void Apply_SameKeyTwice_RefreshesInsteadOfStacking()
    {
        _sut.Apply(Fx(magnitude: 0.1f, end: 100));
        _sut.Apply(Fx(magnitude: 0.1f, end: 200));

        Assert.AreEqual(1, _sut.Snapshot().Count);
        Assert.AreEqual(1.1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(200d, _sut.Snapshot()[0].EndTimeHours);
    }

    [TestMethod]
    public void Apply_SameKeyNewMagnitude_ReplacesTheMagnitude()
    {
        _sut.Apply(Fx(magnitude: 0.1f));
        _sut.Apply(Fx(magnitude: 0.2f));

        Assert.AreEqual(1.2f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void GetMultiplier_TwoSources_Sum()
    {
        _sut.Apply(Fx("a", magnitude: 0.1f));
        _sut.Apply(Fx("b", magnitude: 0.15f));

        Assert.AreEqual(1.25f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void GetMultiplier_TwoKingdoms_AreIndependent()
    {
        _sut.Apply(Fx(kingdom: "a", magnitude: 0.1f));
        _sut.Apply(Fx(kingdom: "b", magnitude: -0.1f));

        Assert.AreEqual(1.1f, _sut.GetMultiplier("a", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(0.9f, _sut.GetMultiplier("b", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 100d)]
    [DataRow(float.PositiveInfinity, 100d)]
    [DataRow(float.NegativeInfinity, 100d)]
    [DataRow(0.1f, double.NaN)]
    [DataRow(0.1f, double.PositiveInfinity)]
    [DataRow(0.1f, double.NegativeInfinity)]
    public void Apply_NonFiniteNumber_IsRejectedWithAWarning(float magnitude, double end)
    {
        _sut.Apply(Fx(magnitude: magnitude, end: end));

        Assert.AreEqual(0, _sut.Snapshot().Count);
        Assert.AreEqual(1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(null, "k")]
    [DataRow("", "k")]
    [DataRow("   ", "k")]
    [DataRow("s", null)]
    [DataRow("s", "")]
    [DataRow("s", "  ")]
    public void Apply_EmptyIds_IsRejectedWithAWarning(string? source, string? kingdom)
    {
        _sut.Apply(new WarEffect(source!, kingdom!, WarEffectKind.VolunteerRate, 0.1f, 100d));

        Assert.AreEqual(0, _sut.Snapshot().Count);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_UndefinedKind_IsRejectedWithAWarning()
    {
        _sut.Apply(Fx(kind: (WarEffectKind)42));

        Assert.AreEqual(0, _sut.Snapshot().Count);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_Null_IsRejectedWithAWarning()
    {
        _sut.Apply(null!);

        Assert.AreEqual(0, _sut.Snapshot().Count);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetMultiplier_VolunteerRateAboveTheCeiling_ClampsToOnePointFive()
    {
        _sut.Apply(Fx("a", magnitude: 0.9f));
        _sut.Apply(Fx("b", magnitude: 0.9f));

        Assert.AreEqual(1.5f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void GetMultiplier_VolunteerRateBelowTheFloor_ClampsToHalf()
    {
        _sut.Apply(Fx("a", magnitude: -0.9f));
        _sut.Apply(Fx("b", magnitude: -0.9f));

        Assert.AreEqual(0.5f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void GetMultiplier_PrisonerEscapeAboveTheCeiling_ClampsToThree()
    {
        _sut.Apply(Fx(kind: WarEffectKind.PrisonerEscape, magnitude: 5f));

        Assert.AreEqual(3f, _sut.GetMultiplier("k", WarEffectKind.PrisonerEscape));
    }

    [TestMethod]
    public void GetMultiplier_PrisonerEscapeBelowTheFloor_ClampsToOne()
    {
        _sut.Apply(Fx(kind: WarEffectKind.PrisonerEscape, magnitude: -0.5f));

        Assert.AreEqual(1f, _sut.GetMultiplier("k", WarEffectKind.PrisonerEscape));
    }

    [DataTestMethod]
    [DataRow(0.2f)]
    [DataRow(1.4f)]
    [DataRow(9f)]
    public void Clamp_AKindWithNoClampOfItsOwn_IsOneNotTheVolunteerClamp(float multiplier)
    {
        // A new kind must name its own range; it never falls into the volunteer clamp by default.
        Assert.AreEqual(1f, WarEffectMath.Clamp((WarEffectKind)99, multiplier));
    }

    [TestMethod]
    public void GetMultiplier_StrengthZero_TurnsEveryEffectOff()
    {
        _settings.WarEffectStrength.Returns(0f);
        _sut.Apply(Fx(magnitude: 0.2f));

        Assert.AreEqual(1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void GetMultiplier_StrengthTwo_DoublesTheMagnitude()
    {
        _settings.WarEffectStrength.Returns(2f);
        _sut.Apply(Fx(magnitude: 0.1f));

        Assert.AreEqual(1.2f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1.1f)]
    [DataRow(float.PositiveInfinity, 1.1f)]
    [DataRow(5f, 1.2f)]
    [DataRow(-3f, 1f)]
    public void GetMultiplier_StrengthFromSettingsIsSanitised(float strength, float expected)
    {
        _settings.WarEffectStrength.Returns(strength);
        _sut.Apply(Fx(magnitude: 0.1f));

        Assert.AreEqual(expected, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void Expire_AtExactlyTheEndTime_RemovesTheEffect()
    {
        _sut.Apply(Fx(end: 100));

        _sut.Expire(99.9);
        Assert.AreEqual(1, _sut.Snapshot().Count, "still running a moment before the end");

        _sut.Expire(100d);

        Assert.AreEqual(0, _sut.Snapshot().Count);
        Assert.AreEqual(1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void Expire_KeepsTheEffectsThatHaveNotEnded()
    {
        _sut.Apply(Fx("old", end: 50));
        _sut.Apply(Fx("new", end: 500, magnitude: 0.2f));

        _sut.Expire(100d);

        Assert.AreEqual(1, _sut.Snapshot().Count);
        Assert.AreEqual("new", _sut.Snapshot()[0].SourceId);
        Assert.AreEqual(1.2f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void Expire_AlsoRebakesAChangedStrength()
    {
        _sut.Apply(Fx(end: 500, magnitude: 0.1f));
        _settings.WarEffectStrength.Returns(2f);
        Assert.AreEqual(1.1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f, "the bake is cached");

        _sut.Expire(10d);

        Assert.AreEqual(1.2f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void Expire_NonFiniteNow_DoesNothing(double now)
    {
        _sut.Apply(Fx(end: 100));

        _sut.Expire(now);

        Assert.AreEqual(1, _sut.Snapshot().Count);
        Assert.AreEqual(1.1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void RemoveSource_RemovesEveryKingdomAndKindOfThatSource()
    {
        _sut.Apply(Fx("rally", "a", WarEffectKind.VolunteerRate, 0.1f));
        _sut.Apply(Fx("rally", "b", WarEffectKind.PrisonerEscape, 0.5f));
        _sut.Apply(Fx("event", "a", WarEffectKind.VolunteerRate, 0.2f));

        _sut.RemoveSource("rally");

        Assert.AreEqual(1, _sut.Snapshot().Count);
        Assert.AreEqual("event", _sut.Snapshot()[0].SourceId);
        Assert.AreEqual(1.2f, _sut.GetMultiplier("a", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(1f, _sut.GetMultiplier("b", WarEffectKind.PrisonerEscape));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("nobody")]
    public void RemoveSource_NullEmptyOrUnknown_ChangesNothing(string? source)
    {
        _sut.Apply(Fx());

        _sut.RemoveSource(source!);

        Assert.AreEqual(1, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void Snapshot_ReturnsACopy()
    {
        _sut.Apply(Fx());

        var first = _sut.Snapshot();
        ((IList<WarEffect>)first).Clear();
        _sut.Apply(Fx("later"));

        Assert.AreEqual(2, _sut.Snapshot().Count);
        Assert.AreNotSame(_sut.Snapshot(), _sut.Snapshot());
    }

    [TestMethod]
    public void RestoreFromSave_ReplacesTheRegistryAndBakes()
    {
        _sut.Apply(Fx("stale", "gone"));

        _sut.RestoreFromSave(new[] { Fx("a", "k", WarEffectKind.VolunteerRate, 0.2f) });

        Assert.AreEqual(1, _sut.Snapshot().Count);
        Assert.AreEqual(1.2f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(1f, _sut.GetMultiplier("gone", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void RestoreFromSave_Null_ClearsTheRegistry()
    {
        _sut.Apply(Fx());

        _sut.RestoreFromSave(null!);

        Assert.AreEqual(0, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void RestoreFromSave_SkipsInvalidRowsAndKeepsTheRest()
    {
        var rows = new List<WarEffect>
        {
            Fx("good", magnitude: 0.1f),
            Fx("nan", magnitude: float.NaN),
            Fx("", magnitude: 0.1f),
            Fx("undefined", kind: (WarEffectKind)7),
            null!,
        };

        _sut.RestoreFromSave(rows);

        CollectionAssert.AreEqual(new[] { "good" }, _sut.Snapshot().Select(e => e.SourceId).ToArray());
    }

    [TestMethod]
    public void RestoreFromSave_DuplicateKeys_KeepOneRow()
    {
        _sut.RestoreFromSave(new[] { Fx(magnitude: 0.1f, end: 100), Fx(magnitude: 0.3f, end: 200) });

        Assert.AreEqual(1, _sut.Snapshot().Count);
        Assert.AreEqual(1.3f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsEffectsAndMultipliers()
    {
        _sut.Apply(Fx());

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.Snapshot().Count);
        Assert.AreEqual(1f, _sut.GetMultiplier("k", WarEffectKind.VolunteerRate));
    }
}
