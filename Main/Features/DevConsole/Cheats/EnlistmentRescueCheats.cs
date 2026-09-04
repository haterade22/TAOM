using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.service_status` — dump every state that can strand an enlisted player, and
/// `taom.rescue_service` — clear the recoverable ones (stranded encounter, lost presence, missing
/// wait menu).
///
/// Built for the 2026-09-04 field report: *"the army left me behind after sieging East Osgiliath
/// making me unable to move and do anything, even the Enlist UI is gone."* That shape had a specific
/// cause, now fixed at the reconciler, but the reason this command exists is that the player was
/// stuck in a SAVE with no way out at all — no movement, no menu, so not even the discharge dialog.
/// A code fix ships to the next campaign; this ships to the save they are already in.
///
/// Same division as <see cref="TimeControlCheats"/>, deliberately: the status dump reads every
/// candidate in one shot instead of another round of decompile-and-guess, and the rescue is what a
/// player can be told to type over a support thread.
///
/// Boundary class, so <c>IoC.Resolve</c> is correct here rather than a violation: the engine
/// constructs the console command and there is no injection point. Same precedent as
/// <c>DiagnosticCheats</c> and <c>MissionSpawnCheats</c>.
/// </summary>
public static class EnlistmentRescueCheats
{
    private const string StatusUsage = "Format is \"taom.service_status\". No arguments.";
    private const string RescueUsage = "Format is \"taom.rescue_service\". No arguments.";

    [CommandLineFunctionality.CommandLineArgumentFunction("service_status", "taom")]
    public static string ServiceStatus(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, args =>
        {
            var store = IoC.Resolve<IEnlistmentStore>();
            var record = store?.Record;
            if (record == null)
                return "No enlistment record (the feature is not loaded).";

            var sb = new StringBuilder();
            sb.AppendLine($"State:            {record.State}");
            sb.AppendLine($"IsEnlisted:       {record.IsEnlisted}");
            sb.AppendLine($"Commander:        {record.CommanderHeroId ?? "<none>"}");
            sb.AppendLine($"OnTownLeave:      {record.OnTownLeave}");

            var main = MobileParty.MainParty;
            sb.AppendLine($"Main.IsActive:    {main?.IsActive.ToString() ?? "<no main party>"}");
            sb.AppendLine($"Main.IsVisible:   {main?.IsVisible.ToString() ?? "-"}");
            sb.AppendLine($"Main.Settlement:  {main?.CurrentSettlement?.StringId ?? "<none>"}");
            sb.AppendLine($"Main.MapEvent:    {(main?.MapEvent == null ? "<none>" : "IN ONE")}");
            sb.AppendLine($"Main.Army:        {main?.Army?.LeaderParty?.StringId ?? "<none>"}");

            // The one that strands people. An open encounter holds the map, blocks every future
            // encounter, and blocks ServiceMaintenanceService.TryBreakBattleLatch.
            sb.AppendLine($"PlayerEncounter:  {(PlayerEncounter.Current == null ? "<none>" : "OPEN")}");
            // PlayerEncounter.InsideSettlement is short-circuited by MainParty.IsActive, which is
            // false for a parked enlisted player, so it reports "false" for someone who IS inside a
            // settlement. Print both: the engine's answer, and the one that is actually true.
            sb.AppendLine($"  InsideSettlement: {(PlayerEncounter.Current == null ? "-" : PlayerEncounter.InsideSettlement.ToString())} (engine; false whenever the party is parked)");
            sb.AppendLine($"  EncounteredParty: {PlayerEncounter.EncounteredParty?.Id.ToString() ?? "<none>"}");

            var commander = IoC.Resolve<ICommanderLordAdapter>();
            var snapshot = record.CommanderHeroId == null ? null : commander?.GetSnapshot(record.CommanderHeroId);
            sb.AppendLine($"Commander alive:  {snapshot?.IsAlive.ToString() ?? "<unknown>"}");
            sb.AppendLine($"Commander party:  {snapshot?.PartyId ?? "<none>"}");
            sb.AppendLine($"Commander battle: {snapshot?.PartyIsInMapEvent.ToString() ?? "-"}");
            sb.AppendLine($"Menu:             {Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}");

            return sb.ToString();
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("rescue_service", "taom")]
    public static string RescueService(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, RescueUsage, args =>
        {
            var store = IoC.Resolve<IEnlistmentStore>();
            var record = store?.Record;
            if (record == null || !record.IsEnlisted)
                return "Not enlisted — nothing to rescue. For a frozen clock try taom.rescue_time.";

            var sb = new StringBuilder();

            // REFUSALS FIRST. Two states where a rescue would do more harm than the strand it is
            // meant to clear, and in both the player is demonstrably not stuck.
            if (Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
                return "You are in a conversation — close it first, then run this again.";
            if (MobileParty.MainParty?.MapEvent != null)
                return "You are in a battle — finish or leave it first, then run this again.";

            // ORDER MATTERS, and it is the same order the reconciler uses. The encounter comes down
            // first because it is what holds the map and what blocks the state machine; presence
            // second because the engine skips inactive parties in placement; the menu last, because
            // it is the only step that needs the other two to have landed.
            //
            // Deliberately NOT routed through EncounterOwnershipPolicy for the rest. Every rule in
            // that policy exists to stop an AUTOMATIC sweep from destroying something the player
            // owns, and this is not automatic: a human typed it because they are already stuck. The
            // two cases that genuinely matter are refused above instead, where they can be explained.
            if (PlayerEncounter.Current != null)
            {
                // NOT PlayerEncounter.InsideSettlement, which is a trap here. It reads
                // `if (MobileParty.MainParty.IsActive) return CurrentSettlement != null; return false;`
                // and an enlisted parked player has IsActive == false — so for the exact player this
                // command exists for it returns FALSE even with CurrentSettlement set. Passing that
                // as forcePlayerOutFromSettlement means Finish's own walk-out (guarded by the same
                // property) never runs, and the rescue leaves the party inside the settlement:
                // immobile, which is the thing it promises to fix, with the #510 menu crash armed.
                var insideSettlement = MobileParty.MainParty?.CurrentSettlement != null;
                sb.AppendLine($"Closing the open PlayerEncounter (insideSettlement={insideSettlement}).");
                var encounter = IoC.Resolve<IEncounterAdapter>();
                if (encounter == null || !encounter.Finish(insideSettlement))
                    sb.AppendLine("  WARNING: the encounter did not close. Save, reload, and run this again.");
            }

            // Walk out explicitly rather than trusting Finish's force flag, for the same reason
            // DischargeService.RestoreCampaignContext does: a party left with CurrentSettlement set
            // cannot move (MobileParty.DoUpdatePosition refuses) and arms the settlement-menu crash.
            var party = IoC.Resolve<IMobilePartyAttachmentAdapter>();
            if (MobileParty.MainParty?.CurrentSettlement != null)
            {
                sb.AppendLine($"Leaving '{MobileParty.MainParty.CurrentSettlement.StringId}' — a party left inside one cannot move.");
                if (party == null || !party.LeaveSettlement())
                    sb.AppendLine("  WARNING: could not leave the settlement.");
            }

            var attachment = IoC.Resolve<IServiceAttachmentService>();
            if (attachment == null)
                return sb + "Enlistment services are not loaded; cannot restore presence.";

            // Which way to put the player back depends on whether the commander is still fit. A dead
            // or captured commander must NOT be parked on: hand the player back to the map and let
            // the hourly reconciler run its own CommanderUnavailable path, which is the one that
            // knows about the grace period and the discharge.
            var commander = IoC.Resolve<ICommanderLordAdapter>();
            var snapshot = record.CommanderHeroId == null ? null : commander?.GetSnapshot(record.CommanderHeroId);
            var commanderFit = snapshot != null && snapshot.Exists && snapshot.IsAlive
                && !snapshot.IsPrisoner && snapshot.PartyIsActive;

            if (commanderFit)
            {
                sb.AppendLine($"Re-parking on commander '{record.CommanderHeroId}'.");
                if (!attachment.EnsureParked(record.CommanderHeroId))
                    sb.AppendLine("  WARNING: re-park failed.");
            }
            else
            {
                sb.AppendLine("Commander is not fit — restoring presence and leaving you on the map.");
                sb.AppendLine("  The hourly check will start the grace period or discharge you.");
                if (!attachment.RestorePresence())
                    sb.AppendLine("  WARNING: could not restore presence.");
            }

            var menu = IoC.Resolve<IGameMenuAdapter>();
            if (commanderFit && menu != null && !menu.EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId))
                sb.AppendLine("  WARNING: could not reopen the service menu.");

            sb.AppendLine("Done. If you still cannot move, run taom.service_status and report it.");
            return sb.ToString();
        });
}
