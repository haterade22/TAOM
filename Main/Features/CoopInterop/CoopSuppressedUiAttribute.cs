using System;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Marks a UIExtenderEx extension type (a <c>[PrefabExtension]</c> or <c>[ViewModelMixin]</c>) that
/// must NOT be registered when a co-op module is active.
///
/// Use this for UI that presents a control the co-op host has taken ownership of. The canonical
/// case is <c>TAOM.Features.TimeAcceleration.UI</c>: BannerlordTogether prefixes the
/// <c>Campaign.TimeControlMode</c> setter and rewrites the assigned value outright whenever a
/// co-op session is active, so TAOM's extra fast-forward button still renders and still responds
/// to clicks while doing nothing at all. A control that lies is worse than no control.
///
/// Registration is a one-shot at <c>OnSubModuleLoad</c> — a runtime check inside the mixin cannot
/// remove an already-injected widget, which is why this is a registration-time filter and not a
/// per-frame gate.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CoopSuppressedUiAttribute : Attribute
{
    /// <param name="reason">Why this UI is suppressed. Surfaced in the startup log.</param>
    public CoopSuppressedUiAttribute(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
