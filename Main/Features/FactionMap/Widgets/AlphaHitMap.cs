using System;

namespace TAOM.Features.FactionMap.Widgets;

/// <summary>
/// Pure pixel-accurate hit-testing for a region texture: a 4×-downsampled max-alpha map plus the
/// normalized-coordinate opaque lookup that gates <c>PolygonWidget</c> hover/click. Extracted verbatim
/// from the widget (T4 refactor) so the index math — the off-by-one-prone part — is unit-tested, and so
/// the downsample factor lives in ONE place (it was previously duplicated in the builder and the lookup).
/// No TaleWorlds dependencies; the widget supplies pixels (from the PNG) and normalized mouse coordinates.
/// </summary>
public sealed class AlphaHitMap
{
    /// <summary>Downsample factor: each map cell covers a DS×DS pixel block (max alpha wins).</summary>
    public const int Downsample = 4;

    /// <summary>Pixel alpha strictly above this is opaque for hit-testing (pre-extraction widget value).</summary>
    public const byte DefaultOpaqueThreshold = 20;

    private readonly byte[] _map;
    private readonly int _mapW;
    private readonly int _mapH;
    private readonly byte _threshold;

    public AlphaHitMap(byte[] downsampledMaxAlpha, int texWidth, int texHeight, byte opaqueThreshold = DefaultOpaqueThreshold)
    {
        _map = downsampledMaxAlpha ?? Array.Empty<byte>();
        _mapW = MapDimension(texWidth);
        _mapH = MapDimension(texHeight);
        _threshold = opaqueThreshold;
    }

    /// <summary>Map cells along one axis for a texture dimension (ceiling division by <see cref="Downsample"/>).</summary>
    public static int MapDimension(int texDimension) => (texDimension + Downsample - 1) / Downsample;

    /// <summary>
    /// True when the map cell under widget-normalized coordinates ([0,1) — the caller divides local mouse
    /// position by widget size) holds alpha strictly above the threshold. Out-of-range coordinates and an
    /// undersized map read as NOT opaque (the pre-extraction fallbacks).
    /// </summary>
    public bool IsOpaqueAtNormalized(float normX, float normY)
    {
        if (normX < 0f || normX >= 1f || normY < 0f || normY >= 1f)
            return false;

        int mapX = (int)(normX * _mapW);
        int mapY = (int)(normY * _mapH);
        mapX = Math.Max(0, Math.Min(mapX, _mapW - 1));
        mapY = Math.Max(0, Math.Min(mapY, _mapH - 1));

        int idx = mapY * _mapW + mapX;
        if (idx < 0 || idx >= _map.Length)
            return false;

        return _map[idx] > _threshold;
    }

    /// <summary>
    /// Builds the map from locked 32bpp ARGB pixel data (B,G,R,A byte order — alpha at offset +3): each
    /// cell takes the MAX alpha of its DS×DS pixel block. Map dimensions come from the TEXTURE size while
    /// sampling clamps to the BITMAP size, exactly as the pre-extraction widget loop did.
    /// </summary>
    public static unsafe AlphaHitMap FromArgbPixels(
        IntPtr scan0, int stride, int bitmapWidth, int bitmapHeight, int texWidth, int texHeight,
        byte opaqueThreshold = DefaultOpaqueThreshold)
    {
        int mapW = MapDimension(texWidth);
        int mapH = MapDimension(texHeight);
        byte[] alphaMap = new byte[mapW * mapH];

        byte* ptr = (byte*)scan0.ToPointer();
        for (int my = 0; my < mapH; my++)
        {
            for (int mx = 0; mx < mapW; mx++)
            {
                byte maxAlpha = 0;
                int startY = my * Downsample;
                int startX = mx * Downsample;
                int endY = Math.Min(startY + Downsample, bitmapHeight);
                int endX = Math.Min(startX + Downsample, bitmapWidth);
                for (int sy = startY; sy < endY; sy++)
                {
                    for (int sx = startX; sx < endX; sx++)
                    {
                        byte a = ptr[sy * stride + sx * 4 + 3];
                        if (a > maxAlpha) maxAlpha = a;
                    }
                }
                alphaMap[my * mapW + mx] = maxAlpha;
            }
        }

        return new AlphaHitMap(alphaMap, texWidth, texHeight, opaqueThreshold);
    }
}
