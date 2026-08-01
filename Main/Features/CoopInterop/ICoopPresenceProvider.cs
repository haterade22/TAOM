namespace TAOM.Features.CoopInterop;

/// <summary>
/// Test seam over the static <c>CoopPresence</c> in TAOM.Dependencies.
///
/// <c>CoopPresence</c> does file + reflection I/O from a static class, so services that need to
/// know "are we in a co-op session" cannot be unit-tested against it directly. Inject this
/// instead; substitute it in tests.
/// </summary>
public interface ICoopPresenceProvider
{
    /// <summary>
    /// True when a known co-op module is active. The list is not enumerated here on purpose — it
    /// lives in <c>CoopPresence.CompiledModuleDefaults</c> ∪ the shipped <c>coop-modules.txt</c>,
    /// and an inline copy drifted within a day of being written (BannerlordCoop was added to the
    /// real list and not to this comment).
    /// Process-constant for the session — see <c>CoopPresence</c> for why it cannot vary per
    /// campaign (TAOM's late patch batch is a one-shot and one of its transpilers is
    /// non-idempotent).
    /// </summary>
    bool IsCoopActive { get; }
}
