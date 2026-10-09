// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
namespace TAOM.Features.NameplateCull.Models;

/// <summary>
/// Everything the skip decision reads about one settlement nameplate, as plain values: the adapter fills one per plate
/// per frame on the stack, and <see cref="NameplateCullRule.MaySkip"/> decides from it. A mutable struct on purpose:
/// no allocation per plate, and an object initializer instead of a thirteen-argument constructor.
/// </summary>
internal struct NameplateFacts
{
    /// <summary>The plate's pushed <c>IsVisibleOnMap</c> (what the widget currently shows).</summary>
    public bool IsVisibleOnMap;

    /// <summary>The plate's private <c>_bindIsVisibleOnMap</c> (what the last update decided).</summary>
    public bool BindIsVisibleOnMap;

    /// <summary>The pushed <c>Position</c> is already the (-1000, -1000) a hidden plate is parked at, so the widget has been told.</summary>
    public bool ParkedOffScreen;

    /// <summary>The plate's <c>IsTracked</c> (pushed tracking, or a tutorial target).</summary>
    public bool IsTracked;

    /// <summary>The plate's pushed <c>IsInRange</c>.</summary>
    public bool IsInRange;

    /// <summary>The plate's pushed <c>IsTargetedByTutorial</c>.</summary>
    public bool IsTargetedByTutorial;

    /// <summary>The map camera's height (<c>z</c>) this frame.</summary>
    public float CameraZ;

    /// <summary>The distance from the settlement's world position to the camera this frame.</summary>
    public float DistanceToCamera;

    public bool IsTown;

    /// <summary>A town or a castle.</summary>
    public bool IsFortification;

    /// <summary>What the plate's next update would compute as its range: <c>IsVisible</c> for a hideout, <c>IsInspected</c> otherwise.</summary>
    public bool SettlementInRange;

    /// <summary>The settlement party's <c>IsVisualDirty</c> (its name is about to be re-read).</summary>
    public bool PartyVisualDirty;

    /// <summary>The campaign's visual tracker holds this settlement.</summary>
    public bool VisuallyTracked;
}
