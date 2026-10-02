using System.Collections.Generic;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>Kysaro's faction-screen tuning files, validated (#704).</summary>
public sealed class FactionScreenConfig
{
    public static readonly FactionScreenConfig Empty = new(
        new Dictionary<string, string>(), new Dictionary<string, string>(),
        new Dictionary<string, (double, double)>(), new Dictionary<string, ViewportTweak>());

    public FactionScreenConfig(
        IReadOnlyDictionary<string, string> viewportCharacters,
        IReadOnlyDictionary<string, string> kingdoms,
        IReadOnlyDictionary<string, (double X, double Y)> mapPositions,
        IReadOnlyDictionary<string, ViewportTweak> viewportTweaks)
    {
        ViewportCharacters = viewportCharacters;
        Kingdoms = kingdoms;
        MapPositions = mapPositions;
        ViewportTweaks = viewportTweaks;
    }

    /// <summary>Faction key to the character id the 3D viewport shows (<c>faction_characters.json</c>).</summary>
    public IReadOnlyDictionary<string, string> ViewportCharacters { get; }

    /// <summary>Faction key to the kingdom whose ruler and lords are listed (<c>faction_kingdoms.json</c>).</summary>
    public IReadOnlyDictionary<string, string> Kingdoms { get; }

    /// <summary>Faction key to the minimap pin, normalized 0 to 1 (<c>faction_map_pos.json</c>).</summary>
    public IReadOnlyDictionary<string, (double X, double Y)> MapPositions { get; }

    /// <summary>Faction key to viewport tweaks (<c>faction_viewport.json</c>).</summary>
    public IReadOnlyDictionary<string, ViewportTweak> ViewportTweaks { get; }
}
