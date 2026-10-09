// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using TAOM.Core.Validation;
using TAOM.Features.NameplateCull.Models;

namespace TAOM.Features.NameplateCull;

/// <summary>
/// The skip decision, from plain values (docs/features/nameplate-cull.md). A plate may be skipped for a frame only when
/// every one of its hidden-and-stays-hidden conditions holds, so a skipped plate is exactly one whose update would have
/// changed nothing the widget can show.
/// </summary>
internal static class NameplateCullRule
{
    // The distance part of SettlementNameplateVM.IsVisible (v1.5.4): above 400 only towns, above 200 towns and castles,
    // below that anything closer than the camera height plus 100.
    internal const float TownCameraHeight = 400f;
    internal const float FortificationCameraHeight = 200f;
    internal const float NearMargin = 100f;

    /// <summary>
    /// True when the game's own height-and-distance test could make the plate visible. False only when it provably
    /// cannot. A non-finite input answers true (cannot rule it out), so a bad camera or position leaves the plate to
    /// vanilla.
    /// </summary>
    internal static bool CanBeVisible(float cameraZ, float distanceToCamera, bool isTown, bool isFortification)
    {
        if (!FiniteFloatValidator.IsFinite(cameraZ) || !FiniteFloatValidator.IsFinite(distanceToCamera)) return true;
        if (cameraZ > TownCameraHeight) return isTown;
        if (cameraZ > FortificationCameraHeight) return isFortification;
        return distanceToCamera < cameraZ + NearMargin;
    }

    /// <summary>True when the plate may be skipped this frame: it is hidden, parked, untracked and out of range, and nothing the update reads has changed.</summary>
    internal static bool MaySkip(in NameplateFacts facts) =>
        !facts.IsVisibleOnMap
        && !facts.BindIsVisibleOnMap
        && facts.ParkedOffScreen
        && !facts.IsTracked
        && !facts.IsInRange
        && !facts.IsTargetedByTutorial
        && !CanBeVisible(facts.CameraZ, facts.DistanceToCamera, facts.IsTown, facts.IsFortification)
        && !facts.SettlementInRange
        && !facts.PartyVisualDirty
        && !facts.VisuallyTracked;
}
