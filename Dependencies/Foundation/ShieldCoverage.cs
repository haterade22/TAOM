using System.Collections.Generic;
using System.Reflection;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// PatchShield's per-method bookkeeping, kept out of the static Harmony-bound class so it can be
/// tested. Two sets, because a pass decides on far more methods than it attaches to: TAOM's own,
/// the excluded hot layers and SaveShield's targets are seen but never shielded, and one set made
/// the diag.log counts report every one of them as shielded. Not thread-safe: PatchShield holds
/// its lock around every call.
/// </summary>
internal sealed class ShieldCoverage
{
    private readonly HashSet<MethodBase> _seen = new();
    private readonly HashSet<MethodBase> _attached = new();

    /// <summary>Whether a pass already decided on this method (attached or deliberately skipped).</summary>
    public bool HasSeen(MethodBase method) => _seen.Contains(method);

    /// <summary>A method PatchShield decided never to shield (its own, an excluded layer, a SaveShield target).</summary>
    public void RecordSkipped(MethodBase method) => _seen.Add(method);

    /// <summary>A method that now carries PatchShield's finalizer.</summary>
    public void RecordAttached(MethodBase method)
    {
        _seen.Add(method);
        _attached.Add(method);
    }

    /// <summary>Methods examined and decided on, skipped ones included.</summary>
    public int SeenCount => _seen.Count;

    /// <summary>Methods carrying PatchShield's finalizer.</summary>
    public int AttachedCount => _attached.Count;
}
