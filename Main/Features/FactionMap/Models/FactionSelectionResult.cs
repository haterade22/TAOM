namespace TAOM.Features.FactionMap.Models;

public class FactionSelectionResult
{
    public bool Found { get; set; }
    public string FactionName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Playable { get; set; }
    public bool HasCulture { get; set; }
    public string Side { get; set; } = "neutral";
    public int Difficulty { get; set; }
    public string DifficultyText { get; set; } = "";
    public string ImageId { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public string DarkPanelHex { get; set; } = "#1a1a2eD0";
    public string AccentColorHex { get; set; } = "#835513FF";
    public string[] Traits { get; set; } = System.Array.Empty<string>();
    public FactionBonus[] Bonuses { get; set; } = System.Array.Empty<FactionBonus>();
    public FactionPerk[] Perks { get; set; } = System.Array.Empty<FactionPerk>();
    public FactionSpecialUnit[] SpecialUnits { get; set; } = System.Array.Empty<FactionSpecialUnit>();
    public string[] Strengths { get; set; } = System.Array.Empty<string>();
    public string[] Weaknesses { get; set; } = System.Array.Empty<string>();
    public float BannerPosX { get; set; } = -1f;
    public float BannerPosY { get; set; } = -1f;
    public string BannerColorHex { get; set; } = "#FFFFFFFF";
    public string BannerSide { get; set; } = "neutral";
    public string BannerImage { get; set; } = "";
}
