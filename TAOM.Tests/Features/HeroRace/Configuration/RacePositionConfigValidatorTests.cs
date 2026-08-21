using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace.Configuration;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Tests.Features.HeroRace.Configuration;

// The "Config Providers MUST Validate" rule (.claude/rules/csharp-architecture.md) applied to the
// two race-framing configs. Before this validator, RacePositionConfig.LoadConfig caught only PARSE
// failures: a hand-edited "Vertical": NaN deserialises fine and rides straight into a native
// GameEntity.SetFrame. A NaN origin is not a visible mis-frame, it is a character that vanishes.
//
// Fallback policy is deliberately ROW-level, not field-level. A row that keeps two good axes and
// zeroes the third frames the race half-right, which reads as a tuning mistake and sends the next
// person hunting the wrong bug. Dropping the row restores the documented default for an
// unconfigured race (a race with no entry keeps the vanilla frame), which is a state the feature
// already understands.
[TestClass]
public class RacePositionConfigValidatorTests
{
    private static RacePositionConfig Config(params RacePositionConfigItem[] items)
    {
        var config = new RacePositionConfig();
        foreach (var item in items)
            config.Items.Add(item);
        return config;
    }

    private static RacePositionConfigItem Item(string race, float h, float v, float z)
        => new RacePositionConfigItem { Race = race, Horizontal = h, Vertical = v, Zoom = z };

    [TestMethod]
    public void Sanitize_NullConfig_ReturnsEmptyConfigNotNull()
    {
        var result = RacePositionConfigValidator.Sanitize(null, "CharacterAvatarPatch", out var warnings);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Items.Count);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void Sanitize_ValidRows_ArePreservedVerbatim()
    {
        var config = Config(Item("dwarf", 0f, 0.15f, -0.1f), Item("mount_dwarf", 0f, 0.1f, 0f));

        var result = RacePositionConfigValidator.Sanitize(config, "CharacterAvatarPatch", out var warnings);

        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual(0, warnings.Count);
        var dwarf = result.Items.Single(i => i.Race == "dwarf");
        Assert.AreEqual(0.15f, dwarf.Vertical);
        Assert.AreEqual(-0.1f, dwarf.Zoom);
    }

    // --- NaN / infinity: the class of bug this validator exists for -------------------------

    [TestMethod]
    public void Sanitize_NaNVertical_DropsRowAndWarns()
    {
        var config = Config(Item("dwarf", 0f, float.NaN, 0f));

        var result = RacePositionConfigValidator.Sanitize(config, "CharacterAvatarPatch", out var warnings);

        Assert.AreEqual(0, result.Items.Count, "A NaN offset must never reach GameEntity.SetFrame.");
        Assert.IsTrue(warnings.Any(w => w.Contains("dwarf")));
    }

    [TestMethod]
    public void Sanitize_NaNHorizontal_DropsRow()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item("orc", float.NaN, 0f, 0f)), "x", out _);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void Sanitize_NaNZoom_DropsRow()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item("orc", 0f, 0f, float.NaN)), "x", out _);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void Sanitize_PositiveInfinity_DropsRow()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item("orc", 0f, float.PositiveInfinity, 0f)), "x", out _);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void Sanitize_NegativeInfinity_DropsRow()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item("orc", float.NegativeInfinity, 0f, 0f)), "x", out _);
        Assert.AreEqual(0, result.Items.Count);
    }

    // --- Range -------------------------------------------------------------------------------

    [TestMethod]
    public void Sanitize_OffsetAboveMax_DropsRowAndWarns()
    {
        var config = Config(Item("cave_troll", 0f, RacePositionConfigValidator.MaxOffset + 0.1f, 0f));

        var result = RacePositionConfigValidator.Sanitize(config, "CharacterAvatarPatch", out var warnings);

        Assert.AreEqual(0, result.Items.Count);
        Assert.IsTrue(warnings.Any(w => w.Contains("cave_troll")));
    }

    [TestMethod]
    public void Sanitize_OffsetBelowMin_DropsRow()
    {
        var result = RacePositionConfigValidator.Sanitize(
            Config(Item("cave_troll", 0f, 0f, RacePositionConfigValidator.MinOffset - 0.1f)), "x", out _);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void Sanitize_OffsetExactlyAtBounds_IsKept()
    {
        var config = Config(Item("cave_troll", RacePositionConfigValidator.MinOffset, RacePositionConfigValidator.MaxOffset, 0f));

        var result = RacePositionConfigValidator.Sanitize(config, "x", out var warnings);

        Assert.AreEqual(1, result.Items.Count, "Bounds are inclusive.");
        Assert.AreEqual(0, warnings.Count);
    }

    // The donor cave_troll tuning is Zoom -4.0, so the range must accommodate real authored values.
    // A range that rejects genuine data would be a validator that breaks the feature it guards.
    [TestMethod]
    public void Sanitize_LargeButRealisticTrollZoom_IsKept()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item("cave_troll", 0f, -0.6f, -4.0f)), "x", out _);
        Assert.AreEqual(1, result.Items.Count);
    }

    // --- Row identity ------------------------------------------------------------------------

    [TestMethod]
    public void Sanitize_NullRaceName_DropsRow()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item(null, 0f, 0.1f, 0f)), "x", out var warnings);
        Assert.AreEqual(0, result.Items.Count);
        Assert.AreEqual(1, warnings.Count);
    }

    [TestMethod]
    public void Sanitize_EmptyRaceName_DropsRow()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item("   ", 0f, 0.1f, 0f)), "x", out _);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void Sanitize_NullItemInList_IsSkippedWithoutThrowing()
    {
        var config = new RacePositionConfig();
        config.Items.Add(null);
        config.Items.Add(Item("dwarf", 0f, 0.1f, 0f));

        var result = RacePositionConfigValidator.Sanitize(config, "x", out _);

        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("dwarf", result.Items[0].Race);
    }

    // Last-wins matches the pre-existing BuildLookup semantics, so sanitising cannot change which
    // row a race resolves to.
    [TestMethod]
    public void Sanitize_DuplicateRace_KeepsLastAndWarns()
    {
        var config = Config(Item("dwarf", 0f, 0.1f, 0f), Item("DWARF", 0f, 0.9f, 0f));

        var result = RacePositionConfigValidator.Sanitize(config, "x", out var warnings);

        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual(0.9f, result.Items[0].Vertical);
        Assert.IsTrue(warnings.Any(w => w.ToLowerInvariant().Contains("duplicate")));
    }

    [TestMethod]
    public void Sanitize_RaceNamesAreNormalisedToLowerCase()
    {
        var result = RacePositionConfigValidator.Sanitize(Config(Item("Cave_Troll", 0f, 0.1f, 0f)), "x", out _);
        Assert.AreEqual("cave_troll", result.Items[0].Race);
    }

    [TestMethod]
    public void Sanitize_OneBadRowAmongGood_KeepsOnlyTheGoodOnes()
    {
        var config = Config(
            Item("dwarf", 0f, 0.15f, -0.1f),
            Item("orc", 0f, float.NaN, 0f),
            Item("elf", 0f, 0.05f, 0f));

        var result = RacePositionConfigValidator.Sanitize(config, "CharacterAvatarPatch", out var warnings);

        CollectionAssert.AreEquivalent(new[] { "dwarf", "elf" }, result.Items.Select(i => i.Race).ToArray());
        Assert.AreEqual(1, warnings.Count);
    }
}
