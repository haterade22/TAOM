namespace TAOM.Features.CrashReport.Adapters;

// Defers ButterLib's ExceptionHandlerSubSystem on-demand when our crash UI takes over.
// All calls are reflection-based — ButterLib API has shifted between minor versions and
// we don't want a hard compile-time dependency on ExceptionHandler.dll, which is shipped
// alongside ButterLib as a sub-module DLL we don't reference directly.
public interface IButterLibExceptionHandlerAdapter
{
    // True if ButterLib's ExceptionHandlerSubSystem type is reachable in the current AppDomain.
    bool IsPresent { get; }

    // Best-effort: call ExceptionHandlerSubSystem.Instance.Disable(). No-op if not present.
    // Returns true if Disable was actually invoked (telemetry).
    bool TrySuspend();
}
