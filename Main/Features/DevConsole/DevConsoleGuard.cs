using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DevConsole;

/// <summary>
/// The cheat gates every <c>taom.*</c> console command passes through, in the vanilla
/// <c>ref string error</c> idiom (<see cref="CampaignCheats.CheckCheatUsage"/>).
///
/// Three gates rather than one, because <see cref="CampaignCheats.CheckCheatUsage"/> requires
/// <c>Campaign.Current != null</c> — which would lock the mission commands out of custom battles,
/// TAOM's primary venue for testing creatures, mounts and equipment. Vanilla's own <c>mission.*</c>
/// cheats require no cheat mode at all (only <c>!GameNetwork.IsSessionActive</c>), so
/// <see cref="CheckMissionCheatUsage"/> is still strictly tighter than the engine's precedent.
///
/// All three answer <see cref="CampaignCheats.CampaignNotStarted"/> when there is no
/// <see cref="Game.Current"/>: at the main menu there is no session of any kind, and reporting one
/// consistent string there keeps the assembly-wide binding test able to assert a single value.
///
/// Callers reach these through <see cref="TaomConsole"/>, never directly — that is what makes the
/// gate structurally unforgettable, and it is where the exception guard lives.
/// </summary>
internal static class DevConsoleGuard
{
    internal const string NoActiveMission = "No active mission — run this from inside a battle, siege or custom battle.";

    /// <summary>Delegate shape for the three gates, so <see cref="TaomConsole"/> can take one as a parameter.</summary>
    internal delegate bool GateCheck(ref string error);

    /// <summary>
    /// Full vanilla gate: a running campaign plus cheat mode. For commands that read or mutate
    /// campaign state (settlements, clans, heroes, the map).
    /// </summary>
    internal static bool CheckCampaignCheatUsage(ref string error) => CampaignCheats.CheckCheatUsage(ref error);

    /// <summary>
    /// Cheat mode plus an open mission, but deliberately NOT a campaign — so agent/spawn/scene
    /// commands work in a custom battle.
    /// </summary>
    internal static bool CheckMissionCheatUsage(ref string error)
    {
        if (!CheckCheatMode(ref error)) return false;

        if (Mission.Current == null)
        {
            error = NoActiveMission;
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Cheat mode only. For commands that inspect process-level state — config, registries, the
    /// Harmony patch table — none of which needs a campaign or a mission to be meaningful.
    /// </summary>
    internal static bool CheckCheatMode(ref string error)
    {
        var game = Game.Current;
        if (game == null)
        {
            error = CampaignCheats.CampaignNotStarted;
            return false;
        }

        if (!game.CheatMode)
        {
            error = CampaignCheats.CheatModeDisabled;
            return false;
        }

        error = string.Empty;
        return true;
    }
}
