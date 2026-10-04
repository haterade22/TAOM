namespace TAOM.Features.MissionPerf;

/// <summary>The five Patch98 brackets, in the order their missing-prefix counts and lines are reported.</summary>
internal enum ProbeBracket
{
    PreTick = 0,
    Wait = 1,
    OnTick = 2,
    Script = 3,
    Spawn = 4,
}

/// <summary>
/// One probe call's pairing state, carried from its prefix to its finalizer in Harmony's <c>__state</c>. Harmony
/// declares one such local per patch class in every call of the patched method and starts it at 0, so a finalizer
/// whose prefix did not run in this very call reads <see cref="PrefixMissing"/>: the prefix was stripped (PatchShield
/// does that, from inside the call that threw), or a prefix before it threw. Each prefix sets <see cref="Entered"/>
/// before any code that can throw (the OnPreTick prefix runs the frame boundary first, which catches every
/// exception itself); the finalizer that closes the call sets <see cref="Closed"/>, so a second run of the same
/// finalizer for the same call (Harmony reruns every finalizer when a later one throws on the normal path) records
/// and warns about nothing. A thread, a nesting depth or a shared count says nothing about which call a finalizer
/// belongs to; this does.
/// </summary>
public enum ProbeState : byte
{
    /// <summary>The default: this call's prefix did not run.</summary>
    PrefixMissing = 0,

    /// <summary>This call's prefix ran and its finalizer has not closed it yet.</summary>
    Entered = 1,

    /// <summary>This call's finalizer already closed it.</summary>
    Closed = 2,
}
