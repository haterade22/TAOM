using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;
using TAOM.Features.HeroRace.Cheats;
using TAOM.Features.HeroRace.Configuration;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Tests.Features.HeroRace;

// The tuner's argument handling, which used to live inline and private inside the
// [CommandLineArgumentFunction] statics. Nothing dispatched across the engine's native console
// boundary is reachable from a test, so the only arithmetic in the feature (the nudge bound check)
// could be broken without the build or the suite noticing.
//
// Two of these cases are real defects the console could previously commit:
//   - an unvalidated race name became a live row and was persisted, dead forever after;
//   - "mount_" was advertised on the 2D surface, which has no mount row on any code path.
[TestClass]
public class RacePositionTuningParserTests
{
    private static bool KnownRaces(string race)
        => race == "dwarf" || race == "cave_troll" || race == "elf";

    private static RacePositionConfigItem Item(float h, float v, float z)
        => new RacePositionConfigItem { Race = "dwarf", Horizontal = h, Vertical = v, Zoom = z };

    // --- Surface ------------------------------------------------------------------------------

    [TestMethod]
    public void TryParseSurface_Avatar_Parses()
    {
        Assert.IsTrue(RacePositionTuningParser.TryParseSurface("avatar", out var s, out _));
        Assert.AreEqual(RacePositionSurface.Avatar, s);
    }

    [TestMethod]
    public void TryParseSurface_ImageMixedCaseAndPadded_Parses()
    {
        Assert.IsTrue(RacePositionTuningParser.TryParseSurface("  Image ", out var s, out _));
        Assert.AreEqual(RacePositionSurface.Image, s);
    }

    [TestMethod]
    public void TryParseSurface_Unknown_FailsWithMessage()
    {
        Assert.IsFalse(RacePositionTuningParser.TryParseSurface("portrait", out _, out var error));
        StringAssert.Contains(error, "portrait");
    }

    [TestMethod]
    public void TryParseSurface_Null_Fails()
        => Assert.IsFalse(RacePositionTuningParser.TryParseSurface(null, out _, out _));

    // --- Axis ---------------------------------------------------------------------------------

    [TestMethod]
    public void TryParseAxis_AcceptsTheThreeAxes()
    {
        foreach (var axis in new[] { "h", "v", "z" })
            Assert.IsTrue(RacePositionTuningParser.TryParseAxis(axis, out _, out _), axis);
    }

    [TestMethod]
    public void TryParseAxis_Unknown_Fails()
        => Assert.IsFalse(RacePositionTuningParser.TryParseAxis("x", out _, out _));

    // --- Offsets ------------------------------------------------------------------------------

    [TestMethod]
    public void TryParseOffset_PlainDecimal_Parses()
    {
        Assert.IsTrue(RacePositionTuningParser.TryParseOffset("-0.15", "vertical", out var v, out _));
        Assert.AreEqual(-0.15f, v, 0.0001f);
    }

    // Invariant culture, not the user's. A German locale would otherwise read "0.15" as 15.
    [TestMethod]
    public void TryParseOffset_UsesInvariantCultureNotCommaDecimals()
    {
        Assert.IsFalse(RacePositionTuningParser.TryParseOffset("0,15", "vertical", out _, out _));
    }

    [TestMethod]
    public void TryParseOffset_NotANumber_Fails()
        => Assert.IsFalse(RacePositionTuningParser.TryParseOffset("high", "vertical", out _, out _));

    [TestMethod]
    public void TryParseOffset_OutOfRange_FailsAndNamesTheBound()
    {
        var tooBig = (RacePositionConfigValidator.MaxOffset + 1f).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsFalse(RacePositionTuningParser.TryParseOffset(tooBig, "zoom", out _, out var error));
        StringAssert.Contains(error, "zoom");
    }

    [TestMethod]
    public void TryParseOffset_NaNLiteral_Fails()
        => Assert.IsFalse(RacePositionTuningParser.TryParseOffset("NaN", "zoom", out _, out _));

    [TestMethod]
    public void TryParseOffset_InfinityLiteral_Fails()
        => Assert.IsFalse(RacePositionTuningParser.TryParseOffset("Infinity", "zoom", out _, out _));

    // --- Race resolution ----------------------------------------------------------------------

    [TestMethod]
    public void TryResolveRace_KnownRace_Resolves()
    {
        Assert.IsTrue(RacePositionTuningParser.TryResolveRace(
            "Dwarf", RacePositionSurface.Avatar, KnownRaces, null, out var race, out _));
        Assert.AreEqual("dwarf", race);
    }

    // The defect: before validation, a typo created a live row, taom.save_race_offsets persisted it,
    // and nothing would ever look it up again.
    [TestMethod]
    public void TryResolveRace_UnknownRace_IsRejectedRatherThanCreatingADeadRow()
    {
        Assert.IsFalse(RacePositionTuningParser.TryResolveRace(
            "dwafr", RacePositionSurface.Avatar, KnownRaces, null, out _, out var error));
        StringAssert.Contains(error, "dwafr");
    }

    [TestMethod]
    public void TryResolveRace_MountPrefixOnAvatar_ValidatesTheRaceBehindThePrefix()
    {
        Assert.IsTrue(RacePositionTuningParser.TryResolveRace(
            "mount_dwarf", RacePositionSurface.Avatar, KnownRaces, null, out var race, out _));
        Assert.AreEqual("mount_dwarf", race);
    }

