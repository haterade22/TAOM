using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrollBruteForce;
using TAOM.Features.TrollBruteForce.Hooks;

namespace TAOM.Tests.Features.TrollBruteForce;

/// <summary>
/// The store is keyed by <c>object</c> with a reference comparer and scopes a team-less simulation copy to its
/// real formation via a <c>[ThreadStatic]</c> <see cref="TrollFormationSpacingStore.LayingOut"/>. Plain
/// <c>object</c> instances stand in for <c>Formation</c> here: the store never calls anything on its keys.
/// </summary>
[TestClass]
public class TrollFormationSpacingStoreTests
{
    [TestInitialize]
    public void Setup() => TrollFormationSpacingStore.Clear();

    [TestCleanup]
    public void Cleanup() => TrollFormationSpacingStore.LayingOut = null;

    // ---- Set --------------------------------------------------------------------------------

    [TestMethod]
    public void Set_FirstWrite_ReturnsTrueAndStoresTheValue()
    {
        var key = new object();

        bool changed = TrollFormationSpacingStore.Set(key, 1.6f);

        Assert.IsTrue(changed);
        Assert.IsTrue(TrollFormationSpacingStore.TryGet(key, isSimulationCopy: false, out float diameter));
        Assert.AreEqual(1.6f, diameter);
    }

    [TestMethod]
    public void Set_SameValueAgain_ReturnsFalseAndLeavesTheValueUnchanged()
    {
        var key = new object();
        TrollFormationSpacingStore.Set(key, 1.6f);

        bool changed = TrollFormationSpacingStore.Set(key, 1.6f);

        Assert.IsFalse(changed);
        TrollFormationSpacingStore.TryGet(key, isSimulationCopy: false, out float diameter);
        Assert.AreEqual(1.6f, diameter);
    }

    [TestMethod]
    public void Set_NewValue_ReturnsTrueAndOverwrites()
    {
        var key = new object();
        TrollFormationSpacingStore.Set(key, 1.6f);

        bool changed = TrollFormationSpacingStore.Set(key, 2.4f);

        Assert.IsTrue(changed);
        TrollFormationSpacingStore.TryGet(key, isSimulationCopy: false, out float diameter);
        Assert.AreEqual(2.4f, diameter);
    }

    [TestMethod]
    public void Set_NullForAStoredKey_RemovesItAndReturnsTrue()
    {
        var key = new object();
        TrollFormationSpacingStore.Set(key, 1.6f);

        bool changed = TrollFormationSpacingStore.Set(key, null);

        Assert.IsTrue(changed);
        Assert.IsFalse(TrollFormationSpacingStore.TryGet(key, isSimulationCopy: false, out _));
    }

    [TestMethod]
    public void Set_NullOnAnAbsentKey_ReturnsFalse()
    {
        bool changed = TrollFormationSpacingStore.Set(new object(), null);

        Assert.IsFalse(changed);
    }

    [TestMethod]
    public void Set_NullForOneOfTwoStoredKeys_TheOtherIsStillFound()
    {
        // The tracker's gone-pass removes one emptied troll formation while another still lives: the removal
        // must re-derive the fast-path flag from what is left, not switch it off.
        var emptied = new object();
        var living = new object();
        TrollFormationSpacingStore.Set(emptied, 1.6f);
        TrollFormationSpacingStore.Set(living, 2.4f);

        TrollFormationSpacingStore.Set(emptied, null);

        Assert.IsTrue(TrollFormationSpacingStore.TryGet(living, isSimulationCopy: false, out float diameter));
        Assert.AreEqual(2.4f, diameter);
    }

    [TestMethod]
    public void Set_KeyWhoseGetHashCodeThrows_StoresAndFindsIt()
    {
        // A team-less Formation throws from GetHashCode; the store's reference comparer must never ask it.
        var key = new ThrowingKey();
        Assert.ThrowsException<InvalidOperationException>(() => key.GetHashCode(), "the key must be hostile");

        TrollFormationSpacingStore.Set(key, 2.7f);

        Assert.IsTrue(TrollFormationSpacingStore.TryGet(key, isSimulationCopy: false, out float diameter));
        Assert.AreEqual(2.7f, diameter);
    }

