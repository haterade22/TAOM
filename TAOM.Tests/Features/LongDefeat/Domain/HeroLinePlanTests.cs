using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class HeroLinePlanTests
{
    private static HeroLinePlan Plan() => new(new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
    {
        ["legolas"] = new IReadOnlyList<string>[]
        {
            new[] { "a1", "a2", "a3" },
            new[] { "b1", "b2", "b3" },
            new[] { "c1", "c2", "c3" },
            new[] { "d1", "d2", "d3" },
        },
    });

    private static char LineOf(string? path) => path![0];

    [TestMethod]
    public void Pick_SpawnsOneToFour_GiveLinesAToDInOrder()
    {
        var plan = Plan();

        var lines = Enumerable.Range(1, 4).Select(n => LineOf(plan.Pick("legolas", n, 0UL))).ToArray();

        CollectionAssert.AreEqual(new[] { 'a', 'b', 'c', 'd' }, lines);
    }

    [TestMethod]
    public void Pick_FifthSpawn_WrapsToLineA()
    {
        Assert.AreEqual('a', LineOf(Plan().Pick("legolas", 5, 0UL)));
        Assert.AreEqual('b', LineOf(Plan().Pick("legolas", 6, 0UL)));
    }

    [TestMethod]
    public void Pick_UnknownHero_IsNull() => Assert.IsNull(Plan().Pick("gimli", 1, 0UL));

    [TestMethod]
    public void Pick_NonePlan_IsNull() => Assert.IsNull(HeroLinePlan.None.Pick("legolas", 1, 0UL));

    [TestMethod]
    public void Pick_SpawnZero_IsNull() => Assert.IsNull(Plan().Pick("legolas", 0, 0UL));

    [TestMethod]
    public void Pick_NegativeSpawn_IsNull() => Assert.IsNull(Plan().Pick("legolas", -3, 0UL));

    [TestMethod]
    public void Pick_Take_FollowsSaltPlusSpawnModuloTakes()
    {
        var plan = Plan();

        Assert.AreEqual("a2", plan.Pick("legolas", 1, 0UL));
        Assert.AreEqual("a3", plan.Pick("legolas", 1, 1UL));
        Assert.AreEqual("a1", plan.Pick("legolas", 1, 2UL));
        Assert.AreEqual("a2", plan.Pick("legolas", 1, 3UL));
    }

    [TestMethod]
    public void Pick_Take_EveryTakeIsReachable()
    {
        var plan = Plan();

        var reached = Enumerable.Range(0, 3).Select(salt => plan.Pick("legolas", 2, (ulong)salt)).Distinct().OrderBy(p => p).ToArray();

        CollectionAssert.AreEqual(new[] { "b1", "b2", "b3" }, reached);
    }

    [TestMethod]
    public void Pick_SameInputs_GiveTheSamePath()
    {
        Assert.AreEqual(Plan().Pick("legolas", 3, 12345UL), Plan().Pick("legolas", 3, 12345UL));
    }

    [TestMethod]
    public void Pick_LargeSalt_DoesNotThrow()
    {
        Assert.IsNotNull(Plan().Pick("legolas", 1, ulong.MaxValue));
    }
}
