using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.time_status` — dump every state that can freeze campaign time, and
/// `taom.rescue_time` — clear the recoverable ones (menu context, time lock, engine pause).
///
/// Built for the 2026-08-23 field-camp freeze: establishing a camp left the session hard-paused
/// (big PAUSED banner, play buttons dead) and the state SURVIVED save/load, which points at
/// persisted or re-derived campaign state (a re-activated menu context being the prime suspect)
/// rather than a wedged UI. The status dump reads every candidate in one shot instead of another
/// round of decompile-and-guess; the rescue exists so a stuck save is recoverable in the field.
/// </summary>
public static class TimeControlCheats
{
    private const string StatusUsage = "Format is \"taom.time_status\". No arguments.";
    private const string RescueUsage = "Format is \"taom.rescue_time\". No arguments.";

    [CommandLineFunctionality.CommandLineArgumentFunction("time_status", "taom")]
    public static string TimeStatus(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, args =>
        {
            var c = Campaign.Current;
            var main = MobileParty.MainParty;
            var sb = new StringBuilder();
            sb.AppendLine($"TimeControlMode:      {c.TimeControlMode}");
            sb.AppendLine($"TimeControlModeLock:  {c.TimeControlModeLock}");
            sb.AppendLine($"IsMainPartyWaiting:   {c.IsMainPartyWaiting}");
            sb.AppendLine($"MBCommon.IsPaused:    {MBCommon.IsPaused}");
            sb.AppendLine($"MenuContext:          {(c.CurrentMenuContext == null ? "<none>" : c.CurrentMenuContext.GameMenu?.StringId ?? "<context with NULL GameMenu>")}");
            var mapState = Game.Current?.GameStateManager?.LastOrDefault<MapState>();
            sb.AppendLine($"MapState.GameMenuId:  {mapState?.GameMenuId ?? "<null>"}");
            sb.AppendLine($"MapState.AtMenu:      {mapState?.AtMenu.ToString() ?? "<no MapState>"}");
            sb.AppendLine($"ActiveState:          {Game.Current?.GameStateManager?.ActiveState?.GetType().Name ?? "<null>"}");
            sb.AppendLine($"Main.DefaultBehavior: {main?.DefaultBehavior.ToString() ?? "<no main party>"}");
            sb.AppendLine($"Main.IsMoving:        {main?.IsMoving.ToString() ?? "-"}");
            sb.AppendLine($"PlayerEncounter:      {(PlayerEncounter.Current == null ? "<none>" : PlayerEncounter.EncounteredParty?.Id ?? "<active, no party>")}");
            return sb.ToString();
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("rescue_time", "taom")]
    public static string RescueTime(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, RescueUsage, args =>
        {
            var c = Campaign.Current;
            var sb = new StringBuilder();
            var mapState = Game.Current?.GameStateManager?.LastOrDefault<MapState>();

            if (mapState != null && mapState.AtMenu)
            {
                sb.AppendLine($"Exiting menu mode (menu '{mapState.GameMenuId ?? "<null>"}').");
                mapState.ExitMenuMode();
            }
            if (c.TimeControlModeLock)
            {
                sb.AppendLine("Releasing TimeControlModeLock.");
                c.SetTimeControlModeLock(isLocked: false);
            }
            if (MBCommon.IsPaused)
            {
                sb.AppendLine("Unpausing the game engine.");
                MBCommon.UnPauseGameEngine();
            }
            c.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
            sb.AppendLine($"TimeControlMode now {c.TimeControlMode}.");
            return sb.Length == 0 ? "Nothing to rescue." : sb.ToString();
        });
}
