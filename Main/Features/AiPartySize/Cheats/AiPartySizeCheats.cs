using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.AiPartySize.Cheats;

/// <summary>
/// `taom.print_ai_food_relief` — what every AI lord party is actually eating, and why.
///
/// The relief is invisible from inside the game: an AI party's food consumption is never shown to
/// the player, so the only way anyone has ever measured it is by attaching a telemetry module and
/// reading `MobileParty.FoodChange` over a campaign. This prints the same picture on demand.
///
/// It answers two questions a raw FoodChange sweep cannot. First, whether a party is ELIGIBLE at
/// all: one failing <see cref="AiPartySizeService.IsScalableAiLordParty"/> never reaches the relief
/// and its residual is pure vanilla perks plus culture feats, which reads identically to a relief
/// that failed. Second, whether an eligible party landed inside the band the clamp promises, which
/// is the guarantee the composition rework exists to provide.
///
/// Tier A: read-only. It walks parties and reads model output. It writes nothing and arms nothing.
/// </summary>
public static class AiPartySizeCheats
{
    private const string Usage =
        "Format is \"taom.print_ai_food_relief [count]\".\n"
        + "Prints each AI party's daily food consumption as a fraction of the vanilla members/20\n"
        + "rate, split by whether the party is eligible for the AI food relief at all. Count caps\n"
        + "the per-party table (default 25); the summary always covers every party.";

    private const int DefaultTableRows = 25;

    [CommandLineFunctionality.CommandLineArgumentFunction("print_ai_food_relief", "taom")]
    public static string PrintAiFoodRelief(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            int limit = DefaultTableRows;
            if (args.Count > 0 && (!int.TryParse(args[0], out limit) || limit < 1))
                return "Expected a positive whole number of rows.\n" + Usage;

            var settings = TaomSettings.Instance;
            if (!(settings?.EnableAiPartyScaling ?? true))
                return "AI party scaling is disabled in MCM, so the relief never runs for any party. "
                     + "Nothing here would tell you about the relief itself.";

            float relief = settings?.AiFoodConsumptionRelief ?? AiPartySizeService.DefaultFoodRelief;
            var rows = Collect();

            return AiFoodReliefReport.Summarize(rows, relief)
                 + "\n" + AiFoodReliefReport.Table(rows, limit);
        });

    private static List<AiFoodReliefRow> Collect()
    {
        var rows = new List<AiFoodReliefRow>();
        var foodModel = Campaign.Current.Models.MobilePartyFoodConsumptionModel;

        foreach (var party in MobileParty.All)
        {
            // The vanilla predicate for "this party has a food bill at all". Without it the sweep
            // picks up garrisons, militia, caravans and villagers, none of which consume and all of
            // which would land at a meaningless residual.
            if (party == null || !foodModel.DoesPartyConsumeFood(party))
                continue;

            // `?.` throughout: TaleWorlds computed getters routinely throw before a null check, and a
            // console command that dies is a native unwind (see TaomConsole).
            float vanilla = -party.BaseFoodChange;
            if (!(vanilla > 0f))
                continue;

            rows.Add(new AiFoodReliefRow
            {
                PartyName = party.Name?.ToString() ?? party.StringId,
                ClanName = party.ActualClan?.StringId ?? "(no clan)",
                CultureName = party.LeaderHero?.Culture?.StringId ?? "(no culture)",
                Members = party.Party?.NumberOfAllMembers ?? 0,
                Eligible = AiPartySizeService.IsScalableAiLordParty(
                    party.IsMainParty,
                    party.IsLordParty,
                    party.LeaderHero != null,
                    Clan.PlayerClan != null && party.ActualClan == Clan.PlayerClan),
                Residual = -party.FoodChange / vanilla,
            });
        }

        return rows;
    }
}
