using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Diagnostics;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// <c>taom.career_perks</c> (#613): what the player's career passives are, where each is consumed,
/// and what the engine currently holds for the ones a probe can reach. Every line is printed to
/// the console and written to the TAOM debug log with the <c>[CareerPerks]</c> prefix, so a play
/// session leaves the same evidence the console showed. The campaign probes read the engine's own
/// ExplainedNumbers and name the "Career" line when it is there; the mission block reads the
/// player agent's driven properties, its mount's, the consumable slots and the live buff.
///
/// Boundary: gathers into a <see cref="CareerPerkSnapshot"/>, renders through
/// <see cref="CareerPerkReport"/>. Each probe is wrapped on its own, a throwing model must not
/// cost the rest of the report.
/// </summary>
public static class CareerPerkCheats
{
    private const string Usage =
        "Format is \"taom.career_perks\".\n"
        + "Lists the player's career passives with their consumers, probes the campaign numbers that\n"
        + "carry a Career line, and in a battle reads the player agent's driven properties. Every line\n"
        + "is also written to the TAOM debug log as [CareerPerks].";

    [CommandLineFunctionality.CommandLineArgumentFunction("career_perks", "taom")]
    public static string CareerPerks(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, _ =>
        {
            var snapshot = Gather();
            var lines = CareerPerkReport.Render(snapshot);

            var logger = IoC.Resolve<IModLogger>();
            foreach (var line in lines)
                logger.LogInfo(CareerPerkReport.Prefix + " " + line.TrimStart());

            return string.Join("\n", lines);
        });

    private static CareerPerkSnapshot Gather()
    {
        var snapshot = new CareerPerkSnapshot();
        var hero = Hero.MainHero;
        if (hero == null)
            return snapshot;

        snapshot.HeroId = hero.StringId;
        snapshot.HeroName = hero.Name?.ToString();
        snapshot.Level = hero.Level;

        var data = IoC.Resolve<ICareerDataService>();
        snapshot.HasCareer = data.HasCareer(hero.StringId);
        if (!snapshot.HasCareer)
            return snapshot;
        snapshot.CareerId = data.GetCareerStringId(hero.StringId);

        snapshot.Rows.AddRange(CareerPerkRows.Gather(IoC.Resolve<ICareerPassiveService>(), hero.StringId));

        GatherProbes(snapshot, hero);
        snapshot.Mission = CareerPerkMissionProbe.Gather(hero);
        return snapshot;
    }

    private static void GatherProbes(CareerPerkSnapshot snapshot, Hero hero)
    {
        var models = Campaign.Current?.Models;
        var party = MobileParty.MainParty;
        if (models == null || party == null)
            return;

        Probe(snapshot, "max hitpoints", () => models.CharacterStatsModel.MaxHitpoints(hero.CharacterObject, true));
        Probe(snapshot, "party speed", () => party.SpeedExplained);
        Probe(snapshot, "seeing range", () => party.SeeingRangeExplanation);
        Probe(snapshot, "party size limit (tooltip value)", () => models.PartySizeLimitModel.GetPartyMemberSizeLimit(party.Party, true));
        // The tooltip value above skips the TroopWeight penalty and is not cut to an int. The next line is
        // what the engine enforces if it recomputed now: PartyBase.PartySizeLimit is
        // (int)GetPartyMemberSizeLimit(this, false).ResultNumber. Only a gap between that value and the
        // cached line below means the cache is stale until a roster bump (#768).
        try
        {
            snapshot.Probes.Add($"party size limit (enforcement value now) {(int)models.PartySizeLimitModel.GetPartyMemberSizeLimit(party.Party, false).ResultNumber}");
        }
        catch (Exception ex)
        {
            snapshot.Probes.Add($"party size limit (enforcement value now): {ex.GetType().Name} {ex.Message}");
        }
        try
        {
            snapshot.Probes.Add($"party size limit (engine cache) {party.Party.PartySizeLimit}");
        }
        catch (Exception ex)
        {
            snapshot.Probes.Add($"party size limit (engine cache): {ex.GetType().Name} {ex.Message}");
        }
        Probe(snapshot, "morale", () => party.MoraleExplained);
        Probe(snapshot, "wages", () => models.PartyWageModel.GetTotalWage(party, party.MemberRoster, true));
        Probe(snapshot, "inventory capacity", () => models.InventoryCapacityModel.CalculateInventoryCapacity(party, party.IsCurrentlyAtSea, true));
        Probe(snapshot, "hero daily healing", () => models.PartyHealingModel.GetDailyHealingHpForHeroes(party.Party, false, true));
        Probe(snapshot, "daily food change", () => party.FoodChangeExplained);
        Probe(snapshot, "regulars daily healing", () => party.Party.HealingRateForMemberRegularsExplained);
        Probe(snapshot, "clan daily gold change", () => models.ClanFinanceModel.CalculateClanGoldChange(Clan.PlayerClan, true));
        try
        {
            snapshot.Probes.Add($"companion limit {models.ClanTierModel.GetCompanionLimit(Clan.PlayerClan)}");
        }
        catch (Exception ex)
        {
            snapshot.Probes.Add($"companion limit: {ex.GetType().Name} {ex.Message}");
        }
    }

    private static void Probe(CareerPerkSnapshot snapshot, string label, Func<ExplainedNumber> read)
    {
        try
        {
            var number = read();
            string? career = null;
            foreach (var (name, value) in number.GetLines())
            {
                if (name != null && name.IndexOf("Career", StringComparison.OrdinalIgnoreCase) >= 0)
                    career = $"Career line {(value >= 0f ? "+" : "")}{value.ToString("0.##", CultureInfo.InvariantCulture)}";
            }
            snapshot.Probes.Add($"{label} {number.ResultNumber.ToString("0.##", CultureInfo.InvariantCulture)} ({career ?? "no Career line"})");
        }
        catch (Exception ex)
        {
            snapshot.Probes.Add($"{label}: {ex.GetType().Name} {ex.Message}");
        }
    }
}
