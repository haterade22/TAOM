using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace TAOM.Features.WeatherBoundsGuard.Hooks;

[HarmonyPatch(typeof(DefaultMapWeatherModel), "GetWeatherEventInPosition")]
[HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
public class DefaultMapWeatherModel_GetWeatherEventInPosition_Patch
{
    [HarmonyPrefix]
    public static void Prefix(ref Vec2 pos)
    {
        var wrapper = Campaign.Current?.MapSceneWrapper;
        if (wrapper == null)
            return;

        Vec2 terrainSize = wrapper.GetTerrainSize();
        var (x, y) = WeatherPositionClamper.ClampPosition(pos.x, pos.y, terrainSize.X, terrainSize.Y);
        pos = new Vec2(x, y);
    }
}
