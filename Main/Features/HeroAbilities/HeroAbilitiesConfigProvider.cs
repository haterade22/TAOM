using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.HeroAuras;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.SceneScripts.Roads;

namespace TAOM.Features.HeroAbilities;

/// <summary>
/// Validating loader for <c>hero_abilities/hero_abilities.json</c> (#1359). A missing file gives no kits and a
/// warning, a parse failure no kits and an error. An ability with a bad id, a cooldown outside 60 to 120 s, a duration
/// outside (0, cooldown] or no usable trigger is dropped; a bad radius or morale reverts to 0; an effect only
/// strengthens (<see cref="HeroEffectsCheck"/>) and is capped. A kit or ability id may appear once in the file. The
/// triggers that read kin (a synthetic profile has none) are skipped. One summary warning follows any rejection.
/// </summary>
public sealed class HeroAbilitiesConfigProvider : IHeroAbilitiesConfigProvider
{
    // Mission.GetNearbyAllyAgents searches the proximity grid up to 15 m and walks every agent past it.
    private const float MaxRadius = 15f;
    private const float MaxMorale = 50f;
    private const float MinCooldown = 60f;
    private const float MaxCooldown = 120f;
    private const float MaxDamageReduction = 90f;
    private const float MaxResistance = 1000f;
    private const float MaxOtherPercent = 300f;
    private const float MaxExtraArrowSpread = 20f;
    private const float MaxExtraArrowRange = 150f;
    private const int MaxExtraArrows = 3;

    private static readonly Regex AbilityId = new Regex("^[a-z0-9_]{1,63}$", RegexOptions.CultureInvariant);

    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<HeroAbilitiesConfig> _config;
    private bool _rejected;

