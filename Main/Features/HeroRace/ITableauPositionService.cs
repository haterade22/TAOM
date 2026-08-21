using TaleWorlds.Library;

namespace TAOM.Features.HeroRace;

/// <summary>Which entity in the character tableau an offset lookup is for.</summary>
/// <remarks>
/// The config rows name ENTITIES, not positions: <c>dwarf</c> frames the dwarf, <c>mount_dwarf</c>
/// frames the horse a dwarf is sitting on. The tableau can swap the two entities between its two
/// fixed places, and that swap changes only WHERE each entity stands, never which model it is, so
/// it must not change which row the entity reads.
/// </remarks>
public enum TableauEntity
{
    /// <summary>The hero. Reads the plain <c>&lt;race&gt;</c> config row.</summary>
    Character,

    /// <summary>The mount. Reads the <c>mount_&lt;race&gt;</c> config row.</summary>
    Mount,
}

/// <summary>
/// Resolves the absolute origin a tableau entity should sit at, given the vanilla spawn origin for
/// the place it currently occupies and the hero race being previewed.
/// </summary>
public interface ITableauPositionService
{
    /// <summary>
    /// Computes the offset origin for one entity. Returns false (leaving <paramref name="origin"/>
    /// at <paramref name="baseOrigin"/>) when the race is unconfigured, the race id is invalid, or
    /// the configured offsets are not finite. In every one of those cases the entity keeps vanilla
    /// framing rather than moving somewhere arbitrary.
    /// </summary>
    /// <param name="baseOrigin">Vanilla spawn origin for the place this entity currently occupies.</param>
    bool TryGetOrigin(Vec3 baseOrigin, int race, TableauEntity entity, out Vec3 origin);
}
