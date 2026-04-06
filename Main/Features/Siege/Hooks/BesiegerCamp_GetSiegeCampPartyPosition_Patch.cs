using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace TAOM.Features.Siege.Hooks;

[HarmonyPatch(typeof(BesiegerCamp), "GetSiegeCampPartyPosition")]
[HarmonyPatchCategory("Patch8_SiegeCampGuard")]
public class BesiegerCamp_GetSiegeCampPartyPosition_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(
        BesiegerCamp __instance,
        MobileParty mobileParty,
        ref MatrixFrame[] siegeCamp1GlobalFrames,
        ref MatrixFrame[] siegeCamp2GlobalFrames,
        ref CampaignVec2 __result)
    {
        try
        {
            if (siegeCamp1GlobalFrames != null && siegeCamp1GlobalFrames.Length > 0)
                return true;

            var settlement = __instance.SiegeEvent?.BesiegedSettlement;
            var settlementId = settlement?.StringId ?? "unknown";
            var settlementName = settlement?.Name?.ToString() ?? "unknown";
            int camp2Count = siegeCamp2GlobalFrames?.Length ?? 0;

            Debug.Print(
                $"TAOM: WARNING — Settlement '{settlementName}' (id={settlementId}) has no siege_camp_1 scene entities (camp2={camp2Count} frames). Fix in map editor.",
                0, Debug.DebugColor.Red, 17592186044416uL);

            if (siegeCamp2GlobalFrames != null && siegeCamp2GlobalFrames.Length > 0)
            {
                Debug.Print(
                    $"TAOM: Using siege_camp_2 frames as fallback for '{settlementId}'",
                    0, Debug.DebugColor.Yellow, 17592186044416uL);
                siegeCamp1GlobalFrames = siegeCamp2GlobalFrames;
                siegeCamp2GlobalFrames = Array.Empty<MatrixFrame>();
                return true;
            }

            Debug.Print(
                $"TAOM: No siege camp frames at all for '{settlementId}', generating positions around gate",
                0, Debug.DebugColor.Red, 17592186044416uL);
            // Distribute parties around the gate instead of stacking on one point
            var gate = settlement.GatePosition;
            int partyIndex = mobileParty?.Party?.Index ?? 0;
            float angle = (partyIndex % 8) * (MathF.PI * 2f / 8f);
            float radius = 0.5f + (partyIndex / 8) * 0.3f;
            __result = new CampaignVec2(
                new Vec2(gate.X + MathF.Cos(angle) * radius,
                         gate.Y + MathF.Sin(angle) * radius),
                gate.IsOnLand);
            return false;
        }
        catch (Exception ex)
        {
            Debug.Print($"TAOM: BesiegerCamp patch exception: {ex.Message}", 0, Debug.DebugColor.Red, 17592186044416uL);
            return true;
        }
    }
}