    public HeroAbilitiesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<HeroAbilitiesConfig>(Load);
    }

    public HeroAbilitiesConfig GetConfig() => _config.Value;

    private HeroAbilitiesConfig Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "hero_abilities", "hero_abilities.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"HeroAbilitiesConfigProvider: hero_abilities.json not found at {path}, no hero has an ability");
            return new HeroAbilitiesConfig();
        }

        HeroAbilitiesConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<HeroAbilitiesConfig>(File.ReadAllText(path), SerializerSettings) ?? new HeroAbilitiesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"HeroAbilitiesConfigProvider: failed to parse hero_abilities.json: {ex.Message}");
            return new HeroAbilitiesConfig();
        }

        var config = Validate(parsed);
        _logger.LogInfo($"HeroAbilitiesConfigProvider: loaded hero_abilities.json, {config.Heroes.Count} kit(s)");
        return config;
    }

    private HeroAbilitiesConfig Validate(HeroAbilitiesConfig parsed)
    {
        var config = new HeroAbilitiesConfig { Enabled = parsed.Enabled };
        var characterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var abilityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kit in parsed.Heroes ?? new List<HeroKitConfig>())
        {
            var checkedKit = ValidateKit(kit, characterIds, abilityIds);
            if (checkedKit != null)
                config.Heroes.Add(checkedKit);
        }

        if (_rejected)
            _logger.LogWarning("HeroAbilitiesConfigProvider: hero_abilities.json contained invalid values. See prior warnings for details.");
        return config;
    }

    private HeroKitConfig? ValidateKit(HeroKitConfig? kit, HashSet<string> characterIds, HashSet<string> abilityIds)
    {
        var characterId = kit?.CharacterId?.Trim();
        if (kit == null || string.IsNullOrEmpty(characterId))
        {
            Reject("a kit has no characterId and was skipped");
            return null;
        }
        if (!characterIds.Add(characterId!))
        {
            Reject($"kit '{characterId}' repeats an earlier kit's characterId and was skipped");
            return null;
        }

        var result = new HeroKitConfig
        {
            CharacterId = characterId!,
            Passive = ValidateEffects($"hero '{characterId}' passive", kit.Passive),
        };
        result.Glow = ValidateGlow(kit.Glow, characterId!, out var color);
        result.GlowColor = color;
        result.Ranged = ValidateRanged(kit.Ranged, characterId!);
        foreach (var ability in kit.Abilities ?? new List<HeroAbilityConfig>())
        {
            var checkedAbility = ValidateAbility(ability, abilityIds, result.Ranged);
            if (checkedAbility != null)
                result.Abilities.Add(checkedAbility);
        }
        return result;
    }

    private HeroRangedConfig? ValidateRanged(HeroRangedConfig? ranged, string characterId)
    {
        if (ranged == null)
            return null;
        var label = $"hero '{characterId}' ranged";
        var item = (ranged.ExtraArrowItem ?? "").Trim();
        if (item.Length > 0 && !AbilityId.IsMatch(item))
        {
            Reject($"{label}: extraArrowItem '{item}' must be 1 to 63 of a-z, 0-9 and _, set to empty");
            item = "";
        }
        return new HeroRangedConfig
        {
            ZeroAimError = ranged.ZeroAimError,
            PierceShields = ranged.PierceShields,
            HeadshotKillChance = InRange(ranged.HeadshotKillChance, 1f, $"{label}: headshotKillChance"),
            BodyKillChance = InRange(ranged.BodyKillChance, 1f, $"{label}: bodyKillChance"),
            ExtraArrowItem = item,
            ExtraArrowSpreadMeters = InRange(ranged.ExtraArrowSpreadMeters, MaxExtraArrowSpread, $"{label}: extraArrowSpreadMeters"),
            ExtraArrowRangeMeters = InRange(ranged.ExtraArrowRangeMeters, MaxExtraArrowRange, $"{label}: extraArrowRangeMeters"),
        };
    }

    private int ValidateExtraArrows(HeroAbilityConfig ability, string id, HeroRangedConfig? ranged)
    {
        if (ability.ExtraArrows == 0)
            return 0;
        if (ability.ExtraArrows < 0 || ability.ExtraArrows > MaxExtraArrows)
        {
            Reject($"ability '{id}': extraArrows {ability.ExtraArrows} is outside 0 to {MaxExtraArrows}, set to 0");
            return 0;
        }
        if (ranged == null || ranged.ExtraArrowItem.Length == 0 || !(ranged.ExtraArrowSpreadMeters > 0f) || !(ranged.ExtraArrowRangeMeters > 0f))
        {
            Reject($"ability '{id}': extraArrows needs a ranged block with an extraArrowItem, spread and range above 0, set to 0");
            return 0;
        }
        return ability.ExtraArrows;
    }

    private HeroAbilityConfig? ValidateAbility(HeroAbilityConfig? ability, HashSet<string> abilityIds, HeroRangedConfig? ranged)
    {
        var id = ability?.Id ?? "";
        if (ability == null || !AbilityId.IsMatch(id))
        {
            Reject($"ability id '{id}' must be 1 to 63 of a-z, 0-9 and _, ability skipped");
            return null;
        }
        if (!abilityIds.Add(id))
        {
            Reject($"ability '{id}' repeats an earlier ability's id and was skipped");
            return null;
        }
        if (!FiniteFloatValidator.IsFiniteInRange(ability.CooldownSeconds, MinCooldown, MaxCooldown))
        {
            Reject($"ability '{id}': cooldownSeconds {ability.CooldownSeconds} must be a finite value in [{MinCooldown},{MaxCooldown}], ability skipped");
            return null;
        }
        if (!(FiniteFloatValidator.IsFinite(ability.DurationSeconds) && ability.DurationSeconds > 0f && ability.DurationSeconds <= ability.CooldownSeconds))
        {
            Reject($"ability '{id}': durationSeconds {ability.DurationSeconds} must be a finite value in (0,cooldownSeconds], ability skipped");
            return null;
        }

        var requires = ValidateTriggers(ability.Requires, $"ability '{id}'.requires");
        var anyOf = ValidateTriggers(ability.AnyOf, $"ability '{id}'.anyOf");
        if (requires.Count == 0 && anyOf.Count == 0)
        {
            Reject($"ability '{id}' has no valid trigger (use kind Always for a plain timer), ability skipped");
            return null;
        }

        return new HeroAbilityConfig
        {
            Id = id,
            CooldownSeconds = ability.CooldownSeconds,
            DurationSeconds = ability.DurationSeconds,
            Requires = requires,
            AnyOf = anyOf,
            Radius = InRange(ability.Radius, MaxRadius, $"ability '{id}': radius"),
            AllyEffects = ValidateEffects($"ability '{id}' allyEffects", ability.AllyEffects),
            SelfEffects = ValidateEffects($"ability '{id}' selfEffects", ability.SelfEffects),
            AllyMorale = InRange(ability.AllyMorale, MaxMorale, $"ability '{id}': allyMorale"),
            EnemyShockRadius = InRange(ability.EnemyShockRadius, MaxRadius, $"ability '{id}': enemyShockRadius"),
            EnemyShockMorale = InRange(ability.EnemyShockMorale, MaxMorale, $"ability '{id}': enemyShockMorale"),
            WarCry = ValidateWarCry(ability.WarCry, id),
            ExtraArrows = ValidateExtraArrows(ability, id, ranged),
            AlliesRangedOnly = ability.AlliesRangedOnly,
        };
    }

    private List<RaceAbilityTrigger> ValidateTriggers(List<RaceAbilityTrigger>? parsed, string field)
    {
        var triggers = RaceAbilityTriggerCheck.ValidateAll(parsed, field, Reject);
        triggers.RemoveAll(t =>
        {
            var reads = t.ParsedKind == RaceAbilityTriggerKind.KinWithin || t.ParsedKind == RaceAbilityTriggerKind.KinFell
                || t.ParsedKind == RaceAbilityTriggerKind.LandedKill;
            if (reads)
                Reject($"{field}: trigger kind '{t.Kind}' needs kin soldiers or a kill record a hero kit does not have, trigger skipped");
            return reads;
        });
        return triggers;
    }

    private RaceAbilityEffects ValidateEffects(string label, RaceAbilityEffects? parsed)
    {
        var e = HeroEffectsCheck.Validate(label, parsed ?? new RaceAbilityEffects(), 100f, Reject);
        e.DamageReductionPercent = Cap(label, "damageReductionPercent", e.DamageReductionPercent, MaxDamageReduction);
        e.KnockdownResistancePercent = Cap(label, "knockdownResistancePercent", e.KnockdownResistancePercent, MaxResistance);
        e.KnockbackResistancePercent = Cap(label, "knockbackResistancePercent", e.KnockbackResistancePercent, MaxResistance);
        e.DismountResistancePercent = Cap(label, "dismountResistancePercent", e.DismountResistancePercent, MaxResistance);
        e.MoveSpeedPercent = Cap(label, "moveSpeedPercent", e.MoveSpeedPercent, MaxOtherPercent);
        e.AccelerationPercent = Cap(label, "accelerationPercent", e.AccelerationPercent, MaxOtherPercent);
        e.SwingSpeedPercent = Cap(label, "swingSpeedPercent", e.SwingSpeedPercent, MaxOtherPercent);
        e.DrawSpeedPercent = Cap(label, "drawSpeedPercent", e.DrawSpeedPercent, MaxOtherPercent);
        e.ReloadSpeedPercent = Cap(label, "reloadSpeedPercent", e.ReloadSpeedPercent, MaxOtherPercent);
        e.MissileSpeedPercent = Cap(label, "missileSpeedPercent", e.MissileSpeedPercent, MaxOtherPercent);
        e.MountSpeedPercent = Cap(label, "mountSpeedPercent", e.MountSpeedPercent, MaxOtherPercent);
        e.MeleeDamagePercent = Cap(label, "meleeDamagePercent", e.MeleeDamagePercent, MaxOtherPercent);
        e.RangedDamagePercent = Cap(label, "rangedDamagePercent", e.RangedDamagePercent, MaxOtherPercent);
        e.BlockAbilityPercent = Cap(label, "blockAbilityPercent", e.BlockAbilityPercent, MaxOtherPercent);
        e.ParryAbilityPercent = Cap(label, "parryAbilityPercent", e.ParryAbilityPercent, MaxOtherPercent);
        e.AttackEagernessPercent = Cap(label, "attackEagernessPercent", e.AttackEagernessPercent, MaxOtherPercent);
        return e;
    }

    // HeroEffectsCheck has already made the value finite and not negative, so only the upper bound is left.
    private float Cap(string label, string name, float value, float max)
    {
        if (value <= max)
            return value;
        Reject($"{label}: {name} {value} is above {max}, set to 0");
        return 0f;
    }

    private float InRange(float value, float max, string name)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, 0f, max))
            return value;
        Reject($"{name} {value} is outside 0 to {max}, set to 0");
        return 0f;
    }

    private string ValidateGlow(string? value, string characterId, out uint? color)
    {
        color = null;
        var glow = (value ?? "").Trim();
        if (glow.Length == 0)
            return "";
        if (HexColorParser.TryParseRgb(glow, out var argb))
        {
            color = argb;
            return "#" + glow.Substring(1).ToUpperInvariant();
        }
        Reject($"hero '{characterId}': glow '{value}' is not a #RRGGBB colour, no outline");
        return "";
    }

    private string ValidateWarCry(string? value, string abilityId)
    {
        foreach (var cry in RaceAbilitiesConfigProvider.WarCries)
            if (string.Equals(cry, value ?? "", StringComparison.OrdinalIgnoreCase))
                return cry;
        Reject($"ability '{abilityId}': warCry '{value}' is not one of Yell, Charge, Victory, Grunt or empty, silenced");
        return "";
    }

    private void Reject(string message)
    {
        _rejected = true;
        _logger.LogWarning($"HeroAbilitiesConfigProvider: {message}");
    }
}
