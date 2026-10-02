using System.Collections.Generic;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>One playable faction as Kysaro's faction screen shows it (#704): TAOM's own faction data
/// (<c>factionmap/factions.json</c> and <c>regions.json</c>) plus his art and layout.</summary>
public sealed class FactionInfo
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string RegionName { get; set; } = "";

    public string CultureId { get; set; } = "";

    public List<KeyValuePair<string, bool>> Benefits { get; } = new();

    public List<string> Strengths { get; } = new();

    public List<string> Weaknesses { get; } = new();

    public string DifficultyText { get; set; } = "";

    public string IconSprite { get; set; } = "";

    public string BackgroundSprite { get; set; } = "";

    public string TerritorySprite { get; set; } = "";

    public double MapX { get; set; } = 0.5;

    public double MapY { get; set; } = 0.5;

    public string PaintedPortraitSprite { get; set; } = "";

    public float PaintedPortraitWidth { get; set; } = 640f;

    public float PaintedPortraitScale { get; set; } = 1f;

    /// <summary>The faction's side from <c>factions.json</c> (<c>free</c>, <c>evil</c> or
    /// <c>neutral</c>): the minimap marker colour.</summary>
    public string Alignment { get; set; } = "neutral";

    /// <summary>The kingdom whose ruler and lords the browse lists show; empty when none is mapped.</summary>
    public string KingdomId { get; set; } = "";
}
