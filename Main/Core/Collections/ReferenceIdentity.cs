using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TAOM.Core.Collections;

/// <summary>
/// Reference-identity equality comparer for any object key: <c>Equals</c> is <c>ReferenceEquals</c>,
/// <c>GetHashCode</c> is the object's identity hash, and neither reads the key's own overrides. A
/// map of engine objects cannot trust the default comparer when the key type's own
/// <c>Equals</c>/<c>GetHashCode</c> are composed from mutable or partially-initialised state:
/// <c>Formation.GetHashCode</c> is <c>Team.TeamIndex * 10 + FormationIndex</c>, which throws on a
/// team-less simulation copy (the order preview's <c>new Formation(null, -1)</c>,
/// <c>docs/reviews/lessons/adapters-taleworlds-api.md</c> 2026-09-26). <see cref="IEqualityComparer{T}"/>
/// is contravariant, so <see cref="Instance"/> (an <c>IEqualityComparer&lt;object&gt;</c>) also works
/// as the comparer for a <c>Dictionary&lt;TKey,...&gt;</c> or
/// <c>ConcurrentDictionary&lt;TKey,...&gt;</c> keyed on any reference type.
///
/// <c>Main/Adapters/AgentAdapterCache.cs</c> carries an identical private comparer for the same
/// reason.
/// </summary>
public sealed class ReferenceIdentity : IEqualityComparer<object>
{
    public static readonly ReferenceIdentity Instance = new();

    private ReferenceIdentity()
    {
    }

    bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
