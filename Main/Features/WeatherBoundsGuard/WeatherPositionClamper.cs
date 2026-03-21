using System;

namespace TAOM.Features.WeatherBoundsGuard;

public static class WeatherPositionClamper
{
    private const float Epsilon = 0.01f;

    public static (float x, float y) ClampPosition(float posX, float posY, float terrainW, float terrainH)
    {
        if (terrainW <= 0f || terrainH <= 0f)
            return (0f, 0f);

        float x = Math.Max(0f, Math.Min(posX, terrainW - Epsilon));
        float y = Math.Max(0f, Math.Min(posY, terrainH - Epsilon));
        return (x, y);
    }
}
