using System;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleScenes.Hooks;

[HarmonyPatch(typeof(MBMapScene), nameof(MBMapScene.GetBattleSceneIndexMap))]
[HarmonyPatchCategory("Patch0_BattleScenes")]
public class MBMapScene_GetBattleSceneIndexMap_Patch
{
    private static bool _isRetrying;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 250;

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    [HarmonyPrefix]
    public static bool Prefix(Scene mapScene, ref byte[] indexData, ref int width, ref int height)
    {
        if (_isRetrying) return true;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _isRetrying = true;
                MBMapScene.GetBattleSceneIndexMap(mapScene, ref indexData, ref width, ref height);

                var msg = $"TAOM: Battle scene index map loaded - {width}x{height} ({indexData?.Length ?? 0} bytes)";
                if (attempt > 1)
                    msg += $" after {attempt} attempts";
                Debug.Print(msg, 0, Debug.DebugColor.Green, 17592186044416uL);
                return false;
            }
            catch (AccessViolationException) when (attempt < MaxRetries)
            {
                Debug.Print(
                    $"TAOM: GetBattleSceneIndexMap AccessViolation (attempt {attempt}/{MaxRetries}), retrying in {RetryDelayMs}ms...",
                    0, Debug.DebugColor.Yellow, 17592186044416uL);
                Thread.Sleep(RetryDelayMs);
            }
            finally
            {
                _isRetrying = false;
            }
        }

        Debug.Print("TAOM: GetBattleSceneIndexMap - final attempt, running unguarded", 0, Debug.DebugColor.Red, 17592186044416uL);
        return true;
    }
}
