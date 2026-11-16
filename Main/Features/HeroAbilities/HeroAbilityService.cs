using System;
using System.Collections.Generic;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAbilities;

/// <summary>
/// Every decision behind the hero abilities (#1359): numbers, flags and TAOM types in, no TaleWorlds types
/// (ADR-007). The triggers are the race abilities' (<see cref="RaceAbilityService.FiringTrigger"/>), so an ability is
/// a synthetic <see cref="RaceAbilityProfile"/>. Engine floats can arrive as NaN, so each gate is a positive
/// requirement that fails closed.
/// </summary>
public sealed class HeroAbilityService
{
    // One decision per hero per second, the race abilities' cadence.
    public const float PassSeconds = 1f;

    // A hero in contact for ContactSecondsToJudge seconds who never fired fails the in-game check.
    public const float ContactSecondsToJudge = 20f;

    private readonly IHeroAbilitiesConfigProvider _provider;
    private readonly RaceAbilityService _raceAbilities;
    private readonly Dictionary<string, RaceAbilityProfile> _profiles = new Dictionary<string, RaceAbilityProfile>(StringComparer.Ordinal);

    public HeroAbilityService(IHeroAbilitiesConfigProvider provider, RaceAbilityService raceAbilities)
    {
        _provider = provider;
        _raceAbilities = raceAbilities;
    }

    public bool ConfigEnabled => _provider.GetConfig().Enabled;

    public int KitCount => _provider.GetConfig().Heroes.Count;

    public HeroKitConfig? KitFor(string? characterId)
    {
        if (string.IsNullOrEmpty(characterId))
            return null;
        foreach (var kit in _provider.GetConfig().Heroes)
            if (string.Equals(kit.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                return kit;
        return null;
    }

    // Main thread only; one profile per ability id, built on first use.
    public RaceAbilityProfile ProfileOf(HeroAbilityConfig ability)
    {
        if (_profiles.TryGetValue(ability.Id, out var profile))
            return profile;
        profile = new RaceAbilityProfile
        {
            AbilityId = ability.Id,
            CooldownSeconds = ability.CooldownSeconds,
            DurationSeconds = ability.DurationSeconds,
            Requires = ability.Requires,
            AnyOf = ability.AnyOf,
        };
        _profiles[ability.Id] = profile;
        return profile;
    }

    // The kit's easiest enemy trigger: among every EnemiesWithin trigger of every ability, Requires and AnyOf alike, the
    // smallest count (a tie takes the larger range). The host counts contact against it, apart from the sensor.
    public bool TryContactRule(HeroKitConfig kit, out int count, out float range)
    {
        count = 0;
        range = 0f;
        var found = false;
        foreach (var ability in kit.Abilities)
        {
            Consider(ability.Requires, ref found, ref count, ref range);
            Consider(ability.AnyOf, ref found, ref count, ref range);
        }
        return found;
    }

    private static void Consider(List<RaceAbilityTrigger> triggers, ref bool found, ref int count, ref float range)
    {
        foreach (var trigger in triggers)
        {
            if (trigger.ParsedKind != RaceAbilityTriggerKind.EnemiesWithin)
                continue;
            if (!found || trigger.Count < count || (trigger.Count == count && trigger.Range > range))
            {
                found = true;
                count = trigger.Count;
                range = trigger.Range;
            }
        }
    }

    public bool Fires(RaceAbilityProfile profile, RaceAbilitySenses senses) =>
        _raceAbilities.FiringTrigger(profile, senses) != null;

    // The first ability in config order that is off cooldown and fires, or -1. A NaN clock decides nothing.
    public int PickAbility(IReadOnlyList<HeroAbilityConfig> abilities, IReadOnlyList<float?> lastFiredAt, float now, Func<int, bool> fires)
    {
        if (float.IsNaN(now))
            return -1;
        for (var i = 0; i < abilities.Count; i++)
        {
            if (_raceAbilities.IsOffCooldown(lastFiredAt[i], now, abilities[i].CooldownSeconds) && fires(i))
                return i;
        }
        return -1;
    }

    // Only the AI's own hero decides: never the player's body, never a puppet, never one running away.
    public bool ShouldDecide(bool isAiControlled, bool isPlayerControlled, bool isMainAgent, bool isRetreating) =>
        isAiControlled && !isPlayerControlled && !isMainAgent && !isRetreating;

    // What one ally gains at the cast: the gain, topped up to the floor, never past 100. An unknown morale (the player,
    // below 0) or a NaN gets nothing.
    public float AllyMoraleGain(float morale, float gain, float floor)
    {
        if (!(morale >= 0f) || float.IsInfinity(morale))
            return 0f;
        var wanted = gain > 0f ? gain : 0f;
        var wantedFloor = floor > 0f ? floor : 0f;
        var gained = Math.Min(wanted, 100f - morale);
        return Math.Max(Math.Max(gained, wantedFloor - morale), 0f);
    }

    // The hero takes his own effects and his allies' together.
    public RaceAbilityEffects SelfEffects(HeroAbilityConfig ability) =>
        RaceAbilityEffectsMerge.Combine(ability.SelfEffects, ability.AllyEffects) ?? ability.SelfEffects;

    public bool IsActive(float? lastFiredAt, float now, float duration)
    {
        if (!lastFiredAt.HasValue)
            return false;
        var elapsed = now - lastFiredAt.Value;
        return elapsed >= 0f && elapsed < duration;
    }
}
