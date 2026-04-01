using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public sealed class NarrativeHorseGuardService : INarrativeHorseGuardService
{
    private readonly IEquipmentRosterProvider _rosterProvider;

    private const string AnimationId = "act_childhood_schooled";
    private const string SpawnPointEntityId = "spawnpoint_player_1";

    public NarrativeHorseGuardService(IEquipmentRosterProvider rosterProvider)
    {
        _rosterProvider = rosterProvider;
    }

    public NarrativeHorseGuardArgs TryBuildNoHorseArgs(
        string cultureId,
        string titleType,
        bool isFemale,
        string characterId,
        int age)
    {
        var equipmentId = NarrativeMenuBuilder.BuildEquipmentRosterId(cultureId, titleType, isFemale);

        var hasHorse = _rosterProvider.HasHorse(equipmentId);
        if (hasHorse != false)
            return null; // Roster not found (null) or horse present (true) — let vanilla run

        return new NarrativeHorseGuardArgs(
            characterId: characterId,
            age: age,
            equipmentId: equipmentId,
            animationId: AnimationId,
            spawnPointEntityId: SpawnPointEntityId,
            isHuman: true,
            isFemale: isFemale);
    }
}
