using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// What the Long Defeat field knows about the lord's body without an engine (phase2-plan.md Slice 10): the body a remount fades is a planned removal matched by identity for any state the
/// engine reports; the callbacks that reach the lord's bodies are kept for the director to log on the main thread; and a hit counts as sally contact when the lord or his horse struck or was struck by an
/// attacker. Agents are plain objects here: the rules compare references only.
/// </summary>
[TestClass]
public class LongDefeatLordWatchTests
{
    private readonly object _lord = new();
    private readonly object _horse = new();
    private readonly object _orc = new();
    private readonly object _ally = new();

    private LongDefeatLordWatch NewWatch()
    {
        var watch = new LongDefeatLordWatch();
        watch.SetLord(_lord);
        return watch;
    }

    private bool IsAttacker(object? agent) => ReferenceEquals(agent, _orc);

    [TestMethod]
    public void APlannedBody_IsMatchedByIdentity_NeverByAnEqualLookingOne()
    {
        var watch = NewWatch();
        var old = new string('a', 3);
        var lookalike = new string('a', 3);

        watch.Plan(old);

        Assert.IsTrue(watch.IsPlanned(old));
        Assert.IsFalse(watch.IsPlanned(lookalike), "an agent that recycled the slot is a different object");
        Assert.IsFalse(watch.IsPlanned(_lord));
    }

    [DataTestMethod]
    [DataRow("Routed", false)]
    [DataRow("Deleted", false)]
    [DataRow("Killed", true)]
    [DataRow("Unconscious", true)]
    public void APlannedBody_IsRecognisedForAnyStateTheEngineReports_AndTheLossIsKept(string state, bool loss)
    {
        var watch = NewWatch();
        var old = new object();
        watch.Plan(old);

        watch.Note(old, "removed", state, loss, false);

        var seen = watch.Take().Single();
        Assert.IsTrue(seen.Planned);
        Assert.AreEqual(state, seen.State);
        Assert.AreEqual(loss, seen.Loss);
        Assert.AreEqual("removed", seen.Callback);
    }

    [TestMethod]
    public void OnlyTheLordsBodies_AndTheMainAgent_AreNoted()
    {
        var watch = NewWatch();
        var old = new object();
        watch.Plan(old);

        watch.Note(_orc, "removed", "Killed", true, false);
        watch.Note(_lord, "removed", "Killed", true, false);
        watch.Note(_orc, "deleted", "Deleted", false, true);
        watch.Note(old, "deleted", "Deleted", false, false);

        var seen = watch.Take();
        Assert.AreEqual(3, seen.Count);
        Assert.IsFalse(seen[0].Planned, "the lord's own body is noted but is not a planned removal");
        Assert.IsFalse(seen[1].Planned, "the main agent is noted (OnAgentDeleted logs the main body)");
        Assert.IsTrue(seen[2].Planned);
    }

    [TestMethod]
    public void Take_DrainsOnce_InTheOrderTheCallbacksCame()
    {
        var watch = NewWatch();
        watch.Note(_lord, "removed", "Routed", false, false);
        watch.Note(_lord, "deleted", "Deleted", false, false);

        Assert.AreEqual("removed,deleted", string.Join(",", watch.Take().Select(e => e.Callback)));
        Assert.AreEqual(0, watch.Take().Count);
    }

    [TestMethod]
    public void ARemount_KeepsTheOldBodyPlanned_WhileTheNewOneIsTheLord()
    {
        var watch = NewWatch();
        var fresh = new object();
        watch.Plan(_lord);

        watch.SetLord(fresh);

        Assert.IsTrue(watch.IsPlanned(_lord), "the old body's removal can arrive after the new body has spawned");
        Assert.AreSame(fresh, watch.Lord);
        Assert.IsFalse(watch.IsPlanned(fresh));
    }

    [TestMethod]
    public void Clear_ForgetsTheLord_ThePlannedBody_AndTheEvents()
    {
        var watch = NewWatch();
        watch.Plan(_lord);
        watch.Note(_lord, "removed", "Routed", false, false);

        watch.Clear();

        Assert.IsNull(watch.Lord);
        Assert.IsFalse(watch.IsPlanned(_lord));
        Assert.AreEqual(0, watch.Take().Count);
    }

    // ---- sally contact ----

    private bool Contact(object? affected, object? affector, float health, out LdContact contact) =>
        NewWatch().TryContact(affected, affector, _horse, health, IsAttacker, out contact);

    [TestMethod]
    public void AHitByTheLord_OnAnAttacker_IsContactGiven()
    {
        Assert.IsTrue(Contact(_orc, _lord, 40f, out var contact));

        Assert.AreEqual(LdContactKind.Given, contact.Kind);
        Assert.IsFalse(contact.Kill);
    }

    [TestMethod]
    public void AHitByTheHorse_OnAnAttacker_CountsAsTheLordsOwn()
    {
        Assert.IsTrue(Contact(_orc, _horse, 40f, out var contact));

        Assert.AreEqual(LdContactKind.Given, contact.Kind);
    }

    [TestMethod]
    public void ABlowThatLeavesTheVictimUnderOneHealth_IsAKill()
    {
        Assert.IsTrue(Contact(_orc, _lord, 0f, out var contact));

        Assert.IsTrue(contact.Kill);
    }

    [TestMethod]
    public void AHitByAnAttacker_OnTheLord_IsTaken()
    {
        Assert.IsTrue(Contact(_lord, _orc, 40f, out var contact));

        Assert.AreEqual(LdContactKind.Taken, contact.Kind);
        Assert.IsFalse(contact.Kill, "a kill is the lord's blow; his own death is counted elsewhere");
    }

    [TestMethod]
    public void AHitByAnAttacker_OnTheHorse_IsAMountHitInItsOwnKind()
    {
        Assert.IsTrue(Contact(_horse, _orc, 40f, out var contact));

        Assert.AreEqual(LdContactKind.MountTaken, contact.Kind);
    }

    [TestMethod]
    public void AHitBetweenStrangers_OnTheLordsSide_OrByNoOne_IsNotContact()
    {
        Assert.IsFalse(Contact(_orc, _ally, 40f, out _), "neither is the lord or his horse");
        Assert.IsFalse(Contact(_lord, _ally, 40f, out _), "a friend's blow is not an attacker's");
        Assert.IsFalse(Contact(_lord, null, 40f, out _), "no affector");
        Assert.IsFalse(Contact(null, _lord, 40f, out _), "no victim");
        Assert.IsFalse(Contact(_lord, _lord, 40f, out _), "the lord's own blow on himself");
    }

    [TestMethod]
    public void WithNoLord_NothingIsContact()
    {
        var watch = new LongDefeatLordWatch();

        Assert.IsFalse(watch.TryContact(_orc, _lord, _horse, 40f, IsAttacker, out _));
    }

    [TestMethod]
    public void ARidersHorseThatIsNull_DoesNotMakeEveryHitWithANullAffectorContact()
    {
        var watch = NewWatch();

        Assert.IsFalse(watch.TryContact(_orc, null, null, 40f, IsAttacker, out _));
        Assert.IsFalse(watch.TryContact(null, _orc, null, 40f, IsAttacker, out _));
    }
}
