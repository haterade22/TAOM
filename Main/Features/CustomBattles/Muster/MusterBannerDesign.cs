using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>
/// One army's banner on the Custom Battle muster screen, as a banner code. It starts as the chosen kingdom's full
/// banner and stays exactly that code until the player edits it: a background colour recolours layer 0, an emblem
/// colour recolours every icon layer (Banner.ChangePrimaryColor and ChangeIconColors do the same), and an emblem swaps
/// the one icon of a simple banner or replaces the icon layers of a multilayer one with one centred emblem
/// (Banner.SetIconMeshId changes layer 1 only, often an ornament there). A faction change resets the edits.
/// A code is 10 integers per layer: mesh, colour, colour 2, size x, y, position x, y, stroke, mirror, rotation.
/// </summary>
public sealed class MusterBannerDesign
{
    private const int Fields = 10;

    // one emblem on the banner's centre, as the engine's Banner.CreateOneColoredBannerWithOneIcon places it
    private static readonly int[] CentredEmblem = { 512, 512, 764, 764, 0, 0, 0 };

    private int? _background, _emblemColor, _emblem;

    public string Source { get; private set; } = "";
    public bool IsEdited => _background != null || _emblemColor != null || _emblem != null;
    public int? Emblem => _emblem;
    public string Code => Compose(_emblem);

    public void Reset(string? source)
    {
        Source = source ?? "";
        _background = _emblemColor = _emblem = null;
    }

    /// <summary>A palette colour id; -1 (a colour outside the palette) changes nothing.</summary>
    public void SetBackgroundColor(int colorId) { if (colorId >= 0) _background = colorId; }

    public void SetEmblemColor(int colorId) { if (colorId >= 0) _emblemColor = colorId; }

    public void SetEmblem(int meshId) { if (meshId >= 0) _emblem = meshId; }

    /// <summary>The banner as it would be with this emblem, for the emblem chooser; the design does not change.</summary>
    public string WithEmblem(int meshId) => Compose(meshId);

    /// <summary>The code when it holds whole layers, else null (the engine's own check passes "1.2.3" as an empty banner).</summary>
    public static string? ExactCode(string? code)
    {
        var trimmed = code?.Trim();
        return Parse(trimmed) is { Count: > 0 } ? trimmed : null;
    }

    /// <summary>The kingdom banner of a culture, or null when it has none that parses.</summary>
    public static string? KingdomCode(IReadOnlyDictionary<string, string>? cultureBanners, string? cultureId) =>
        cultureBanners != null && cultureId != null && cultureBanners.TryGetValue(cultureId, out var code) ? ExactCode(code) : null;

    private string Compose(int? emblem)
    {
        if (_background == null && _emblemColor == null && emblem == null) return Source;
        var layers = Parse(Source);
        if (layers == null || layers.Count == 0) return Source;
        if (_background != null) { layers[0][1] = _background.Value; layers[0][2] = _background.Value; }
        if (emblem != null)
        {
            if (layers.Count == 2) layers[1][0] = emblem.Value;
            else
            {
                int color = layers.Count > 1 ? layers[1][1] : layers[0][2];
                int color2 = layers.Count > 1 ? layers[1][2] : layers[0][2];
                layers.RemoveRange(1, layers.Count - 1);
                layers.Add(new[] { emblem.Value, color, color2 }.Concat(CentredEmblem).ToArray());
            }
        }
        if (_emblemColor != null)
            for (int i = 1; i < layers.Count; i++) { layers[i][1] = _emblemColor.Value; layers[i][2] = _emblemColor.Value; }
        return string.Join(".", layers.SelectMany(l => l));
    }

    private static List<int[]>? Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var parts = code!.Split('.');
        if (parts.Length % Fields != 0) return null;
        var layers = new List<int[]>();
        for (int i = 0; i < parts.Length; i += Fields)
        {
            var layer = new int[Fields];
            for (int f = 0; f < Fields; f++)
                if (!int.TryParse(parts[i + f], out layer[f])) return null;
            layers.Add(layer);
        }
        return layers;
    }
}
