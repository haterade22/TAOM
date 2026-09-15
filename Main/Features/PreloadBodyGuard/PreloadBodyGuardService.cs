using System;
using System.Collections.Generic;

namespace TAOM.Features.PreloadBodyGuard;

/// <summary>
/// The bounded drain behind <c>Patch90_PreloadBodyGuard</c>. Pure: the resolver, the clock and
/// the sleep are injected, and nothing here knows a <c>PhysicsShape</c> from a string.
///
/// <para>
/// Why a budget rather than a single pass: <c>PhysicsShape.GetFromResource(name, true)</c> returns
/// null both for "not loaded yet" and for "no such body". The physics preload is queued and
/// processed inside the same load (<c>PreloadHelper.PreloadMeshesAndPhysics</c>), and a healthy load
/// resolves every name on the first pass (the <c>[BattleLoad]</c> diagnostics record
/// <c>WaitingForRender waitedMs=0..1</c>), so a name still null after the budget is a data defect,
/// not a slow disk. The budget is generous on purpose: a false drop takes a real collision body
/// away from a weapon, a late drop costs a few seconds once.
/// </para>
/// </summary>
public sealed class PreloadBodyGuardService : IPreloadBodyGuardService
{
    public IReadOnlyList<string> DrainUnresolvable(
        ICollection<string> names,
        Func<string, bool> isResolved,
        Func<double> elapsedSeconds,
        Action sleepOnce,
        double budgetSeconds)
    {
        var dropped = new List<string>();
        if (names == null || names.Count == 0)
            return dropped;

        // Snapshot: the caller's set is mutated at the end, never while enumerating it.
        var pending = new List<string>(names);
        // Built once: a capturing lambda inside the loop would allocate a delegate per 1 ms pass,
        // about five thousand of them across the budget on the path this guard exists for.
        Predicate<string> resolved = name => Resolves(isResolved, name);
        while (true)
        {
            pending.RemoveAll(resolved);
            if (pending.Count == 0)
                return dropped;
            if (!(elapsedSeconds() < budgetSeconds))
                break;
            sleepOnce();
        }

        foreach (var name in pending)
        {
            names.Remove(name);
            dropped.Add(name);
        }
        return dropped;
    }

    /// <summary>A resolver that throws is a native lookup that failed; that name is unresolved
    /// this pass, and the guard keeps going so the load it protects still finishes.</summary>
    private static bool Resolves(Func<string, bool> isResolved, string name)
    {
        try
        {
            return isResolved(name);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
