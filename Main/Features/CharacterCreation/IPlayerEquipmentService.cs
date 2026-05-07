namespace TAOM.Features.CharacterCreation;

public interface IPlayerEquipmentService
{
    void ApplyPlayerStartingEquipment(string cultureId, string titleType, bool isFemale, string playerHeroId);
}
