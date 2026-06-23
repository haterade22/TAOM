namespace TAOM.Features.FaceMorphCompat;

/// <summary>
/// Decides whether an agent of a given race must be built through the engine's GPU face-morph path
/// instead of the CPU / static-morph path.
///
/// Every custom LOTR race (i.e. everything except the vanilla "human" race) uses a custom head mesh
/// authored in LOTRLOME skins.xml. Those heads can lack the per-face-component morph data the
/// CPU/static-morph path indexes — verified read-only: the dwarf eye mesh carries 0 morph targets
/// (vs 102 on base/mouth), and NO custom LOTR head has a <c>face_eyelash_mesh</c> component that
/// vanilla heads (head_male_a / head_female_a) carry. When the engine builds such a head on the
/// static path it dereferences a null morph-data pointer and access-violates natively
/// ("No morph data found for face mesh. Can not do static morph.").
///
/// The static path is taken only for batched, deferred location-character spawns — arena / town
/// crowd spectators — which is why the bug surfaces as a naked, crashing dwarf crowd in the Erebor
/// arena while dwarves render fine in battles / town / character-creation (those use GPU morph).
/// Forcing GPU morph routes crowd agents onto the path their own head already renders correctly with.
/// See docs/features/face-morph-compat.md.
/// </summary>
public interface IFaceMorphCompatService
{
    /// <summary>
    /// True if an agent of <paramref name="raceId"/> must be built with GPU face morph (the safe
    /// path). True for every non-"human" race, and — fail-safe — for an unrecognized race id.
    /// </summary>
    bool ShouldForceGpuMorph(int raceId);
}
