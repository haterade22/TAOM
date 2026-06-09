using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;

namespace TAOM.Features.Arena.Hooks;

/// <summary>
/// Patch46 — Postfix on the public <c>TournamentFightMissionController.PrepareForMatch</c>
/// (an <c>ITournamentGameBehavior</c> member; public in v1.4.5 SandBox.dll).
///
/// Tournament participants are mounted when the settlement culture's
/// <c>TournamentTeamTemplatesFor{One,Two,Four}Participant</c> (or the <c>tournament_template_empire_*</c>
/// fallback) includes a horse in its battle equipment. <c>PrepareForMatch</c> clones that template
/// into <c>participant.MatchEquipment</c> (slot 10 = Horse, 11 = HorseHarness). Dwarves use a custom
/// (shorter) skeleton whose rider bone is misaligned, so a mounted dwarf spawns *inside* the horse mesh
/// (the same defect the eye-height workaround in <c>EyeHeightAdjustmentHook</c> exists for).
///
/// This Postfix runs after vanilla has assigned <c>MatchEquipment</c> for every participant — the single
/// chokepoint feeding BOTH the visual spawn (<c>SpawnAgentWithRandomItems</c>) and the AI simulation
/// (<c>Simulate</c> → <c>GetSimulationAttackPower</c>). For any participant whose race must fight on foot
/// (currently dwarves) it clears the Horse + HorseHarness slots, so the dwarf is never treated as cavalry
/// anywhere in the tournament. Keyed on race (not culture), so a dwarf competing in any town — and the
/// player, if the player is a dwarf — is caught.
///
/// Slots are cleared via <c>AddEquipmentToSlotWithoutAgent</c> (the same API vanilla uses in
/// <c>AddRandomClothes</c>) because no agent is built yet. Decision logic lives in
/// <see cref="ITournamentService.ShouldDismountInTournament"/>; this patch is a thin boundary
/// (ADR-002/007). Mirrors the lazy-resolve pattern of Patch40_HideoutDescription.
/// </summary>
[HarmonyPatch(typeof(TournamentFightMissionController), "PrepareForMatch")]
[HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
public static class Patch46_TournamentDwarfDismount
{
    private static ITournamentService _service;

    private static ITournamentService GetService() =>
        _service ??= TAOM.IoC.Resolve<ITournamentService>();

    // Harmony injects the private field `_match` as a parameter prefixed with THREE underscores
    // (`___` + the field name `_match`) → `____match` (four underscores total). Using `___match`
    // (three) made Harmony look for a field named `match`, which does not exist → the patch failed
    // to apply and crashed the game on load (HarmonyException at PatchCategory). See RCA 2026-06-09.
    [HarmonyPostfix]
    public static void Postfix(TournamentMatch ____match)
    {
        if (____match?.Teams == null) return;
        var service = GetService();
        if (service == null) return;

        foreach (var team in ____match.Teams)
        {
            if (team?.Participants == null) continue;
            foreach (var participant in team.Participants)
            {
                var equipment = participant?.MatchEquipment;
                var character = participant?.Character;
                if (equipment == null || character == null) continue;
                if (!service.ShouldDismountInTournament(character.Race)) continue;

                equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, EquipmentElement.Invalid);
                equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness, EquipmentElement.Invalid);
            }
        }
    }
}
