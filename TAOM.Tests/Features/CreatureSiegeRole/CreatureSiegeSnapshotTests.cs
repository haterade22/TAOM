using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The immutable per-mission record the two game models read from the engine's AI and hit threads: the mission it belongs
/// to, the creature race mask, the validated gate damage multiplier and the gates' destruction components. One volatile
/// reference, replaced whole and cleared by token, so a reader needs no lock and a stale record can never apply to another
/// mission.
/// </summary>
[TestClass]
public class CreatureSiegeSnapshotTests
{
    [TestInitialize]
    [TestCleanup]
    public void ClearTheStaticSnapshot() => CreatureSiegeSnapshot.Clear();

    private static CreatureSiegeSnapshot Make(object? token = null, bool[]? mask = null, float multiplier = 2f,
        params object?[] gates) =>
        new(token ?? new object(), mask ?? new[] { false, true, true }, multiplier, gates);

    // --- the static slot --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Current_BeforeAnyPublish_IsNull()
    {
        Assert.IsNull(CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void Publish_MakesTheSnapshotCurrent()
    {
        var snapshot = Make();

        CreatureSiegeSnapshot.Publish(snapshot);

        Assert.AreSame(snapshot, CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void Publish_ASecondSnapshot_ReplacesTheFirst()
    {
        CreatureSiegeSnapshot.Publish(Make());
        var second = Make();

        CreatureSiegeSnapshot.Publish(second);

        Assert.AreSame(second, CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void Publish_Null_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => CreatureSiegeSnapshot.Publish(null!));
    }

    [TestMethod]
    public void ClearIf_TheSnapshotsOwnToken_ClearsIt()
    {
        var token = new object();
        CreatureSiegeSnapshot.Publish(Make(token));

        Assert.IsTrue(CreatureSiegeSnapshot.ClearIf(token));
        Assert.IsNull(CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void ClearIf_AForeignToken_LeavesTheSnapshotAlone()
    {
        // A mission that never published (an inert one) ends and asks for its own token: it must not clear another's.
        var snapshot = Make();
        CreatureSiegeSnapshot.Publish(snapshot);

        Assert.IsFalse(CreatureSiegeSnapshot.ClearIf(new object()));
        Assert.AreSame(snapshot, CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void ClearIf_NothingPublished_IsFalse_AndTwiceIsHarmless()
    {
        var token = new object();

        Assert.IsFalse(CreatureSiegeSnapshot.ClearIf(token));

        CreatureSiegeSnapshot.Publish(Make(token));
        Assert.IsTrue(CreatureSiegeSnapshot.ClearIf(token));
        Assert.IsFalse(CreatureSiegeSnapshot.ClearIf(token), "OnEndMission and OnRemoveBehavior both clear: the second is a no-op");
    }

    [TestMethod]
    public void ClearIf_ANullToken_LeavesTheSnapshotAlone()
    {
        var snapshot = Make();
        CreatureSiegeSnapshot.Publish(snapshot);

        Assert.IsFalse(CreatureSiegeSnapshot.ClearIf(null));
        Assert.AreSame(snapshot, CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void ClearIf_TheTokenIsComparedByReference_NotByEquals()
    {
        var token = new AlwaysEqual();
        CreatureSiegeSnapshot.Publish(Make(token));

        Assert.IsFalse(CreatureSiegeSnapshot.ClearIf(new AlwaysEqual()));
        Assert.IsNotNull(CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void Clear_RemovesWhateverIsPublished()
    {
        CreatureSiegeSnapshot.Publish(Make());

        CreatureSiegeSnapshot.Clear();

        Assert.IsNull(CreatureSiegeSnapshot.Current);
    }

    // --- what a snapshot holds ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void MissionToken_AndMultiplier_AreWhatWasGiven()
    {
        var token = new object();

        var snapshot = Make(token, multiplier: 3.5f);

        Assert.AreSame(token, snapshot.MissionToken);
        Assert.AreEqual(3.5f, snapshot.GateDamageMultiplier);
    }

    [TestMethod]
    public void Constructor_ANullToken_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new CreatureSiegeSnapshot(null!, new[] { true }, 2f, Array.Empty<object>()));
    }

    [DataTestMethod]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(0, false)]
    public void IsCreatureRace_InsideTheMask_ReadsTheFlag(int race, bool expected)
    {
        Assert.AreEqual(expected, Make().IsCreatureRace(race));
    }

    [DataTestMethod]
    [DataRow(3)]
    [DataRow(1000)]
    [DataRow(int.MaxValue)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void IsCreatureRace_OutsideTheMask_IsFalse(int race)
    {
        Assert.IsFalse(Make().IsCreatureRace(race));
    }

    [TestMethod]
    public void IsCreatureRace_AnEmptyMask_IsAlwaysFalse()
    {
        var snapshot = Make(mask: Array.Empty<bool>());

        Assert.IsFalse(snapshot.IsCreatureRace(0));
        Assert.IsFalse(snapshot.IsCreatureRace(1));
    }

    [TestMethod]
    public void TheMask_IsCopied_SoTheCallersArrayCannotChangeIt()
    {
        var mask = new[] { false, true };
        var snapshot = Make(mask: mask);

        mask[0] = true;
        mask[1] = false;

        Assert.IsFalse(snapshot.IsCreatureRace(0));
        Assert.IsTrue(snapshot.IsCreatureRace(1));
    }

    [TestMethod]
    public void IsGateComponent_TheComponentsGiven_AreRecognisedByReference()
    {
        var outer = new object();
        var inner = new object();
        var snapshot = Make(gates: new[] { outer, inner });

        Assert.IsTrue(snapshot.IsGateComponent(outer));
        Assert.IsTrue(snapshot.IsGateComponent(inner));
        Assert.IsFalse(snapshot.IsGateComponent(new object()));
    }

    [TestMethod]
    public void IsGateComponent_IsByReference_NotByEquals()
    {
        var snapshot = Make(gates: new object[] { new AlwaysEqual() });

        Assert.IsFalse(snapshot.IsGateComponent(new AlwaysEqual()));
    }

    [TestMethod]
    public void IsGateComponent_NullAndNoGates_AreFalse()
    {
        Assert.IsFalse(Make(gates: new object[] { new object() }).IsGateComponent(null));
        Assert.IsFalse(Make(gates: Array.Empty<object>()).IsGateComponent(new object()));
    }

    [TestMethod]
    public void TheGateComponents_AreCopied_AndNullEntriesDropped()
    {
        var outer = new object();
        var gates = new object?[] { outer, null };
        var snapshot = Make(gates: gates);

        gates[0] = new object();

        Assert.IsTrue(snapshot.IsGateComponent(outer));
        Assert.IsFalse(snapshot.IsGateComponent(null), "a null gate (no inner gate) is not a component");
    }

    // --- reading while the main thread publishes and clears ------------------------------------------------------------------

    [TestMethod]
    public void AReaderOnAnotherThread_NeverSeesATornSnapshot_WhileTheMainThreadPublishesAndClears()
    {
        var failures = 0;
        var stop = false;
        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                var snapshot = CreatureSiegeSnapshot.Current;
                if (snapshot == null) continue;

                // Every published snapshot carries a mask of exactly [false, true, true] and multiplier 2.
                if (!snapshot.IsCreatureRace(1) || !snapshot.IsCreatureRace(2) || snapshot.IsCreatureRace(0)
                    || snapshot.GateDamageMultiplier != 2f)
                    Interlocked.Increment(ref failures);
            }
        });

        for (var i = 0; i < 20000; i++)
        {
            var token = new object();
            CreatureSiegeSnapshot.Publish(Make(token));
            CreatureSiegeSnapshot.ClearIf(token);
        }

        Volatile.Write(ref stop, true);
        reader.Wait();
        Assert.AreEqual(0, failures);
    }

    // A token or a component whose Equals says yes to everything: reference identity must not be fooled by it.
    private sealed class AlwaysEqual
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 0;
    }
}