    [TestMethod]
    public void TryResolveRace_MountPrefixWithUnknownRace_IsRejected()
        => Assert.IsFalse(RacePositionTuningParser.TryResolveRace(
            "mount_dwafr", RacePositionSurface.Avatar, KnownRaces, null, out _, out _));

    // The 2D portrait path only ever calls ResolveImage with a plain race name, so a mount_ row on
    // that surface reports success, persists, and is read by nothing.
    [TestMethod]
    public void TryResolveRace_MountPrefixOnImageSurface_IsRejected()
    {
        Assert.IsFalse(RacePositionTuningParser.TryResolveRace(
            "mount_dwarf", RacePositionSurface.Image, KnownRaces, null, out _, out var error));
        StringAssert.Contains(error, "image");
    }

    [TestMethod]
    public void TryResolveRace_BareMountPrefix_IsRejected()
        => Assert.IsFalse(RacePositionTuningParser.TryResolveRace(
            "mount_", RacePositionSurface.Avatar, KnownRaces, null, out _, out _));

    [TestMethod]
    public void TryResolveRace_Dot_ResolvesToTheOnScreenRace()
    {
        Assert.IsTrue(RacePositionTuningParser.TryResolveRace(
            ".", RacePositionSurface.Avatar, KnownRaces, "Cave_Troll", out var race, out _));
        Assert.AreEqual("cave_troll", race);
    }

    [TestMethod]
    public void TryResolveRace_DotWithNothingOnScreen_FailsWithGuidance()
    {
        Assert.IsFalse(RacePositionTuningParser.TryResolveRace(
            ".", RacePositionSurface.Avatar, KnownRaces, null, out _, out var error));
        StringAssert.Contains(error, "tableau");
    }

    [TestMethod]
    public void TryResolveRace_Empty_Fails()
        => Assert.IsFalse(RacePositionTuningParser.TryResolveRace(
            "  ", RacePositionSurface.Avatar, KnownRaces, null, out _, out _));

    // --- Nudge --------------------------------------------------------------------------------

    [TestMethod]
    public void TryNudge_WithinRange_ReturnsTheNewTripleWithoutMutatingTheRow()
    {
        var item = Item(0f, 0.10f, 0f);

        Assert.IsTrue(RacePositionTuningParser.TryNudge(item, "v", 0.05f, out var h, out var v, out var z));

        Assert.AreEqual(0.15f, v, 0.0001f);
        Assert.AreEqual(0f, h);
        Assert.AreEqual(0f, z);
        Assert.AreEqual(0.10f, item.Vertical, "TryNudge must not mutate; the caller commits.");
    }

    // Reject rather than clamp: a clamping nudge looks like the key stopped working.
    [TestMethod]
    public void TryNudge_PastTheBound_IsRejectedRatherThanClamped()
    {
        var item = Item(0f, RacePositionConfigValidator.MaxOffset, 0f);

        Assert.IsFalse(RacePositionTuningParser.TryNudge(item, "v", 1f, out _, out _, out _));
    }

    [TestMethod]
    public void TryNudge_PastTheLowerBound_IsRejected()
    {
        var item = Item(0f, 0f, RacePositionConfigValidator.MinOffset);
        Assert.IsFalse(RacePositionTuningParser.TryNudge(item, "z", -1f, out _, out _, out _));
    }

    [TestMethod]
    public void TryNudge_NonFiniteDelta_IsRejected()
    {
        var item = Item(0f, 0f, 0f);
        Assert.IsFalse(RacePositionTuningParser.TryNudge(item, "v", float.NaN, out _, out _, out _));
        Assert.IsFalse(RacePositionTuningParser.TryNudge(item, "v", float.PositiveInfinity, out _, out _, out _));
    }

    [TestMethod]
    public void TryNudge_UnknownAxis_IsRejected()
    {
        var item = Item(0f, 0f, 0f);
        Assert.IsFalse(RacePositionTuningParser.TryNudge(item, "x", 0.1f, out _, out _, out _));
    }

    [TestMethod]
    public void TryNudge_EachAxisMovesOnlyItself()
    {
        var item = Item(1f, 2f, 3f);

        RacePositionTuningParser.TryNudge(item, "h", 0.5f, out var h1, out var v1, out var z1);
        Assert.AreEqual(1.5f, h1, 0.0001f);
        Assert.AreEqual(2f, v1);
        Assert.AreEqual(3f, z1);

        RacePositionTuningParser.TryNudge(item, "z", 0.5f, out var h2, out var v2, out var z2);
        Assert.AreEqual(1f, h2);
        Assert.AreEqual(2f, v2);
        Assert.AreEqual(3.5f, z2, 0.0001f);
    }

    // --- Format -------------------------------------------------------------------------------

    [TestMethod]
    public void Format_UsesThreeDecimalsForEachAxis()
    {
        StringAssert.Contains(RacePositionTuningParser.Format(Item(0f, 0.15f, -0.1f)), "v=0.150");
        StringAssert.Contains(RacePositionTuningParser.Format(Item(0f, 0.15f, -0.1f)), "z=-0.100");
    }
}
