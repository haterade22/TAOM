using System.Threading;

namespace TAOM.Features.Execution.Hooks;

/// <summary>
/// Victim/executor identity captured by <see cref="KillCharacterAction_ApplyInternal_Patch"/> at the
/// top of <c>KillCharacterAction.ApplyInternal</c>, before the engine can mutate it.
/// </summary>
/// <remarks>
/// This exists for two reasons. First, <c>TraitLevelingHelper.OnLordExecuted</c> takes no arguments,
/// so the honor-penalty patch has no other way to learn who killed whom. Second, and less obviously,
/// <c>ApplyInternal</c> destroys the victim's clan (nulling <c>Clan.Kingdom</c> via
/// <c>ChangeKingdomAction.ApplyByLeaveKingdomByClanDestruction</c>) BEFORE it fires
/// <c>OnHeroKilled</c>, which is what drives the relation pass. Re-deriving the victim's kingdom
/// during that pass therefore reads null for any executed lord who was the last adult of his clan.
///
/// <para><c>ApplyInternal</c> RE-ENTERS ITSELF, which is why ownership is tracked.</para>
/// Destroying that clan calls <c>KillCharacterAction.ApplyByRemove</c> for every other living hero
/// in it (<c>DestroyClanAction.cs:43</c>), and each of those runs the same Harmony prefix and
/// finalizer while the outer execution is still on the stack. A nested kill carries a non-execution
/// detail, so it never sets the snapshot, but an unconditional clear would still wipe it before the
/// outer call reached <c>OnHeroKilled</c> at <c>KillCharacterAction.cs:144</c>. Only the frame that
/// called <see cref="TrySet"/> may clear, which the patch threads through Harmony's <c>__state</c>.
/// </remarks>
public static class ExecutionContext
{
    private static readonly ThreadLocal<bool> Active = new ThreadLocal<bool>();
    private static readonly ThreadLocal<string> VictimKingdomId = new ThreadLocal<string>();
    private static readonly ThreadLocal<string> VictimCultureId = new ThreadLocal<string>();
    private static readonly ThreadLocal<string> ExecutorKingdomId = new ThreadLocal<string>();
    private static readonly ThreadLocal<string> ExecutorCultureId = new ThreadLocal<string>();

    /// <summary>
    /// Establishes the snapshot for this thread and reports whether the caller now owns it. A nested
    /// call finds a context already active, leaves it untouched, and gets <c>false</c>, so the outer
    /// execution's identity always wins.
    /// </summary>
    /// <returns><c>true</c> if this call established the context and must later clear it.</returns>
    public static bool TrySet(
        string victimKingdomId,
        string victimCultureId,
        string executorKingdomId,
        string executorCultureId)
    {
        if (Active.Value)
            return false;

        VictimKingdomId.Value = victimKingdomId;
        VictimCultureId.Value = victimCultureId;
        ExecutorKingdomId.Value = executorKingdomId;
        ExecutorCultureId.Value = executorCultureId;
        Active.Value = true;
        return true;
    }

    /// <summary>
    /// Ends the snapshot, but only for the frame that established it. Every other frame passing
    /// through the finalizer, including the nested kills clan destruction triggers, passes
    /// <c>false</c> and leaves the outer execution's snapshot alone.
    /// </summary>
    public static void ClearIfOwned(bool owned)
    {
        if (!owned)
            return;

        VictimKingdomId.Value = null;
        VictimCultureId.Value = null;
        ExecutorKingdomId.Value = null;
        ExecutorCultureId.Value = null;
        Active.Value = false;
    }

    /// <summary>
    /// True while an execution is in flight on this thread. A hero legitimately has no kingdom, so
    /// this is tracked with its own flag rather than inferred from a null id.
    /// </summary>
    public static bool HasContext => Active.Value;

    public static string GetVictimKingdomId() => VictimKingdomId.Value;
    public static string GetVictimCultureId() => VictimCultureId.Value;
    public static string GetExecutorKingdomId() => ExecutorKingdomId.Value;
    public static string GetExecutorCultureId() => ExecutorCultureId.Value;

    /// <summary>
    /// The victim as of the start of the kill, falling back to the live values when no execution is
    /// in flight. The fallback is the pre-confirm relation preview
    /// (<c>HeroExecutionSceneNotificationData</c>), which runs the same GameModel before anything has
    /// been destroyed. Keeping both paths on one model is what holds the preview and the applied
    /// result in agreement.
    /// </summary>
    public static ExecutionParticipant ResolveVictim(string liveKingdomId, string liveCultureId)
        => Active.Value
            ? new ExecutionParticipant(VictimKingdomId.Value, VictimCultureId.Value)
            : new ExecutionParticipant(liveKingdomId, liveCultureId);

    /// <summary>The executor as of the start of the kill; see <see cref="ResolveVictim"/>.</summary>
    public static ExecutionParticipant ResolveExecutor(string liveKingdomId, string liveCultureId)
        => Active.Value
            ? new ExecutionParticipant(ExecutorKingdomId.Value, ExecutorCultureId.Value)
            : new ExecutionParticipant(liveKingdomId, liveCultureId);
}
