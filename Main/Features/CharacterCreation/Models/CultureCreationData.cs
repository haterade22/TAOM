namespace TAOM.Features.CharacterCreation.Models;

public class CultureCreationData
{
    public string CultureId { get; set; }
    public string[] Races { get; set; }
    public string StartingSettlement { get; set; }
    public float DefaultAge { get; set; }
    public float DefaultWeight { get; set; }
    public float DefaultBuild { get; set; }
    // Culture-base bonus removed 2026-05-30 (matched vanilla budget) — defaults are 0 so a
    // missing key means "no culture base" (matches the zeroed cultures.json data), not 1/10.
    public int FocusToAdd { get; set; } = 0;
    public int SkillLevelToAdd { get; set; } = 0;
}