    private sealed class ThrowingKey
    {
        public override int GetHashCode() => throw new InvalidOperationException("GetHashCode read");

        public override bool Equals(object? obj) => throw new InvalidOperationException("Equals read");
    }

    // ---- TryGet -------------------------------------------------------------------------------

    [TestMethod]
    public void TryGet_EmptyStore_ReturnsFalse()
    {
        bool found = TrollFormationSpacingStore.TryGet(new object(), isSimulationCopy: false, out float diameter);

        Assert.IsFalse(found);
        Assert.AreEqual(0f, diameter);
    }

    [TestMethod]
    public void TryGet_SimulationCopy_InsideLayingOutScope_BorrowsTheRealFormationsWidth()
    {
        var real = new object();
        var copy = new object();
        TrollFormationSpacingStore.Set(real, 2.7f);
        TrollFormationSpacingStore.LayingOut = real;

        bool found = TrollFormationSpacingStore.TryGet(copy, isSimulationCopy: true, out float diameter);

        Assert.IsTrue(found);
        Assert.AreEqual(2.7f, diameter);
    }

    [TestMethod]
    public void TryGet_SimulationCopy_OutsideAnyScope_ReturnsFalse()
    {
        // _any must be true (a real troll formation is stored) so the fast path can't mask the scope check.
        var real = new object();
        TrollFormationSpacingStore.Set(real, 2.7f);

        bool found = TrollFormationSpacingStore.TryGet(new object(), isSimulationCopy: true, out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryGet_TeamOwnedUnknownKey_InsideAScope_ReturnsFalse()
    {
        // isSimulationCopy: false must never borrow LayingOut's width, even inside an active scope.
        var real = new object();
        var other = new object();
        TrollFormationSpacingStore.Set(real, 2.7f);
        TrollFormationSpacingStore.LayingOut = real;

        bool found = TrollFormationSpacingStore.TryGet(other, isSimulationCopy: false, out _);

        Assert.IsFalse(found);
    }

    // ---- LayingOut scope ------------------------------------------------------------------------

    [TestMethod]
    public void SimulationFinalizer_UnwindingNestedLayoutCalls_RestoresEachOuterScopeInTurn()
    {
        // Patch92's prefix hands the finalizer the scope it replaced; the finalizer runs on every exit.
        var outer = new object();
        var inner = new object();
        TrollFormationSpacingStore.LayingOut = inner;   // the nested call's prefix named the inner formation

        Patch92_TrollFormationSpacingSimulation.Finalizer(__state: outer);
        Assert.AreSame(outer, TrollFormationSpacingStore.LayingOut, "the nested call's exit");

        Patch92_TrollFormationSpacingSimulation.Finalizer(__state: null);
        Assert.IsNull(TrollFormationSpacingStore.LayingOut, "the outermost call's exit");
    }

    [TestMethod]
    public void LayingOut_SetOnAnotherThread_IsInvisibleOnThisThread()
    {
        var real = new object();
        // A dedicated thread, not the pool: Task.Wait can inline the task onto this thread.
        var other = new Thread(() => TrollFormationSpacingStore.LayingOut = real);

        other.Start();
        other.Join();

        Assert.IsNull(TrollFormationSpacingStore.LayingOut);
    }

    // ---- Clear ----------------------------------------------------------------------------------

    [TestMethod]
    public void Clear_StoreHoldingAWidth_ForgetsItAndReArmsSet()
    {
        var key = new object();
        TrollFormationSpacingStore.Set(key, 1.6f);

        TrollFormationSpacingStore.Clear();

        Assert.IsFalse(TrollFormationSpacingStore.TryGet(key, isSimulationCopy: false, out _));
        // The fast path must re-arm: a Set after Clear has to work, not stay latched off.
        Assert.IsTrue(TrollFormationSpacingStore.Set(key, 1.6f));
    }
}
