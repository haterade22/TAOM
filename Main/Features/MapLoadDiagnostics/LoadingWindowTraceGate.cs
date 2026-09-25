namespace TAOM.Features.MapLoadDiagnostics;

/// <summary>
/// Decides whether a call to <c>LoadingWindow.DisableGlobalLoadingWindow</c> actually lowered the
/// window. The engine calls it every frame from several screens' frame ticks and, whenever a
/// loading-window manager exists, clears <c>IsLoadingWindowActive</c> whether or not it was up (with
/// no manager, or when another patch skips the original, the flag stays up). Only the before and
/// after values together can tell a real lower from a no-op. Pure, so the patch stays a thin
/// boundary (ADR-002).
/// </summary>
public static class LoadingWindowTraceGate
{
    public static bool IsRealLower(bool wasActive, bool isActiveNow) => wasActive && !isActiveNow;
}
