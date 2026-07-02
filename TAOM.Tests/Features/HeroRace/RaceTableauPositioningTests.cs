using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;
using TAOM.Features.HeroRace.Configuration;
using TaleWorlds.Library;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Tests.Features.HeroRace;

// Pure race-position math extracted from CharacterTableauService (R5, round-3): the per-race frame
// offset block was duplicated FOUR times inside the service (character + mount frames in both
// RefreshCharacterTableau and UpdateMount) with zero tests. Pins the axis mapping — config
// Horizontal -> origin.y, Vertical -> origin.z, Zoom -> origin.x (NOT the intuitive x/y/z order) —
// and the case-insensitive config lookup semantics.
[TestClass]
public class RaceTableauPositioningTests
{
    private static RacePositionConfigItem Item(string race, float h, float v, float z)
        => new RacePositionConfigItem { Race = race, Horizontal = h, Vertical = v, Zoom = z };

    private static RacePositionConfig Config(params RacePositionConfigItem[] items)
    {
        var config = new RacePositionConfig();
        foreach (var item in items)
            config.Items.Add(item);
        return config;
    }

    private static MatrixFrame BaseFrame()
    {
        var frame = MatrixFrame.Identity;
        frame.origin = new Vec3(10f, 20f, 30f);
        return frame;
    }

    // ------------------------------------------------------------------ ApplyOffset

    [TestMethod]
    public void ApplyOffset_NullItem_ReturnsBaseUnchanged()
    {
        var baseFrame = BaseFrame();
        var result = RaceTableauPositioning.ApplyOffset(baseFrame, null);
        Assert.AreEqual(baseFrame.origin.x, result.origin.x);
        Assert.AreEqual(baseFrame.origin.y, result.origin.y);
        Assert.AreEqual(baseFrame.origin.z, result.origin.z);
    }

    [TestMethod]
    public void ApplyOffset_MapsHorizontalToY_VerticalToZ_ZoomToX()
    {
        // The load-bearing (and unintuitive) axis mapping — a silent swap here misplaces every
        // custom-race portrait in the party/encyclopedia/inventory tableaus.
        var result = RaceTableauPositioning.ApplyOffset(BaseFrame(), Item("dwarf", h: 1f, v: 2f, z: 3f));
        Assert.AreEqual(10f + 3f, result.origin.x, 1e-5f, "Zoom must offset origin.x");
        Assert.AreEqual(20f + 1f, result.origin.y, 1e-5f, "Horizontal must offset origin.y");
        Assert.AreEqual(30f + 2f, result.origin.z, 1e-5f, "Vertical must offset origin.z");
    }

    [TestMethod]
    public void ApplyOffset_PreservesRotation()
    {
        var result = RaceTableauPositioning.ApplyOffset(BaseFrame(), Item("dwarf", 1f, 2f, 3f));
        Assert.AreEqual(MatrixFrame.Identity.rotation.f.x, result.rotation.f.x);
        Assert.AreEqual(MatrixFrame.Identity.rotation.f.y, result.rotation.f.y);
        Assert.AreEqual(MatrixFrame.Identity.rotation.u.z, result.rotation.u.z);
    }

    [TestMethod]
    public void ApplyOffset_DoesNotMutateTheBaseFrame()
    {
        var baseFrame = BaseFrame();
        RaceTableauPositioning.ApplyOffset(baseFrame, Item("dwarf", 1f, 2f, 3f));
        Assert.AreEqual(10f, baseFrame.origin.x, "MatrixFrame is a struct; base must stay intact");
        Assert.AreEqual(20f, baseFrame.origin.y);
        Assert.AreEqual(30f, baseFrame.origin.z);
    }

    // ------------------------------------------------------------------ BuildLookup

    [TestMethod]
    public void BuildLookup_IsCaseInsensitive()
    {
        var lookup = RaceTableauPositioning.BuildLookup(Config(Item("Dwarf", 1f, 2f, 3f)));
        Assert.IsTrue(lookup.TryGetValue("dwarf", out var item));
        Assert.AreEqual(1f, item.Horizontal);
    }

    [TestMethod]
    public void BuildLookup_SkipsEmptyAndNullRaceEntries()
    {
        var lookup = RaceTableauPositioning.BuildLookup(
            Config(Item("", 1f, 2f, 3f), Item(null, 4f, 5f, 6f), Item("elf", 7f, 8f, 9f)));
        Assert.AreEqual(1, lookup.Count);
        Assert.IsTrue(lookup.ContainsKey("elf"));
    }

    [TestMethod]
    public void BuildLookup_DuplicateRace_LastEntryWins()
    {
        var lookup = RaceTableauPositioning.BuildLookup(
            Config(Item("orc", 1f, 0f, 0f), Item("ORC", 9f, 0f, 0f)));
        Assert.AreEqual(9f, lookup["orc"].Horizontal);
    }

    [TestMethod]
    public void BuildLookup_NullConfig_ReturnsEmpty()
        => Assert.AreEqual(0, RaceTableauPositioning.BuildLookup(null).Count);
}
