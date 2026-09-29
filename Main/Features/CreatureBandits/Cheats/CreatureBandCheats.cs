using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.CreatureBandits.Cheats;

/// <summary>
/// `taom.spawn_creature_band trolls|broods [confirm]` (#694 testing): spawns one Wild Troll band or one spider brood
/// beside the player's party, within a quarter of the player's sight so it always shows on the map (a small band is
/// spotted only inside about 0.6 of sight, 0.4 in forest). It goes through the daily spawners' own
/// <see cref="CreatureBandParties.Spawn"/> (template, patrol, diagnostics), homed on the town, castle or village
/// nearest the player: a brood patrols there, even outside Mirkwood, and a troll band counts for that settlement's
/// kingdom, if any. Either takes a slot of its spawner's cap.
///
/// Tier C (dev-console.md): the party is saved with the campaign and no console command removes it. The bare command
/// is a dry run; only the literal `confirm` spawns. Entity states: refused before the campaign has started (character
/// creation), while the player is a prisoner, in a battle, or in a siege on either side (<c>PlayerSiege</c>, which also
/// sees a defender); allowed inside a settlement (the band patrols around it) and in an army.
/// </summary>
public static class CreatureBandCheats
{
    private const string Usage =
        "Format is \"taom.spawn_creature_band trolls|broods [confirm]\".\n"
        + "Spawns one Wild Troll band or one spider brood beside your party, homed on your nearest town, castle or\n"
        + "village. Without 'confirm' it only reports what it would do: the band is saved with the campaign, only\n"
        + "killing it removes it, and it takes a slot of the daily spawner's cap.";

    [CommandLineFunctionality.CommandLineArgumentFunction("spawn_creature_band", "taom")]
    public static string SpawnCreatureBand(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            if (!TryParse(args, out bool trolls, out bool confirmed))
                return "Expected 'trolls' or 'broods', then optionally 'confirm'.\n" + Usage;

            string kind = trolls ? "Wild Troll band" : "spider brood";
            string clanId = trolls ? CreatureBanditsConfig.TrollClanId : CreatureBanditsConfig.BroodClanId;
            var clan = Clan.All.FirstOrDefault(c => c.StringId == clanId);
            if (clan?.DefaultPartyTemplate == null)
                return $"No '{clanId}' clan in this campaign: it was started before the clan existed. Start a new campaign.";

            var player = MobileParty.MainParty;
            if (!Campaign.Current.GameStarted || Hero.MainHero.IsPrisoner)
                return "Refused: your party is not on the map yet, or you are a prisoner.";
            if (player.MapEvent != null || PlayerSiege.PlayerSiegeEvent != null)
                return "Refused: your party is in a battle or a siege. Try again once it is over.";

            var anchor = SettlementHelper.FindNearestSettlementToMobileParty(player, MobileParty.NavigationType.Default,
                s => s.IsTown || s.IsCastle || s.IsVillage);
            if (anchor == null)
                return "Refused: no town, castle or village is reachable from your party.";

            // A troll band counts only for a kingdom (Clan.MapFaction is the clan itself outside one).
            var kingdom = anchor.MapFaction as Kingdom;
            string where = $"{anchor.Name} ({anchor.StringId})"
                           + (trolls ? $", counting for {kingdom?.Name?.ToString() ?? "no kingdom (a cap slot only)"}" : "");
            if (!confirmed)
                return $"Dry run: would spawn one {kind} beside you, homed on {where}. "
                     + $"Run 'taom.spawn_creature_band {(trolls ? "trolls" : "broods")} confirm' to spawn it.";

            var at = NavigationHelper.FindPointAroundPosition(player.Position, MobileParty.NavigationType.Default, player.SeeingRange / 4f);
            var band = CreatureBandParties.Spawn(clan, anchor, at);
            string roster = string.Join(", ", band.MemberRoster.GetTroopRoster().Select(e => $"{e.Number} {e.Character.Name}"));
            return $"Spawned {kind} '{band.StringId}' ({roster}) {band.Position.Distance(player.Position):0.0} map units "
                 + $"from you (sight {player.SeeingRange:0.0}), homed on {where}.";
        });

    /// <summary>The kind (trolls or broods) and the optional literal confirm token; anything else is refused.</summary>
    internal static bool TryParse(List<string> args, out bool trolls, out bool confirmed)
    {
        trolls = confirmed = false;
        if (args.Count < 1 || args.Count > 2) return false;
        switch (args[0].ToLowerInvariant())
        {
            case "trolls": trolls = true; break;
            case "broods": break;
            default: return false;
        }
        if (args.Count == 1) return true;
        confirmed = string.Equals(args[1], "confirm", StringComparison.Ordinal);
        return confirmed;
    }
}
