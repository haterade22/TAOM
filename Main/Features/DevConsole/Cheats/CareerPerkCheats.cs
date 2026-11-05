using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Diagnostics;
using TAOM.Features.CareerSystem.Domain;

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

    // The six kinded hits plus the two kindless ones (an Invalid DamageType with none of vanilla's
    // blunt-by-rule conditions), so a kind-only pip's zero against a kindless hit is visible.
    private static readonly AttackTypeMask[] HitMasks =
    {
        AttackTypeMask.Melee,
        AttackTypeMask.Ranged,
        AttackTypeMask.Melee | AttackTypeMask.Cut,
        AttackTypeMask.Melee | AttackTypeMask.Pierce,
        AttackTypeMask.Melee | AttackTypeMask.Blunt,
        AttackTypeMask.Ranged | AttackTypeMask.Cut,
        AttackTypeMask.Ranged | AttackTypeMask.Pierce,
        AttackTypeMask.Ranged | AttackTypeMask.Blunt,
    };

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

        var passives = IoC.Resolve<ICareerPassiveService>();
        foreach (PassiveEffectType type in Enum.GetValues(typeof(PassiveEffectType)))
        {
            var magnitude = passives.GetPassiveMagnitude(hero.StringId, type);
            if (magnitude == 0f) continue;
            snapshot.Rows.Add(new CareerPerkRow(type, magnitude, MaskedBuckets(passives, hero.StringId, type)));
        }

        GatherProbes(snapshot, hero);
        snapshot.Mission = GatherMission(hero);
        return snapshot;
    }

    // Damage and Resistance are mask-gated: show the effective magnitude per hit kind, collapsed
    // to one "All" entry when every kind reads the same.
    private static List<(AttackTypeMask, float)>? MaskedBuckets(ICareerPassiveService passives, string heroId, PassiveEffectType type)
    {
        if (type != PassiveEffectType.Damage && type != PassiveEffectType.Resistance)
            return null;

        var buckets = new List<(AttackTypeMask, float)>(HitMasks.Length);
        var allSame = true;
        float first = float.NaN;
        foreach (var hit in HitMasks)
        {
            var value = passives.GetMaskedMagnitude(heroId, type, hit);
            if (float.IsNaN(first)) first = value;
            else if (value != first) allSame = false;
            buckets.Add((hit, value));
        }

        if (allSame)
            return new List<(AttackTypeMask, float)> { (AttackTypeMask.All, first) };
        return buckets;
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
        Probe(snapshot, "party size limit", () => models.PartySizeLimitModel.GetPartyMemberSizeLimit(party.Party, true));
        Probe(snapshot, "morale", () => party.MoraleExplained);
        Probe(snapshot, "wages", () => models.PartyWageModel.GetTotalWage(party, party.MemberRoster, true));
        Probe(snapshot, "inventory capacity", () => models.InventoryCapacityModel.CalculateInventoryCapacity(party, party.IsCurrentlyAtSea, true));
        Probe(snapshot, "hero daily healing", () => models.PartyHealingModel.GetDailyHealingHpForHeroes(party.Party, false, true));
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

    private static CareerPerkMissionSnapshot? GatherMission(Hero hero)
    {
        var mission = Mission.Current;
        if (mission == null)
            return null;

        var block = new CareerPerkMissionSnapshot();
        var agent = mission.MainAgent;
        if (agent == null || !agent.IsActive() || !CareerHeroIdentityGate.IsCareerHeroAgent(agent, hero))
            return block;

        block.PlayerAgentAlive = true;
        var props = agent.AgentDrivenProperties;
        if (props != null)
        {
            block.SwingSpeedMultiplier = props.SwingSpeedMultiplier;
            block.MaxSpeedMultiplier = props.MaxSpeedMultiplier;
            block.DamageMultiplierBonus = props.DamageMultiplierBonus;
            block.ReadySpeedMultiplier = props.ThrustOrRangedReadySpeedMultiplier;
            block.ArmorEncumbrance = props.ArmorEncumbrance;
        }

        var mount = agent.MountAgent;
        if (mount != null && mount.AgentDrivenProperties != null)
        {
            block.HasMount = true;
            block.MountChargeDamage = mount.AgentDrivenProperties.MountChargeDamage;
            block.MountSpeed = mount.AgentDrivenProperties.MountSpeed;
            block.MountHealthLimit = mount.HealthLimit;
        }

        for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
        {
            var weapon = agent.Equipment[slot];
            if (weapon.IsEmpty || !weapon.IsAnyConsumable()) continue;
            block.AmmoSlots.Add($"slot {(int)slot} {weapon.Item?.StringId} {weapon.Amount}/{weapon.ModifiedMaxAmount}");
        }

        var buff = CareerAbilityBuffTracker.GetBuff(hero.StringId);
        block.LiveBuff = buff == null ? null : ActiveBuffsFormat.Describe(buff);
        return block;
    }
}
