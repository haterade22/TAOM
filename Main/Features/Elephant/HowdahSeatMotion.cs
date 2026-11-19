namespace TAOM.Features.Elephant;

/// <summary>
/// When a crew seat may place its archer by teleport, and how far apart two crew frames must stand (#627, #1482).
/// Pure: no engine types, so both rules are tested rather than trusted.
///
/// A teleport is the last resort. A seated archer settles about 0.10 m from its frame, and the first seat teleported
/// it back EVERY frame: the engine reads position deltas as real movement, so each archer reported 30 m/s while its
/// elephant stood still, and no bow draw finished (#627). A deadband around the settle helped a standing crew, but
/// the archers walk off their seats by themselves, so the teleports came back several times a second and every one
/// reset the reload (#1482). The seats now hold the archer with the engine's position lock and attachment, and
/// <see cref="SeatRescueMetres"/> is the only distance at which one still teleports.
///
/// The spacing rule: two 0.37 m body capsules closer than 0.74 m overlap, and the engine shoves them apart every
/// frame. It is a PHYSICAL limit, not a visual one: the howdah's walls carry no collision, so a capsule may overlap
/// the rim, but two archers may not crowd. It is what decides how many archers a platform can hold, here and on the
/// mumakil's decks.
/// </summary>
internal static class HowdahSeatMotion
{
    /// <summary>Human body capsule radius, Native/ModuleData/monsters.xml.</summary>
    public const float HumanCapsuleRadius = 0.37f;

    /// <summary>The closest two crew frames may stand without their occupants shoving each other.</summary>
    public const float MinimumFrameSeparation = 2f * HumanCapsuleRadius;

    /// <summary>
    /// True when the archer is further than the band from its seat, so the seat should place it back on its frame. A
    /// non-finite distance (a native position that came back NaN) answers FALSE: never teleport an agent to a
    /// position we cannot reason about, and never let a NaN slip through a bare greater-than
    /// (`.claude/rules/csharp-architecture.md`).
    /// </summary>
    public static bool ShouldCorrect(float distanceSquared, float bandMetres)
    {
        if (float.IsNaN(distanceSquared) || float.IsInfinity(distanceSquared)) return false;
        if (float.IsNaN(bandMetres) || float.IsInfinity(bandMetres) || bandMetres < 0f) return false;
        return distanceSquared > bandMetres * bandMetres;
    }

    /// <summary>
    /// True when a seat position is a number the engine can be handed. The seat frame comes from the elephant's own
    /// position through GameEntity.SetFrame, so it is engine-sourced and validated by nobody: its consumers (the
    /// position lock and the rescue teleport) pass it straight to native. NaN there would be a native write we
    /// cannot reason about (`.claude/rules/csharp-architecture.md`, the engine-float gate).
    /// </summary>
    public static bool IsPlaceable(float x, float y, float z) =>
        !float.IsNaN(x) && !float.IsInfinity(x) &&
        !float.IsNaN(y) && !float.IsInfinity(y) &&
        !float.IsNaN(z) && !float.IsInfinity(z);

    /// <summary>True when two crew frames are far enough apart that their occupants will not shove each other.</summary>
    /// <summary>How far the live spine may sit from its scaled rest height before the read is treated as bad, at
    /// 1.0x. The spine bobs about 0.09 m and rides about 0.15 m high in the standing pose; a metre is a wrong read
    /// (a ragdoll, a recycled slot, an unposed skeleton), not a deck.</summary>
    public const float SpinePlausibleBandMetres = 1.0f;

    /// <summary>
    /// The howdah platform root's height above the elephant's feet, taken from where the spine actually is.
    ///
    /// The visible howdah is skinned to Spine1_05 and bobs with it, about 0.18 m peak to peak on a 1.3x elephant
    /// (measured 2026-09-22). A platform at a FIXED height sits inside that bob: set it low enough never to meet the
    /// deck and the archers look sunk behind the walls, set it at the deck's average and the deck rises through them
    /// half the time, which Mike saw as constant stuttering and re-nocking. Following the spine's height, and only
    /// its height, removes the relative motion without inheriting the bone's rotation, whose axes do not line up
    /// with the animal's.
    ///
    /// Answers NaN on a bad read so the caller falls back to the fixed height rather than putting archers wherever a
    /// broken bone says.
    /// </summary>
    public static float RootAboveFeetFromSpine(float spineAboveFeet, float scale)
    {
        if (float.IsNaN(spineAboveFeet) || float.IsInfinity(spineAboveFeet)) return float.NaN;
        if (float.IsNaN(scale) || float.IsInfinity(scale) || !(scale > 0f)) return float.NaN;
        float restSpine = ElephantConfig.HowdahSpineRestAboveFeet * scale;
        if (!(System.Math.Abs(spineAboveFeet - restSpine) <= SpinePlausibleBandMetres * scale)) return float.NaN;
        return spineAboveFeet + (ElephantConfig.HowdahRootRestAboveFeet - ElephantConfig.HowdahSpineRestAboveFeet) * scale;
    }

    public static bool FramesAreClear(float separationMetres) =>
        !float.IsNaN(separationMetres) && separationMetres >= MinimumFrameSeparation;

    /// <summary>
    /// How far from its seat an archer may be before the seat places it back by teleport, in metres (#1482). On
    /// 2026-11-19 a locked and attached archer stayed within 0.11 m of its seat on a mumakil at up to 5 m/s, so this
    /// fires only for an archer that is truly off its deck. Every placement is a full native movement reset that
    /// can abort a reload, which is why it is a last resort and is counted (the status line's rs field).
    /// </summary>
    public const float SeatRescueMetres = 3f;
}
