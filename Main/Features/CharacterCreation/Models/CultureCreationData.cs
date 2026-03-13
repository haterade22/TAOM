namespace TAOM.Features.CharacterCreation.Models;

public class CultureCreationData
{
    public string CultureId { get; set; }
    public string[] Races { get; set; }
    public string StartingSettlement { get; set; }
    public float DefaultAge { get; set; }
    public float DefaultWeight { get; set; }
    public float DefaultBuild { get; set; }
    public int FocusToAdd { get; set; } = 1;
    public int SkillLevelToAdd { get; set; } = 10;
}
