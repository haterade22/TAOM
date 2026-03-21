using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace TAOM.Features.WeatherBoundsGuard.Hooks;

[HarmonyPatch(typeof(DefaultMapWeatherModel), "UpdateWeatherForPosition")]
[HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
public class DefaultMapWeatherModel_UpdateWeatherForPosition_Patch
{
    [HarmonyPrefix]
    public static void Prefix(ref CampaignVec2 position)
    {
        var wrapper = Campaign.Current?.MapSceneWrapper;
        if (wrapper == null)
            return;

        Vec2 terrainSize = wrapper.GetTerrainSize();
        var (x, y) = WeatherPositionClamper.ClampPosition(position.X, position.Y, terrainSize.X, terrainSize.Y);
        position = new CampaignVec2(new Vec2(x, y));
    }
}
