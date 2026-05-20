namespace TAOM.Features.CharacterCreation;

public interface ICareerStartingEquipmentService
{
    void ApplyCareerStartingEquipment(string cultureId, string careerId, bool isFemale, string playerHeroId);
}
