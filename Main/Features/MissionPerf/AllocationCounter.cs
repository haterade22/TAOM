using System;
using System.Reflection;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// The CLR's per-thread allocation counter, <c>GC.GetAllocatedBytesForCurrentThread()</c>. The desktop
/// CLR the game runs on (4.0.30319.42000) has it, but the net472 reference assemblies TAOM compiles
/// against do not declare it, so it is bound once by reflection into a delegate; when it is absent
/// <see cref="Available"/> is false and every allocation field the profiler writes reads <c>na</c>.
///
/// The counter is per thread: called from the main thread it measures main-thread allocation only,
/// never what the agent tick allocates on the asynchronous AI thread or the worker pool.
/// </summary>
internal static class AllocationCounter
{
    private static readonly Func<long>? Reader = Bind(typeof(GC), "GetAllocatedBytesForCurrentThread");

    public static bool Available => Reader != null;

    /// <summary>Bytes allocated by the calling thread so far, or 0 when the counter is unavailable.</summary>
    public static long ReadOrZero() => Reader != null ? Reader() : 0L;

    /// <summary>A public static parameterless method returning <c>long</c>, as a delegate; null when
    /// the method is absent, has another shape, or cannot be bound.</summary>
    internal static Func<long>? Bind(Type owner, string methodName)
    {
        try
        {
            var method = owner.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(long))
                return null;
            return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
