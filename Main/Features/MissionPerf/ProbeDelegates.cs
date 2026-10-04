using System;
using System.Reflection;

namespace TAOM.Features.MissionPerf;

/// <summary>Open-instance delegates for methods TAOM cannot call directly (a protected internal virtual in
/// another assembly). Bound once; the delegate performs a virtual call, so an override runs
/// (<c>OpenDelegateDispatchTests</c>).</summary>
internal static class ProbeDelegates
{
    /// <summary>An <c>Action&lt;T, float&gt;</c> over <paramref name="method"/>, or null when the method is
    /// null or does not bind.</summary>
    internal static Action<T, float>? BindOpenInstance<T>(MethodInfo? method)
    {
        if (method == null)
            return null;
        try
        {
            return (Action<T, float>)Delegate.CreateDelegate(typeof(Action<T, float>), method);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
