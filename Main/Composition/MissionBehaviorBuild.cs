using System;

namespace TAOM.Composition;

/// <summary>
/// Builds one hand-wired mission behavior in isolation (#785). SubModule.AddTaomMissionBehaviors registers each TAOM
/// mission behavior through a factory: a throw while building one (a service that is not registered, a config that
/// fails to load) is logged with the behavior's type name and reported to the mission-start guard, and that behavior
/// is left out, so the registrations after it still run. The caller adds the result to the mission outside this call:
/// Mission.AddMissionBehavior lists the behavior before it calls OnCreated, so a throw there must still reach the
/// mission-start guard. The isolation does not read the "Survive Mission Start Failures" setting: a failed build is
/// always contained and reported. An OutOfMemoryException is never contained, as the guard never swallows one.
/// Engine-free and unit-tested (MissionBehaviorBuildTests).
/// </summary>
internal static class MissionBehaviorBuild
{
    /// <summary>
    /// What <paramref name="create"/> returns, or null when it throws (an OutOfMemoryException propagates). A throw
    /// is logged through <paramref name="logError"/> and handed to <paramref name="reportFailure"/> as (owner type,
    /// owner assembly, exception), the arguments IMissionStartGuardService.Report takes after the start call.
    /// </summary>
    internal static T? Isolated<T>(Func<T?> create, Action<string> logError, Action<string, string, Exception> reportFailure)
        where T : class
    {
        try
        {
            return create();
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            var type = typeof(T);
            try
            {
                logError($"[MissionWiring] {type.Name} was not added: building it threw, and the other TAOM mission behaviors are still added. {ex}");
            }
            catch
            {
                // swallow-ok: the log line must never be what breaks the mission wiring.
            }

            try
            {
                reportFailure(type.FullName ?? type.Name, type.Assembly.GetName().Name ?? "unknown", ex);
            }
            catch
            {
                // swallow-ok: the guard's report must never be what breaks the mission wiring.
            }

            return null;
        }
    }
}
