using System;

namespace TAOM.Features.CharacterCreation.Models;

public class CareerMenuOptionDefinition
{
    public string CareerStringId { get; set; } = "";
    public string[] Skills { get; set; } = Array.Empty<string>();
    public string Attribute { get; set; } = "";
    public int FocusToAdd { get; set; } = 1;
    public int SkillLevelToAdd { get; set; } = 10;
    public int AttributeLevelToAdd { get; set; } = 1;
}
