using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace.Diagnostics;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Pins the per-tableau identity keys behind the #389 render census.
///
/// This exists because deep review 2026-08-06 found three defects in the previous key derivation, all
/// in code that had no test: it rebuilt the key string on every rendered frame (garbage on a hot
/// path), it would have pinned engine objects if cached in a plain dictionary, and it keyed on
/// `RuntimeHelpers.GetHashCode`, which is reused after GC and could silently suppress a genuinely new
/// tableau's report. The session keys on `object`, so these are drivable with plain objects.
/// </summary>
[TestClass]
public class TableauCensusSessionTests
{
    private const string Uruk = "urukhai_fighter";
    private const string Orc = "isengard_orc_ravager";

    /// <summary>
    /// The steady-state guarantee: the same tableau showing the same character must hand back the
    /// SAME string instance, not an equal one. Reference equality is the only assertion that proves
    /// no per-frame allocation — `AreEqual` would pass on a freshly built duplicate.
    /// </summary>
    [TestMethod]
    public void KeyFor_SameTableauAndCharacter_ReturnsTheSameStringInstance()
    {
        var tableau = new object();

        string first = TableauCensusSession.KeyFor(tableau, Uruk);
        string second = TableauCensusSession.KeyFor(tableau, Uruk);
        string third = TableauCensusSession.KeyFor(tableau, Uruk);

        Assert.IsNotNull(first);
        Assert.IsTrue(ReferenceEquals(first, second), "key was rebuilt on the second call — that is a per-frame allocation");
        Assert.IsTrue(ReferenceEquals(second, third), "key was rebuilt on the third call");
    }

    [TestMethod]
    public void KeyFor_CharacterChangesOnSameTableau_ReturnsADifferentKey()
    {
        // One tableau shows many troops as the user walks a troop tree; each deserves its own census.
        var tableau = new object();

        string urukKey = TableauCensusSession.KeyFor(tableau, Uruk);
        string orcKey = TableauCensusSession.KeyFor(tableau, Orc);

        Assert.AreNotEqual(urukKey, orcKey);
        StringAssert.Contains(urukKey, Uruk);
        StringAssert.Contains(orcKey, Orc);
    }

    [TestMethod]
    public void KeyFor_ReturningToAPreviousCharacter_RebuildsRatherThanReusingStaleKey()
    {
        var tableau = new object();

        string first = TableauCensusSession.KeyFor(tableau, Uruk);
        TableauCensusSession.KeyFor(tableau, Orc);
        string back = TableauCensusSession.KeyFor(tableau, Uruk);

        // Same logical key, and correctness does not depend on it being the same instance.
        Assert.AreEqual(first, back);
    }

    /// <summary>
    /// The identity guarantee. Two distinct tableaus showing the SAME troop must never share a key —
    /// otherwise the second one is treated as already-reported and its census is silently dropped.
    /// A hash-based key could collide here after GC recycled a hash; a monotonic serial cannot.
    /// </summary>
    [TestMethod]
    public void KeyFor_DifferentTableausSameCharacter_ReturnsDistinctKeys()
    {
        var a = new object();
        var b = new object();

        string keyA = TableauCensusSession.KeyFor(a, Uruk);
        string keyB = TableauCensusSession.KeyFor(b, Uruk);

        Assert.AreNotEqual(keyA, keyB, "two live tableaus showing the same troop collapsed to one key");
    }

    [TestMethod]
    public void KeyFor_NullTableau_ReturnsNull()
    {
        Assert.IsNull(TableauCensusSession.KeyFor(null, Uruk));
    }

    [TestMethod]
    public void KeyFor_NullOrWhitespaceCharacterId_UsesAStablePlaceholder()
    {
        var tableau = new object();

        string fromNull = TableauCensusSession.KeyFor(tableau, null);
        string fromBlank = TableauCensusSession.KeyFor(tableau, "   ");

        Assert.IsNotNull(fromNull);
        Assert.AreEqual(fromNull, fromBlank, "null and blank ids must collapse to the same placeholder key");
        StringAssert.Contains(fromNull, "no-char-id");
    }

    [TestMethod]
    public void Forget_NullTableau_DoesNotThrow()
    {
        TableauCensusSession.Forget(null, Uruk);
    }

    /// <summary>
    /// Teardown must actually release the tracked slot — without this the tracker fills with entries
    /// for tableaus that no longer exist and the instrument goes quiet mid-session.
    /// </summary>
    [TestMethod]
    public void Forget_AfterObserving_ReleasesTheTrackedSlot()
    {
        var tableau = new object();
        string key = TableauCensusSession.KeyFor(tableau, "forget_probe_troop");

        TableauCensusSession.Observe(key, agentVisualLoadingCounter: 1, mountVisualLoadingCounter: 0);
        int whileTracked = TableauCensusSession.TrackedCount;

        TableauCensusSession.Forget(tableau, "forget_probe_troop");

        Assert.IsTrue(whileTracked >= 1, "observation did not register a tracked entry");
        Assert.AreEqual(whileTracked - 1, TableauCensusSession.TrackedCount, "Forget did not release the slot");
    }
}
