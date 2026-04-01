namespace TAOM.Features.CharacterCreation.Models;

/// <summary>
/// The character args to use when a culture's CC equipment roster has no horse.
/// Returned by <see cref="INarrativeHorseGuardService.TryBuildNoHorseArgs"/> when the
/// roster is found but the horse slot is empty. The patch uses this to short-circuit
/// vanilla and return only the player character entry (no mount actor).
/// </summary>
public sealed class NarrativeHorseGuardArgs
{
    public string CharacterId { get; }
    public int Age { get; }
    public string EquipmentId { get; }
    public string AnimationId { get; }
    public string SpawnPointEntityId { get; }
    public bool IsHuman { get; }
    public bool IsFemale { get; }

    public NarrativeHorseGuardArgs(
        string characterId,
        int age,
        string equipmentId,
        string animationId,
        string spawnPointEntityId,
        bool isHuman,
        bool isFemale)
    {
        CharacterId = characterId;
        Age = age;
        EquipmentId = equipmentId;
        AnimationId = animationId;
        SpawnPointEntityId = spawnPointEntityId;
        IsHuman = isHuman;
        IsFemale = isFemale;
    }
}
