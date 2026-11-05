using System;
using System.Collections.Generic;

namespace TAOM.Features.PreloadBodyGuard;

/// <summary>
/// Drains the collision-body names that will never resolve out of a preload set, so
/// <c>PreloadHelper.WaitForMeshesToBeLoaded</c> can exit (#352, #599, #601).
/// </summary>
public interface IPreloadBodyGuardService
{
    /// <summary>
    /// Polls <paramref name="names"/> with <paramref name="isResolved"/> until every name resolves
    /// or <paramref name="budgetSeconds"/> of <paramref name="elapsedSeconds"/> has passed, sleeping
    /// once per failed pass via <paramref name="sleepOnce"/>. Names still unresolved at the end are
    /// removed from <paramref name="names"/> and returned, in the set's own order. A resolver that
    /// throws counts as unresolved for that pass and never escapes. A non-positive budget drops the
    /// unresolved names after a single pass.
    /// </summary>
    IReadOnlyList<string> DrainUnresolvable(
        ICollection<string> names,
        Func<string, bool> isResolved,
        Func<double> elapsedSeconds,
        Action sleepOnce,
        double budgetSeconds);
}
