using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch15_BannerLayerLimit")]
public static class Banner_TryGetBannerDataFromCode_Transpiler
{
    private static IBannerColorConfigProvider? _configProvider;
    private static IModLogger? _logger;

    public static void Initialize(IBannerColorConfigProvider configProvider, IModLogger logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(Banner), "TryGetBannerDataFromCode",
            new[] { typeof(string), typeof(List<BannerData>).MakeByRefType() });

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);

        // Find the RemoveRange callvirt
        int removeRangeIdx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].opcode == OpCodes.Callvirt &&
                list[i].operand is MethodInfo mi &&
                mi.Name == "RemoveRange" &&
                mi.DeclaringType == typeof(List<BannerData>))
            {
                removeRangeIdx = i;
                break;
            }
        }

        if (removeRangeIdx < 0)
        {
            _logger?.LogWarning("[BannerColor] Transpiler: RemoveRange not found — layer limit patch not applied");
            return list;
        }

        // Find the Ble_S (or Ble) guard immediately before RemoveRange
        int bleIdx = -1;
        for (int i = removeRangeIdx - 1; i >= System.Math.Max(0, removeRangeIdx - 15); i--)
        {
            if (list[i].opcode == OpCodes.Ble_S || list[i].opcode == OpCodes.Ble)
            {
                bleIdx = i;
                break;
            }
        }

        if (bleIdx < 0)
        {
            _logger?.LogWarning("[BannerColor] Transpiler: Ble_S guard not found — layer limit patch not applied");
            return list;
        }

        // Insert AFTER the Ble_S — at that point the stack is empty (ble consumed both operands).
        // Inserting before would corrupt the stack: [Count][32] are already pushed for the ble comparison.
        var skipTarget = list[bleIdx].operand; // the Label the Ble_S jumps to when count <= 32

        var callHelper = new CodeInstruction(OpCodes.Call,
            AccessTools.Method(typeof(Banner_TryGetBannerDataFromCode_Transpiler), nameof(ShouldSkipLayerLimit)));
        var brTrue = new CodeInstruction(OpCodes.Brtrue_S, skipTarget);

        list.Insert(bleIdx + 1, callHelper);
        list.Insert(bleIdx + 2, brTrue);

        _logger?.LogDebug("[BannerColor] Transpiler: Banner layer limit patch applied");
        return list;
    }

    public static bool ShouldSkipLayerLimit() =>
        _configProvider?.GetConfig().EnableLayerLimitTranspiler ?? false;
}
