using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;
using TAOM.Features.Execution.Hooks;

namespace TAOM.Tests.Features.Execution;

/// <summary>
/// Pins the nesting contract of the thread-local execution snapshot.
/// </summary>
/// <remarks>
/// `KillCharacterAction.ApplyInternal` re-enters itself: destroying the victim's clan calls
/// `KillCharacterAction.ApplyByRemove` for every other living hero in that clan
/// (`DestroyClanAction.cs:43`), and the clan is destroyed at `KillCharacterAction.cs:137`, before
/// `OnHeroKilled` fires at line 144. The Harmony finalizer therefore runs for those nested kills
/// while the outer execution is still on the stack. If it cleared unconditionally it would wipe the
/// snapshot the relation pass is about to read, which is the whole reason the snapshot exists (#556).
/// </remarks>
[TestClass]
public class ExecutionContextTests
{
    private const string VictimKingdom = "empire_s";
    private const string VictimCulture = "mordor";
    private const string ExecutorKingdom = "empire_w";
    private const string ExecutorCulture = "gondor";
    private static readonly SideIds Victim = new(VictimKingdom, VictimCulture);
    private static readonly SideIds Executor = new(ExecutorKingdom, ExecutorCulture);
    private static readonly SideIds Live = new("live_kingdom", "live_culture");

    [TestInitialize]
    [TestCleanup]
    public void ResetThreadLocalState() => ExecutionContext.ClearIfOwned(true);

    [TestMethod]
    public void TrySet_OnAFreshContext_TakesOwnership()
    {
        Assert.IsTrue(ExecutionContext.TrySet(Victim, Executor));
        Assert.IsTrue(ExecutionContext.HasContext);
    }

    [TestMethod]
    public void NestedKill_DoesNotTakeOwnership_AndDoesNotClearTheOuterSnapshot()
    {
        ExecutionContext.TrySet(Victim, Executor);

        // The nested ApplyInternal is a non-execution removal, so its prefix never calls TrySet and
        // its finalizer must not clear. Before the ownership gate this wiped the outer snapshot.
        ExecutionContext.ClearIfOwned(owned: false);

        Assert.IsTrue(ExecutionContext.HasContext, "A nested kill must not end the outer execution's snapshot");
        var victim = ExecutionContext.ResolveVictim(Live);
        Assert.AreEqual(VictimKingdom, victim.FactionId);
        Assert.AreEqual(VictimCulture, victim.CultureId);
    }

    [TestMethod]
    public void NestedTrySet_DoesNotOverwriteTheOuterSnapshot()
    {
        ExecutionContext.TrySet(Victim, Executor);

        Assert.IsFalse(ExecutionContext.TrySet(new SideIds("isengard", "isengard"), new SideIds("vlandia", "vlandia")),
            "A nested execution must not claim ownership from the outer one");

        var victim = ExecutionContext.ResolveVictim(SideIds.None);
        Assert.AreEqual(VictimKingdom, victim.FactionId, "The outer snapshot must survive a nested TrySet");
    }

    [TestMethod]
    public void ClearIfOwned_ByTheOwningFrame_EndsTheSnapshot()
    {
        var owned = ExecutionContext.TrySet(Victim, Executor);

        ExecutionContext.ClearIfOwned(owned);

        Assert.IsFalse(ExecutionContext.HasContext);
    }

    [TestMethod]
    public void ResolveVictim_WithNoContext_UsesTheLiveValues()
    {
        // The pre-confirm preview path: no kill in flight, nothing destroyed yet.
        var victim = ExecutionContext.ResolveVictim(Live);

        Assert.AreEqual("live_kingdom", victim.FactionId);
        Assert.AreEqual("live_culture", victim.CultureId);
    }

    [TestMethod]
    public void ResolveExecutor_WithContext_PrefersTheSnapshot()
    {
        ExecutionContext.TrySet(Victim, Executor);

        var executor = ExecutionContext.ResolveExecutor(Live);

        Assert.AreEqual(ExecutorKingdom, executor.FactionId);
        Assert.AreEqual(ExecutorCulture, executor.CultureId);
    }

    [TestMethod]
    public void ResolveVictim_WithContextHoldingNoKingdom_StillPrefersTheSnapshot()
    {
        // A victim from a minor clan has no kingdom. The snapshot is still authoritative, which is
        // why the active flag is tracked separately instead of being inferred from a null id.
        ExecutionContext.TrySet(new SideIds(string.Empty, VictimCulture), Executor);

        var victim = ExecutionContext.ResolveVictim(Live);

        Assert.AreEqual(string.Empty, victim.FactionId);
        Assert.AreEqual(VictimCulture, victim.CultureId);
    }

    [TestMethod]
    public void GetVictimAndGetExecutor_WithNoContext_AreNone()
    {
        Assert.AreSame(SideIds.None, ExecutionContext.GetVictim());
        Assert.AreSame(SideIds.None, ExecutionContext.GetExecutor());
    }
}
