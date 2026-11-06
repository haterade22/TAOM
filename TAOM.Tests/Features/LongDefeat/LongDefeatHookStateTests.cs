using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The Long Defeat hooks share one armed mission and one published table (phase2-plan.md Slice 8, the fold owed from the Slice 3 second-wave review):
/// <see cref="LongDefeatHookState"/> holds them, and the stat hooks and the combat hooks read from it. The attacker damage factor therefore lives in
/// the table alone, and the arm, disarm and publish lifecycle exists once.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatHookStateTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Clean() => LongDefeatHookState.Arm(null);

    private static Mission NewMission() => (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

    [TestMethod]
    public void Unarmed_HoldsNothing_AndIgnoresAPublish()
    {
        LongDefeatHookState.Arm(null);

        LongDefeatHookState.Publish(NewMission(), new LdStatTable(2f));

        Assert.IsNull(LongDefeatHookState.Armed);
        Assert.IsNull(LongDefeatHookState.Table);
    }

    [TestMethod]
    public void Arm_TakesTheMission_AndStartsWithNoTable()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));

        LongDefeatHookState.Arm(mission);

        Assert.AreSame(mission, LongDefeatHookState.Armed);
        Assert.IsNull(LongDefeatHookState.Table, "a new mission never reads the last one's table");
    }

    [TestMethod]
    public void Publish_FromTheArmedMission_ReplacesTheTable_AndRestartsTheBlowCount()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        var first = new LdStatTable(2f);
        var second = new LdStatTable(1.25f);

        LongDefeatHookState.Publish(mission, first);
        LongDefeatHookState.CountRaised();
        LongDefeatHookState.CountRaised();
        Assert.AreEqual(2L, LongDefeatHookState.Raised);
        Assert.AreSame(first, LongDefeatHookState.Table);

        LongDefeatHookState.Publish(mission, second);

        Assert.AreSame(second, LongDefeatHookState.Table);
        Assert.AreEqual(0L, LongDefeatHookState.Raised, "the count shows the seam ran since the last publish");
    }

    [TestMethod]
    public void Publish_FromAnotherMission_IsIgnored_AndKeepsTheCount()
    {
        var armed = NewMission();
        LongDefeatHookState.Arm(armed);
        var table = new LdStatTable(2f);
        LongDefeatHookState.Publish(armed, table);
        LongDefeatHookState.CountRaised();

        LongDefeatHookState.Publish(NewMission(), new LdStatTable(9f));

        Assert.AreSame(table, LongDefeatHookState.Table);
        Assert.AreEqual(1L, LongDefeatHookState.Raised);
    }

    [TestMethod]
    public void Disarm_OfAnotherMission_ChangesNothing_AndOfTheArmedOneClearsBoth()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));

        LongDefeatHookState.Disarm(NewMission());
        Assert.AreSame(mission, LongDefeatHookState.Armed);
        Assert.IsNotNull(LongDefeatHookState.Table);

        LongDefeatHookState.Disarm(mission);
        Assert.IsNull(LongDefeatHookState.Armed);
        Assert.IsNull(LongDefeatHookState.Table);
    }

    [TestMethod]
    public void NeitherHook_KeepsItsOwnArmedMissionOrTable()
    {
        var combat = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatCombatHooks.cs", stripComments: true);
        var stat = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatStatHooks.cs", stripComments: true);

        foreach (var source in new[] { combat, stat })
        {
            Assert.IsFalse(source.Contains("volatile Mission"), "the armed mission lives in LongDefeatHookState");
            Assert.IsFalse(source.Contains("volatile LdStatTable"), "the published table lives in LongDefeatHookState");
            Assert.IsFalse(source.Contains("internal static void Arm("), "one arm lifecycle");
            Assert.IsFalse(source.Contains("internal static void Publish"), "one publish");
        }

        Assert.IsFalse(combat.Contains("_attackerDamage"), "the attackers' damage factor is LdStatTable.AttackerDamage alone");
    }
}
