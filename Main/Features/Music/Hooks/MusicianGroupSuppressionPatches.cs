using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.Objects;
using SandBox.Objects.Usables;

namespace TAOM.Features.Music.Hooks;

[HarmonyPatchCategory("Patch46_Music")]
public static class MusicianGroup_SetPlayList_Patch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(MusicianGroup),
            nameof(MusicianGroup.SetPlayList),
            new[] { typeof(List<SettlementMusicData>) });
    }

    [HarmonyPrefix]
    public static bool Prefix(object __instance, List<SettlementMusicData> playList)
    {
        if (playList == null || playList.Count == 0)
        {
            MusicianGroupSuppressionPatchHelper.MarkTavernContextInactive();
            return true;
        }

        return !MusicianGroupSuppressionPatchHelper.ShouldSuppressVanillaTavernMusic(__instance);
    }
}

[HarmonyPatchCategory("Patch46_Music")]
public static class MusicianGroup_CheckNewTrackStart_Patch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(MusicianGroup), "CheckNewTrackStart", new System.Type[0]);
    }

    [HarmonyPrefix]
    public static bool Prefix(object __instance)
    {
        return !MusicianGroupSuppressionPatchHelper.ShouldSuppressVanillaTavernMusic(__instance);
    }
}

[HarmonyPatchCategory("Patch46_Music")]
public static class MusicianGroup_CheckTrackEnd_Patch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(MusicianGroup), "CheckTrackEnd", new System.Type[0]);
    }

    [HarmonyPrefix]
    public static bool Prefix(object __instance)
    {
        return !MusicianGroupSuppressionPatchHelper.ShouldSuppressVanillaTavernMusic(__instance);
    }
}

[HarmonyPatchCategory("Patch46_Music")]
public static class MusicianGroup_SetupInstruments_Patch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(MusicianGroup), "SetupInstruments", new System.Type[0]);
    }

    [HarmonyPrefix]
    public static bool Prefix(object __instance)
    {
        return !MusicianGroupSuppressionPatchHelper.ShouldSuppressVanillaTavernMusic(__instance);
    }
}
