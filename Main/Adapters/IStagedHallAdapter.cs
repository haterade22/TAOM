using System.Collections.Generic;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Everything the staged hall service reads from or does to the lord's hall fight (docs/features/staged-siege.md "The lord's hall").
/// One adapter per mission, used on the main thread only. Reads throw on an engine fault and the service catches them. The domain
/// speaks plain floats, ints and enums, so nothing here hands the service an engine type.
/// </summary>
public interface IStagedHallAdapter
{
    /// <summary>The mission as an object, for the slot's by-reference token compare.</summary>
    object MissionToken { get; }

    /// <summary>Mission time in seconds.</summary>
    float Time { get; }

    /// <summary>The mission carries the lord's hall fight controller: the hall mission's marker.</summary>
    bool IsHallMission { get; }

    bool HasCampaign { get; }

    bool IsClientOrReplay { get; }

    /// <summary>The encounter settlement's saved siege state is the lord's hall. False without a campaign.</summary>
    bool IsInLordsHall { get; }

    /// <summary>
    /// The scene the walls assault loads for the encounter settlement: its "center" location's scene at the town's current wall level.
    /// Empty when there is no campaign, encounter settlement, town or location. The service looks it up in the scene catalog.
    /// </summary>
    string WallsSceneName { get; }

    /// <summary>The encounter settlement's id, for the log; empty when there is none or no campaign.</summary>
    string EncounterSettlementId { get; }

    /// <summary>Every fight area marker of the scene, as plain values.</summary>
    IReadOnlyList<HallMarker> ReadMarkers();

    /// <summary>Creates the flag at point E and returns how many exist (0 or 1).</summary>
    int SpawnFlag(StagePoint centre);

    void ColourFlag(StagedSide owner);

    /// <summary>Humans inside point E: active, not running away, counted by their team's side.</summary>
    ZoneCount CountInHall(HallZone zone);

    /// <summary>Active humans of the side's teams that are not running away, anywhere in the mission.</summary>
    int CountStanding(StagedSide side);

    /// <summary>Stops the hall controller's troop spawning for both sides. Idempotent.</summary>
    void StopSpawners();

    /// <summary>Shows the localized "the lord's hall is taken" message.</summary>
    void AnnounceHallTaken();
}
