using System;

namespace TAOM.Features.CharacterCreation.Models;

public class NarrativeOptionDefinition
{
    public string StringId { get; set; } = "";
    public string CultureId { get; set; } = "";
    public string Text { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Skills { get; set; } = Array.Empty<string>();
    public string Attribute { get; set; } = "";
    public string OccupationType { get; set; } = "";
    public string TitleType { get; set; } = "";
    public int FocusToAdd { get; set; } = 1;
    public int SkillLevelToAdd { get; set; } = 10;
    public int AttributeLevelToAdd { get; set; } = 1;
}
