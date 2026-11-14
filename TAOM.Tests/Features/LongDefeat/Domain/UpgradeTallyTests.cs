using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The tally of an upgrade order's spawns (#1161, Codex F1): a soldier counts as replaced, and is paid for, only when the new soldier is active, and reads back only when he stands within the
/// radius of the old place. The adapter records each spawn and hands the list here.
/// </summary>
[TestClass]
public class UpgradeTallyTests
{
    private const float Radius = 1f;

    [TestMethod]
    public void AnInactiveSpawn_IsNeitherReplacedNorReadBack()
    {
        var report = UpgradeTally.Of(1, new[] { new UpgradeSpawn(false, 0f, "(1.0, 2.0, 3.0) stood at (1.0, 2.0, 3.0)") }, Radius);

        Assert.AreEqual(new LdUpgradeReport(1, 0, 0, null), report);
    }

    [TestMethod]
    public void AnActiveSpawnFarFromTheOldPlace_IsReplacedButNotReadBack_AndNamedAsTheMiss()
    {
        var report = UpgradeTally.Of(1, new[] { new UpgradeSpawn(true, 4f, "(1.0, 2.0, 3.0) stood at (5.0, 2.0, 3.0)") }, Radius);

        Assert.AreEqual(new LdUpgradeReport(1, 1, 0, null, "(1.0, 2.0, 3.0) stood at (5.0, 2.0, 3.0)"), report);
    }

    [TestMethod]
    public void AnActiveSpawnAtTheOldPlace_IsReplacedAndReadBack()
    {
        var report = UpgradeTally.Of(1, new[] { new UpgradeSpawn(true, Radius, "here") }, Radius);

        Assert.AreEqual(new LdUpgradeReport(1, 1, 1, null), report, "the radius itself still reads back");
    }

    [TestMethod]
    public void TheFoundCountPassesThrough_AndOnlyTheFirstMissIsNamed()
    {
        var spawns = new[]
        {
            new UpgradeSpawn(true, 0.2f, "near"),
            new UpgradeSpawn(true, 3f, "first far"),
            new UpgradeSpawn(false, 0f, "inactive"),
            new UpgradeSpawn(true, 9f, "second far"),
        };

        var report = UpgradeTally.Of(30, spawns, Radius);

        Assert.AreEqual(new LdUpgradeReport(30, 3, 1, null, "first far"), report);
    }

    [TestMethod]
    public void NoSpawns_IsAnEmptyReport()
    {
        Assert.AreEqual(new LdUpgradeReport(0, 0, 0, null), UpgradeTally.Of(0, new UpgradeSpawn[0], Radius));
    }
}
