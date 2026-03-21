using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace TAOM.Features.WeatherBoundsGuard.Hooks;

[HarmonyPatch(typeof(DefaultMapWeatherModel), "GetSnowAndRainDataForPosition")]
[HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
public class DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch
{
    [HarmonyPrefix]
    public static void Prefix(ref Vec2 position)
    {
        var wrapper = Campaign.Current?.MapSceneWrapper;
        if (wrapper == null)
            return;

        Vec2 terrainSize = wrapper.GetTerrainSize();
        var (x, y) = WeatherPositionClamper.ClampPosition(position.x, position.y, terrainSize.X, terrainSize.Y);
        position = new Vec2(x, y);
    }
}
