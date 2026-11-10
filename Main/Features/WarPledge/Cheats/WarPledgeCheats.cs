using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.DevConsole;

namespace TAOM.Features.WarPledge.Cheats;

/// <summary>
/// War Pledge's console commands (docs/features/dev-console.md, #1113), thin over
/// <see cref="WarPledgeCheatReport"/> and <see cref="WarPledgeService"/>. <c>taom.print_war_pledges</c> is tier A.
/// <para>
/// <c>taom.add_war_pledge &lt;kingdom id&gt; &lt;free|evil&gt; [confirm]</c> is tier C: the pledge, the wars it
/// declares and the peace it makes are saved, and no command reverts them. Without <c>confirm</c> it is a dry
/// run through the same checks; only the literal <c>confirm</c> acts, and the arguments are validated before any
/// lookup. It changes save-backed world state, so it runs only on the co-op authority, like the daily step.
/// Realm states: a live kingdom with no side and no pledge is the only one that joins. Refused: an id no live
/// kingdom has (an eliminated realm included), a realm that already pledged, a native member of a side, a realm
/// with a Hostile row to a live realm of that side, and any phase but Full War. A realm a player rules is
/// allowed, because the command is the player's own act. A realm at war with realms of its new side makes peace
/// with them. One allied with a realm of the other side loses that alliance when the war is declared (vanilla
/// <c>AllianceCampaignBehavior.OnWarDeclared</c>), at -100 relation between the two rulers, charged to the
/// pledged realm's ruler: a trait hit follows when that ruler is the player.
/// </para>
/// </summary>
public static class WarPledgeCheats
{
    [CommandLineFunctionality.CommandLineArgumentFunction("print_war_pledges", "taom")]
    public static string PrintWarPledges(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, WarPledgeCheatReport.StatusUsage, _ =>
            WarPledgeCheatReport.FormatStatus(
                IoC.Resolve<WarPledgeService>().Status(),
                IoC.Resolve<IKingdomWarSnapshotAdapter>().TryGetElapsedDay(out var day) ? day : (int?)null));

    [CommandLineFunctionality.CommandLineArgumentFunction("add_war_pledge", "taom")]
    public static string AddWarPledge(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, WarPledgeCheatReport.JoinUsage, args =>
        {
            var coop = IoC.Resolve<ICoopSessionProvider>();
            if (!coop.IsAuthority || coop.ShouldDeferToHost || !CoopSessionPolicy.MayWriteSaveBackedState(coop.IsCoopClient))
                return "Refused: only the co-op host adds a pledge.";

            if (!WarPledgeCheatReport.TryReadJoinArgs(args, out var id, out var side, out var confirmed, out var error))
                return error;

            var acted = IoC.Resolve<WarPledgeService>().TryJoin(id, side, byAi: false, dryRun: !confirmed, out var reason);
            return WarPledgeCheatReport.FormatJoin(id, side, dryRun: !confirmed, acted, reason);
        });
}
