using System;
using TAOM.Core.Domain;
using TaleWorlds.Library;

namespace TAOM.Features.HeroRace;

/// <summary>
/// The per-race framing maths for the 3D character tableau.
///
/// <para>Axis mapping is camera-relative and deliberately not the intuitive x/y/z order, inherited
/// from the config format: <c>Horizontal</c> offsets <c>y</c>, <c>Vertical</c> offsets <c>z</c>,
/// <c>Zoom</c> offsets <c>x</c>.</para>
///
/// <para>This works in ABSOLUTE origins rather than deltas. The caller passes the vanilla spawn
/// origin for the place the entity occupies and gets back where that entity should sit, so
/// re-applying the result on every tableau refresh is idempotent. A read-modify-write against the
/// entity current origin would be one line shorter and would walk the character out of shot over
/// repeated refreshes.</para>
///
/// <para><b>Rows are chosen by ENTITY, not by place.</b> The tableau can swap the character and its
/// mount between two fixed places. That swap moves the models; it does not turn the dwarf into a
/// horse. So the character always reads <c>&lt;race&gt;</c> and the mount always reads
/// <c>mount_&lt;race&gt;</c>, and the swap changes only which spawn origin the caller passes in.
/// The deleted CharacterTableauService selected the row by PLACE instead, which meant swapping
/// handed the horse the rider offsets. That was never observable because the service was dead code,
/// but it is wrong against the shipped data: <c>cave_troll</c> has a plain row and no mount row, so
/// place-based selection would have given a horse the troll -4.0 zoom and left the troll
/// unframed.</para>
/// </summary>
public class TableauPositionService : ITableauPositionService
{
    private readonly IRaceManager _raceManager;
    private readonly IRacePositionStore _store;

    public TableauPositionService(IRaceManager raceManager, IRacePositionStore store)
    {
        _raceManager = raceManager ?? throw new ArgumentNullException(nameof(raceManager));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public bool TryGetOrigin(Vec3 baseOrigin, int race, TableauEntity entity, out Vec3 origin)
    {
        origin = baseOrigin;

        // Validate the id BEFORE the name lookup. GetRaceNameFromId falls back to "human" for
        // unknown ids, so looking up first would silently apply human framing to a junk race id
        // (the "Lookup Functions With Fallbacks" rule).
        if (!_raceManager.IsValidRaceId(race))
            return false;

        var raceName = _raceManager.GetRaceNameFromId(race);
        if (string.IsNullOrEmpty(raceName))
            return false;

        // No cross-fallback between the two rows: they describe different models, and borrowing one
        // for the other sinks the mount into its rider. The store also guarantees a returned row is
        // finite, so a tuner-authored value cannot reach a native SetFrame through here.
        var item = entity == TableauEntity.Mount
            ? _store.ResolveAvatarMount(raceName)
            : _store.ResolveAvatar(raceName);

        // No row for this entity: vanilla framing, which is the documented default.
        if (item == null)
            return false;

        origin = new Vec3(
            baseOrigin.x + item.Zoom,
            baseOrigin.y + item.Horizontal,
            baseOrigin.z + item.Vertical,
            baseOrigin.w);

        return true;
    }
}
