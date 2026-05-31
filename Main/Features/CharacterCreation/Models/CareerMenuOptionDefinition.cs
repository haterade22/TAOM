using System;

namespace TAOM.Features.CharacterCreation.Models;

public class CareerMenuOptionDefinition
{
    public string CareerStringId { get; set; } = "";
    public string[] Skills { get; set; } = Array.Empty<string>();
    public string Attribute { get; set; } = "";
    // Career is flavor-only (no CC stat bonus) as of 2026-05-30 — defaults are 0 so a
    // missing key means "no bonus" (matches the zeroed career_menu.json data), not 1/10/1.
    public int FocusToAdd { get; set; } = 0;
    public int SkillLevelToAdd { get; set; } = 0;
    public int AttributeLevelToAdd { get; set; } = 0;
}
