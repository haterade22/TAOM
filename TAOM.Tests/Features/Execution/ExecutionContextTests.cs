using Microsoft.VisualStudio.TestTools.UnitTesting;
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

    [TestInitialize]
    [TestCleanup]
    public void ResetThreadLocalState() => ExecutionContext.ClearIfOwned(true);

    [TestMethod]
    public void TrySet_OnAFreshContext_TakesOwnership()
    {
        Assert.IsTrue(ExecutionContext.TrySet(VictimKingdom, VictimCulture, ExecutorKingdom, ExecutorCulture));
        Assert.IsTrue(ExecutionContext.HasContext);
    }

    [TestMethod]
    public void NestedKill_DoesNotTakeOwnership_AndDoesNotClearTheOuterSnapshot()
    {
        ExecutionContext.TrySet(VictimKingdom, VictimCulture, ExecutorKingdom, ExecutorCulture);

        // The nested ApplyInternal is a non-execution removal, so its prefix never calls TrySet and
        // its finalizer must not clear. Before the ownership gate this wiped the outer snapshot.
        ExecutionContext.ClearIfOwned(owned: false);

        Assert.IsTrue(ExecutionContext.HasContext, "A nested kill must not end the outer execution's snapshot");
        var victim = ExecutionContext.ResolveVictim("live_kingdom", "live_culture");
        Assert.AreEqual(VictimKingdom, victim.KingdomId);
        Assert.AreEqual(VictimCulture, victim.CultureId);
    }

    [TestMethod]
    public void NestedTrySet_DoesNotOverwriteTheOuterSnapshot()
    {
        ExecutionContext.TrySet(VictimKingdom, VictimCulture, ExecutorKingdom, ExecutorCulture);

        Assert.IsFalse(ExecutionContext.TrySet("isengard", "isengard", "vlandia", "vlandia"),
            "A nested execution must not claim ownership from the outer one");

        var victim = ExecutionContext.ResolveVictim(null, null);
        Assert.AreEqual(VictimKingdom, victim.KingdomId, "The outer snapshot must survive a nested TrySet");
    }

    [TestMethod]
    public void ClearIfOwned_ByTheOwningFrame_EndsTheSnapshot()
    {
        var owned = ExecutionContext.TrySet(VictimKingdom, VictimCulture, ExecutorKingdom, ExecutorCulture);

        ExecutionContext.ClearIfOwned(owned);

        Assert.IsFalse(ExecutionContext.HasContext);
    }

    [TestMethod]
    public void ResolveVictim_WithNoContext_UsesTheLiveValues()
    {
        // The pre-confirm preview path: no kill in flight, nothing destroyed yet.
        var victim = ExecutionContext.ResolveVictim("live_kingdom", "live_culture");

        Assert.AreEqual("live_kingdom", victim.KingdomId);
        Assert.AreEqual("live_culture", victim.CultureId);
    }

    [TestMethod]
    public void ResolveExecutor_WithContext_PrefersTheSnapshot()
    {
        ExecutionContext.TrySet(VictimKingdom, VictimCulture, ExecutorKingdom, ExecutorCulture);

        var executor = ExecutionContext.ResolveExecutor("live_kingdom", "live_culture");

        Assert.AreEqual(ExecutorKingdom, executor.KingdomId);
        Assert.AreEqual(ExecutorCulture, executor.CultureId);
    }

    [TestMethod]
    public void ResolveVictim_WithContextHoldingNoKingdom_StillPrefersTheSnapshot()
    {
        // A victim from a minor clan has no kingdom. The snapshot is still authoritative, which is
        // why the active flag is tracked separately instead of being inferred from a null id.
        ExecutionContext.TrySet(null, VictimCulture, ExecutorKingdom, ExecutorCulture);

        var victim = ExecutionContext.ResolveVictim("live_kingdom", "live_culture");

        Assert.IsNull(victim.KingdomId);
        Assert.AreEqual(VictimCulture, victim.CultureId);
    }
}
