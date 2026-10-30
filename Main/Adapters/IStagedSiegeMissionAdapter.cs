using System.Collections.Generic;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Everything the staged siege service reads from or does to the live wall battle (docs/features/staged-siege.md). One adapter per
/// mission, used on the main thread only. Reads throw on an engine fault and the service catches them (its fail-safe). The domain
/// speaks plain floats, ints and enums, so nothing here hands the service an engine type.
/// </summary>
public interface IStagedSiegeMissionAdapter
{
    /// <summary>The mission as an object, for the slot's by-reference token compare.</summary>
    object MissionToken { get; }

    string SceneName { get; }

    /// <summary>Mission time in seconds.</summary>
    float Time { get; }

    bool IsSiegeBattle { get; }

    /// <summary>A sally-out or relief force carries a controller; this feature stays out of both.</summary>
    bool HasSallyOutController { get; }

    bool IsClientOrReplay { get; }

    /// <summary>The campaign's siege is at the walls (not the lord's hall, and not a Custom Battle).</summary>
    bool IsCampaignWallsAssault { get; }

    /// <summary>No campaign is running and the mission is a Custom Battle siege (it carries the Custom Battle spawn handler).</summary>
    bool IsCustomBattleSiege { get; }

    /// <summary>The campaign siege state's name, for the log line that explains an inert battle.</summary>
    string SiegeStateName { get; }

    /// <summary>The player's side; null when the spawn logic or its side is missing.</summary>
    StagedSide? PlayerSide { get; }

    bool IsDeploymentFinished { get; }

    SpawnCounts ReadSpawnCounts(StagedSide side);

    /// <summary>Active humans of the side's teams that are not running away (the spawn logic's active count includes the fleeing).</summary>
    int CountStanding(StagedSide side);

    /// <summary>The spawn logic runs the Balanced method on a custom timer: the prefix's swap reached the engine.</summary>
    bool SpawnSettingsAreStaged { get; }

    ZoneCount CountInZone(StagePoint center, float radius, float heightBand);

    /// <summary>Creates one flag per zone (A to D) and returns how many exist.</summary>
    int SpawnFlags(IReadOnlyList<StageZone> zones);

    void ColourFlag(StageId stage, StagedSide owner);

    /// <summary>Opens every gate that is neither destroyed nor disabled and returns how many it opened.</summary>
    int OpenGates();

    /// <summary>Units in the defenders' infantry formations the AI may split, the player's own formation excluded.</summary>
    int ActiveDefenderInfantry();

    DetachmentReading CreateDetachment(int count, IReadOnlyList<StagePoint> slots, StagePoint holdAt, StageFacing facing);

    /// <summary>What the last <see cref="CreateDetachment"/> took and from where, for the log.</summary>
    string DetachmentNote { get; }

    DetachmentReading ReadDetachment();

    void OrderDetachment(DetachmentOrder order, StagePoint at, StageFacing facing);

    /// <summary>Hands the detachment back to the AI. Idempotent.</summary>
    void ReleaseDetachment();

    /// <summary>Adds the staged tactic to each AI team and returns how many teams got it.</summary>
    int InstallTactics();

    void ResetTactics();

    /// <summary>Gives every formation with units a defend behavior it lacks, and returns how many it gave one to.</summary>
    int EnsureDefendBehaviors();

    string DescribeTactics();

    /// <summary>The quick-information win line and vanilla's "press Tab" hint: BattleEndLogic shows them only for a depleted enemy.</summary>
    void AnnounceVictory();
}
