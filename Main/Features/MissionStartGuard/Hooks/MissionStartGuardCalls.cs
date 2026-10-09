// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System;
using TAOM.Features.MissionStartGuard.Models;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionStartGuard.Hooks;

/// <summary>
/// The helpers Patch103 swaps in for the engine's six start calls inside <c>Mission.AfterStart</c>. Each takes the
/// call's instance first and then its arguments (the stack the engine pushed), makes the call inside a try, and
/// catches only when <see cref="IMissionStartGuardService.ShouldSurvive"/> says so. The exception filter runs before
/// any frame unwinds, so with the guard off, with no service, or for <see cref="OutOfMemoryException"/> the engine's
/// exception reaches its old catcher untouched and with its original stack. A report that fails costs only the report.
/// Main thread only: <c>Mission.AfterStart</c> and its callbacks.
/// </summary>
internal static class MissionStartGuardCalls
{
    internal static IMissionStartGuardService? Service { get; private set; }

    internal static void Initialize(IMissionStartGuardService? service) => Service = service;

    internal static void OnBeforeMissionBehaviorInitialize(MBSubModuleBase module, Mission mission)
    {
        try { module.OnBeforeMissionBehaviorInitialize(mission); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.OnBeforeMissionBehaviorInitialize, module, ex); }
    }

    internal static void OnBehaviorInitialize(MissionBehavior behavior)
    {
        try { behavior.OnBehaviorInitialize(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.OnBehaviorInitialize, behavior, ex); }
    }

    internal static void OnMissionBehaviorInitialize(MBSubModuleBase module, Mission mission)
    {
        try { module.OnMissionBehaviorInitialize(mission); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.OnMissionBehaviorInitialize, module, ex); }
    }

    internal static void EarlyStart(MissionBehavior behavior)
    {
        try { behavior.EarlyStart(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.EarlyStart, behavior, ex); }
    }

    internal static void AfterStart(MissionBehavior behavior)
    {
        try { behavior.AfterStart(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.AfterStart, behavior, ex); }
    }

    internal static void AfterMissionStart(MissionObject missionObject)
    {
        try { missionObject.AfterMissionStart(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.AfterMissionStart, missionObject, ex); }
    }

    // A throw inside an exception filter is swallowed by the runtime as "not handled", so this is belt and braces.
    private static bool Survives(Exception ex)
    {
        try { return Service?.ShouldSurvive(ex) ?? false; }
        catch { return false; }
    }

    private static void Note(StartCall call, object? owner, Exception ex)
    {
        try
        {
            var type = owner?.GetType();
            Service?.Report(call, type?.FullName ?? type?.Name ?? "<null>", type == null ? "unknown" : AssemblyNameOf(type), ex);
        }
        catch
        {
            // The service never throws; this keeps even a broken one from replacing the exception we survived.
        }
    }

    private static string AssemblyNameOf(Type type)
    {
        try { return type.Assembly.GetName().Name ?? "unknown"; }
        catch { return "unknown"; }
    }
}
