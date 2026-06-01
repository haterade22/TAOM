using System;
using System.Globalization;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public class FactionSelectionService : IFactionSelectionService
{
    private readonly IFactionRegistryService _registry;

    public FactionSelectionService(IFactionRegistryService registry)
    {
        _registry = registry;
    }

    public FactionSelectionResult SelectRegion(string regionName)
    {
        if (string.IsNullOrEmpty(regionName))
            return new FactionSelectionResult { Found = false };

        var region = _registry.GetRegion(regionName);
        var faction = _registry.GetFactionForRegion(regionName);

        if (faction == null)
            return BuildUnknownRegionResult(regionName);

        return BuildFactionResult(faction, region);
    }

    public string? GetCultureIdForRegion(string regionName)
    {
        var faction = _registry.GetFactionForRegion(regionName);
        if (faction == null || string.IsNullOrEmpty(faction.GameFaction))
            return null;
        return faction.GameFaction;
    }

    public string MakeDarkPanelHex(string factionHex)
    {
        if (string.IsNullOrEmpty(factionHex) || factionHex.Length < 7)
            return "#1a1a2eFF";

        var (r, g, b, _) = ParseHexColor(factionHex);
        float baseR = 0.07f, baseG = 0.07f, baseB = 0.10f;
        const float mix = 0.18f;

        int outR = Clamp((int)(((1f - mix) * baseR + mix * r) * 255f));
        int outG = Clamp((int)(((1f - mix) * baseG + mix * g) * 255f));
        int outB = Clamp((int)(((1f - mix) * baseB + mix * b) * 255f));

        return $"#{outR:X2}{outG:X2}{outB:X2}D0";
    }

    public string MakeAccentColorHex(string factionHex)
    {
        if (string.IsNullOrEmpty(factionHex) || factionHex.Length < 7)
            return "#835513FF";

        var (r, g, b, _) = ParseHexColor(factionHex);
        int outR = Math.Max(15, Math.Min(255, (int)(r * 0.55f * 255f)));
        int outG = Math.Max(15, Math.Min(255, (int)(g * 0.55f * 255f)));
        int outB = Math.Max(15, Math.Min(255, (int)(b * 0.55f * 255f)));

        return $"#{outR:X2}{outG:X2}{outB:X2}FF";
    }

    public string FormatDifficultyText(int difficulty)
    {
        return difficulty switch
        {
            1 => "{=taom_faction_difficulty_1}Difficulty: Very Easy",
            2 => "{=taom_faction_difficulty_2}Difficulty: Easy",
            3 => "{=taom_faction_difficulty_3}Difficulty: Medium",
            4 => "{=taom_faction_difficulty_4}Difficulty: Medium-Hard",
            5 => "{=taom_faction_difficulty_5}Difficulty: Hard",
            6 => "{=taom_faction_difficulty_6}Difficulty: Very Hard",
            7 => "{=taom_faction_difficulty_7}Difficulty: Extreme",
            _ => "",
        };
    }

    private FactionSelectionResult BuildFactionResult(FactionData faction, RegionData? region)
    {
        bool hasBanner = faction.Playable && region is { HasCapitalPos: true };

        return new FactionSelectionResult
        {
            Found = true,
            FactionName = faction.Name,
            Description = faction.Description,
            Playable = faction.Playable,
            HasCulture = !string.IsNullOrEmpty(faction.GameFaction),
            Side = faction.Side ?? "neutral",
            Difficulty = faction.Difficulty,
            DifficultyText = FormatDifficultyText(faction.Difficulty),
            ImageId = faction.Image ?? "",
            ColorHex = faction.Color ?? "",
            DarkPanelHex = MakeDarkPanelHex(faction.Color),
            AccentColorHex = MakeAccentColorHex(faction.Color),
            Traits = faction.Traits ?? Array.Empty<string>(),
            Bonuses = faction.Bonuses ?? Array.Empty<FactionBonus>(),
            Perks = faction.Perks ?? Array.Empty<FactionPerk>(),
            SpecialUnits = faction.SpecialUnits ?? Array.Empty<FactionSpecialUnit>(),
            Strengths = faction.Strengths ?? Array.Empty<string>(),
            Weaknesses = faction.Weaknesses ?? Array.Empty<string>(),
            BannerPosX = hasBanner ? region!.CapitalX : -1f,
            BannerPosY = hasBanner ? region!.CapitalY : -1f,
            BannerColorHex = hasBanner ? faction.Color ?? "#FFFFFFFF" : "#FFFFFFFF",
            BannerSide = hasBanner ? faction.Side ?? "neutral" : "neutral",
            BannerImage = hasBanner ? "banner_" + region!.FactionId : "",
        };
    }

    private static FactionSelectionResult BuildUnknownRegionResult(string regionName)
    {
        return new FactionSelectionResult
        {
            Found = true,
            FactionName = regionName,
            Playable = false,
            HasCulture = false,
            Side = "neutral",
            DarkPanelHex = "#1a1a2eD0",
            AccentColorHex = "#835513FF",
            BannerPosX = -1f,
            BannerPosY = -1f,
        };
    }

    private static (float r, float g, float b, float a) ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7)
            return (1f, 1f, 1f, 1f);

        hex = hex.TrimStart('#');
        try
        {
            float r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255f;
            float a = hex.Length >= 8
                ? int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber) / 255f
                : 1f;
            return (r, g, b, a);
        }
        catch
        {
            return (1f, 1f, 1f, 1f);
        }
    }

    private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));
}
