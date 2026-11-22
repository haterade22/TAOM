using System;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.TrainingGrounds.Teams.Hooks;

/// <summary>
/// Patch174 (#1528): the practice fight at the training grounds of a Free town is a team fight
/// (docs/features/team-practice.md). Three patches on vanilla's <see cref="ArenaPracticeFightMissionController"/>, none
/// of which skips vanilla. In any other town, in a co-op session, on a dedicated server or with the switch off,
/// <see cref="TeamPracticeState"/> answers "not teams" and each one returns at once.
///
/// - <c>ArrangePlayerTeamEnmity</c> (private, no arguments; runs in <c>AfterStart</c> and on every <c>StartPractice</c>)
///   postfix: decides the mode once for the controller, splits the AI teams into two sides and takes friendly fire off.
/// - <c>SpawnArenaAgent(Team, MatrixFrame)</c> postfix: outlines each allied fighter in blue while the player practises.
/// - <c>OnMissionTick(float)</c> prefix: when no fighter of the other side is left, writes 0 into vanilla's alive count
///   so vanilla's own victory path ends the fight.
///
/// The targets are the large methods on the path; the two tiny ones, <c>CheckPracticeEndedForPlayer</c> and
/// <c>get_RemainingOpponentCount</c>, are not patched because the JIT can inline them into a caller another mod compiled
/// first (lessons/harmony-il.md, #1189). Every patch catches and logs: the first runs inside <c>AfterStart</c>.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(Category)]
public static class ArenaPracticeFightMissionController_TeamPractice_Patch
{
    internal const string Category = "Patch174_TeamPractice";

    private static TeamPracticeService? _service;
    private static IModLogger? _logger;

    internal static void Initialize(TeamPracticeService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPatch(typeof(ArenaPracticeFightMissionController), "ArrangePlayerTeamEnmity")]
    [HarmonyPostfix]
    public static void ArrangeEnmityPostfix(ArenaPracticeFightMissionController __instance)
    {
        try
        {
            if (_service == null || !TeamPracticeState.Decide(__instance, DecideTeamTown))
                return;
            TeamPracticeFight.ArrangeSides(__instance, _logger);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "ArenaPracticeFightMissionController.ArrangePlayerTeamEnmity", ex);
        }
    }

    [HarmonyPatch(typeof(ArenaPracticeFightMissionController), "SpawnArenaAgent", new[] { typeof(Team), typeof(MatrixFrame) })]
    [HarmonyPostfix]
    public static void SpawnPostfix(ArenaPracticeFightMissionController __instance, Team team, Agent __result)
    {
        try
        {
            if (TeamPracticeState.IsTeams(__instance))
                TeamPracticeFight.OutlineAlly(__instance, team, __result, _logger);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "ArenaPracticeFightMissionController.SpawnArenaAgent", ex);
        }
    }

    [HarmonyPatch(typeof(ArenaPracticeFightMissionController), nameof(ArenaPracticeFightMissionController.OnMissionTick), new[] { typeof(float) })]
    [HarmonyPrefix]
    public static void TickPrefix(ArenaPracticeFightMissionController __instance)
    {
        if (!TeamPracticeState.IsTeams(__instance))
            return;
        try
        {
            TeamPracticeFight.TryDeclareWin(__instance);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "ArenaPracticeFightMissionController.OnMissionTick", ex);
        }
    }

    /// <summary>
    /// The side of the town the fight is in: the owner's faction and that faction's own culture, read from the encounter
    /// vanilla reads at <c>AfterStart</c> (<c>ArenaPracticeFightMissionController.cs:77</c>).
    /// </summary>
    private static bool DecideTeamTown() => TeamPracticeRules.IsTeamMode(TeamPracticeAccess.IsAvailable, IsTeamTown);

    private static bool IsTeamTown() =>
        _service!.IsTeamTown(FactionSides.Of(PlayerEncounter.LocationEncounter?.Settlement?.MapFaction));
}
