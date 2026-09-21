namespace TAOM.Features.Elephant;

/// <summary>
/// When a howdah seat may move its archer, and how far apart two crew frames must stand (#627, 2026-09-20).
/// Pure: no engine types, so both rules are tested rather than trusted.
///
/// Both come from the same measured fact. A seated archer settles about 0.10 m from its frame, and the seat used to
/// teleport it back EVERY frame. The engine reads position deltas as real movement, so each archer reported 30 m/s
/// while its elephant stood still and its own legs were stopped, and no agent in this engine finishes a bow draw at
/// that speed: the draw stalled at 85 percent of its action and re-nocked about once a second, all battle, at every
/// range. Leaving a resting archer alone dropped that to 0.00 m/s standing and the elephant's own 5 m/s walking, the
/// draw completed (progress 1.00, one restart in a whole battle) and the crew finally shot.
///
/// The spacing rule is the same failure by another route: two 0.37 m body capsules closer than 0.74 m overlap, the
/// engine shoves them apart every frame, and the seat teleports them back, which recreates exactly the velocity that
/// stops them shooting. It is a PHYSICAL limit, not a visual one: the howdah's walls carry no collision, so a capsule
/// may overlap the rim, but two archers may not crowd. It is what decides how many archers a platform can hold,
/// here and on the mumakil's decks.
/// </summary>
internal static class HowdahSeatMotion
{
    /// <summary>Human body capsule radius, Native/ModuleData/monsters.xml.</summary>
    public const float HumanCapsuleRadius = 0.37f;

    /// <summary>The closest two crew frames may stand without their occupants shoving each other.</summary>
    public const float MinimumFrameSeparation = 2f * HumanCapsuleRadius;

    /// <summary>
    /// True when the archer has drifted far enough that the seat should place it back on its frame. A non-finite
    /// distance (a native position that came back NaN) answers FALSE: never teleport an agent to a position we
    /// cannot reason about, and never let a NaN slip through a bare greater-than (`.claude/rules/csharp-architecture.md`).
    /// </summary>
    public static bool ShouldCorrect(float distanceSquared, float deadbandMetres)
    {
        if (float.IsNaN(distanceSquared) || float.IsInfinity(distanceSquared)) return false;
        if (float.IsNaN(deadbandMetres) || float.IsInfinity(deadbandMetres) || deadbandMetres < 0f) return false;
        return distanceSquared > deadbandMetres * deadbandMetres;
    }

    /// <summary>
    /// True when a seat position is a number the engine can be handed. The seat frame comes from the elephant's own
    /// position through GameEntity.SetFrame, so it is engine-sourced and validated by nobody: both consumers (the
    /// teleport and the scripted position) pass it straight to native. NaN in either would be a native write we
    /// cannot reason about (`.claude/rules/csharp-architecture.md`, the engine-float gate).
    /// </summary>
    public static bool IsPlaceable(float x, float y, float z) =>
        !float.IsNaN(x) && !float.IsInfinity(x) &&
        !float.IsNaN(y) && !float.IsInfinity(y) &&
        !float.IsNaN(z) && !float.IsInfinity(z);

    /// <summary>True when two crew frames are far enough apart that their occupants will not shove each other.</summary>
    public static bool FramesAreClear(float separationMetres) =>
        !float.IsNaN(separationMetres) && separationMetres >= MinimumFrameSeparation;
}
