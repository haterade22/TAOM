using System;

namespace TAOM.Features.FactionMap.Models;

public class FactionData
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public bool Playable { get; set; }
    public string GameFaction { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image { get; set; } = "";
    public string[] Traits { get; set; } = Array.Empty<string>();
    public FactionBonus[] Bonuses { get; set; } = Array.Empty<FactionBonus>();
    public FactionSpecialUnit[] SpecialUnits { get; set; } = Array.Empty<FactionSpecialUnit>();
    public FactionPerk[] Perks { get; set; } = Array.Empty<FactionPerk>();
    public string Side { get; set; } = "neutral";
    public string[] Strengths { get; set; } = Array.Empty<string>();
    public string[] Weaknesses { get; set; } = Array.Empty<string>();
    public int Difficulty { get; set; }
}

public class FactionBonus
{
    public string Text { get; set; } = "";
    public bool Positive { get; set; } = true;
}

public class FactionSpecialUnit
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class FactionPerk
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
