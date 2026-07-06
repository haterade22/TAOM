using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerConfigProvider : ICareerConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    private List<CareerDefinition> _careers;
    private List<CareerChoiceGroupDefinition> _groups;
    private List<CareerChoiceDefinition> _choices;
    private int _maxPerkPoints = 30;
    private Dictionary<string, AbilityTemplateData> _abilityTemplates;
    private AbilityTuningConfig _abilityTuning;

    public CareerConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<CareerDefinition> LoadCareers()
    {
        EnsureLoaded();
        return _careers;
    }

    public IReadOnlyList<CareerChoiceGroupDefinition> LoadChoiceGroups()
    {
        EnsureLoaded();
        return _groups;
    }

    public IReadOnlyList<CareerChoiceDefinition> LoadChoices()
    {
        EnsureLoaded();
        return _choices;
    }

    public int GetMaxPerkPoints()
    {
        EnsureLoaded();
        return _maxPerkPoints;
    }

    public AbilityTemplateData GetAbilityTemplate(string templateId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(templateId)) return null;
        return _abilityTemplates.TryGetValue(templateId, out var t) ? t : null;
    }

    public AbilityTuningConfig GetAbilityTuning()
    {
        EnsureLoaded();
        return _abilityTuning;
    }

    private void EnsureLoaded()
    {
        if (_careers != null) return;

        _logger.LogInfo("CareerSystem: Loading career config...");

        _careers = new List<CareerDefinition>();
        _groups = new List<CareerChoiceGroupDefinition>();
        _choices = new List<CareerChoiceDefinition>();
        _abilityTemplates = new Dictionary<string, AbilityTemplateData>();

        LoadCareersXml();
        LoadChoicesXml();
        ValidatePassiveConsumers();
        LoadAbilityTemplatesXml();
        LoadAbilityTuningXml();

        _logger.LogInfo($"CareerSystem: Loaded {_careers.Count} careers, {_groups.Count} groups, {_choices.Count} choices, maxPerkPoints={_maxPerkPoints}");
    }

    private void LoadCareersXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_careers.xml");
        _logger.LogInfo($"CareerSystem: Loading careers from '{path}'");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: careers file not found at {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return;

            _maxPerkPoints = ParseInt(root, "max_perk_points", 30);

            foreach (var el in root.Elements("Career"))
            {
                try
                {
                    var cultureIds = new List<string>();
                    var culturesEl = el.Element("EligibleCultures");
                    if (culturesEl != null)
                    {
                        foreach (var c in culturesEl.Elements("Culture"))
                        {
                            var id = c.Attribute("id")?.Value;
                            if (!string.IsNullOrEmpty(id))
                                cultureIds.Add(id);
                        }
                    }

                    var groupIds = new List<string>();
                    var groupsEl = el.Element("ChoiceGroups");
                    if (groupsEl != null)
                    {
                        foreach (var g in groupsEl.Elements("Group"))
                        {
                            var id = g.Attribute("id")?.Value;
                            if (!string.IsNullOrEmpty(id))
                                groupIds.Add(id);
                        }
                    }

                    var career = new CareerDefinition(
                        id: el.Attribute("id")?.Value ?? "",
                        displayName: el.Attribute("display_name")?.Value ?? "",
                        description: el.Attribute("description")?.Value ?? "",
                        portraitSprite: el.Attribute("portrait_sprite")?.Value ?? "",
                        abilityTemplateId: el.Attribute("ability_template_id")?.Value ?? "",
                        minClanTier: ParseInt(el, "min_clan_tier", 0),
                        rootChoiceId: el.Attribute("root_choice_id")?.Value ?? "",
                        eligibleCultureIds: cultureIds,
                        choiceGroupIds: groupIds,
                        rank1Name: el.Attribute("rank1_name")?.Value ?? "",
                        rank2Name: el.Attribute("rank2_name")?.Value ?? "",
                        rank3Name: el.Attribute("rank3_name")?.Value ?? "");

                    _careers.Add(career);
                    _logger.LogDebug($"CareerSystem: Parsed career '{career.Id}' — cultures=[{string.Join(", ", cultureIds)}], groups=[{string.Join(", ", groupIds)}], rootChoice='{career.RootChoiceId}'");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CareerSystem: Failed to parse career element: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load careers XML: {ex.Message}");
        }
    }

    private void LoadChoicesXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_career_choices.xml");
        _logger.LogInfo($"CareerSystem: Loading choices from '{path}'");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: choices file not found at {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return;

            // Parse standalone choices (root nodes)
            foreach (var choiceEl in root.Elements("Choice"))
            {
                var choice = ParseChoice(choiceEl);
                if (choice != null)
                    _choices.Add(choice);
            }

            // Parse choice groups
            foreach (var groupEl in root.Elements("ChoiceGroup"))
            {
                try
                {
                    var choiceIds = new List<string>();
                    foreach (var choiceEl in groupEl.Elements("Choice"))
                    {
                        var choice = ParseChoice(choiceEl);
                        if (choice != null)
                        {
                            _choices.Add(choice);
                            choiceIds.Add(choice.Id);
                        }
                    }

                    var group = new CareerChoiceGroupDefinition(
                        id: groupEl.Attribute("id")?.Value ?? "",
                        careerId: groupEl.Attribute("career_id")?.Value ?? "",
                        tier: ParseInt(groupEl, "tier", 1),
                        choiceIds: choiceIds,
                        displayName: groupEl.Attribute("display_name")?.Value ?? "");

                    _groups.Add(group);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CareerConfig: failed to parse choice group: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load choices XML: {ex.Message}");
        }
    }

    private CareerChoiceDefinition ParseChoice(XElement el)
    {
        try
        {
            PassiveEffect passive = null;
            // Two authoring schemas exist: a direct singular child <PassiveEffect ... magnitude=.../>
            // and a plural wrapper <PassiveEffects><PassiveEffect ... value=.../></PassiveEffects>.
            // The wrapper form (310 choices) was historically unparsed. Read it as a fallback; all
            // wrappers carry exactly one child (verified). Direct child wins when both are present.
            var passiveEl = el.Element("PassiveEffect") ?? el.Element("PassiveEffects")?.Element("PassiveEffect");
            if (passiveEl != null)
            {
                // An unrecognized type= must surface loudly, not silently coerce to Special (inert).
                // ParseEnum's fallback is for survival, not acceptance — mirror its case-insensitive
                // parse here so the gate and the parse can't disagree.
                var typeRaw = passiveEl.Attribute("type")?.Value;
                if (typeRaw != null && !Enum.TryParse<PassiveEffectType>(typeRaw, true, out _))
                    _logger.LogWarning(
                        $"CareerConfig: choice '{el.Attribute("id")?.Value}' has unknown PassiveEffect " +
                        $"type '{typeRaw}' — treated as Special (inert pip).");
                passive = new PassiveEffect(
                    effectType: ParseEnum<PassiveEffectType>(passiveEl, "type", PassiveEffectType.Special),
                    // Accept value= as an alias for magnitude= (the wrapper schema uses value=).
                    // magnitude= takes precedence when both are present.
                    magnitude: ParseFloat(passiveEl, "magnitude", ParseFloat(passiveEl, "value", 0f)),
                    // operation= / is_percentage= were parsed-but-never-read (the consumer chooses
                    // additive vs multiplicative per type); dropped. attack_type_mask IS consumed.
                    attackTypeMask: ParseEnum<AttackTypeMask>(passiveEl, "attack_type_mask", AttackTypeMask.All));
            }

            var mutations = new List<MutationDefinition>();
            var mutationsEl = el.Element("Mutations");
            if (mutationsEl != null)
            {
                foreach (var mutEl in mutationsEl.Elements("Mutation"))
                {
                    var parameters = new Dictionary<string, string>();
                    foreach (var attr in mutEl.Attributes())
                    {
                        var name = attr.Name.LocalName;
                        if (name != "target_id" && name != "property" && name != "calculator" && name != "operation")
                            parameters[name] = attr.Value;
                    }

                    mutations.Add(new MutationDefinition(
                        targetTemplateId: mutEl.Attribute("target_id")?.Value ?? "",
                        propertyName: mutEl.Attribute("property")?.Value ?? "",
                        calculatorId: mutEl.Attribute("calculator")?.Value ?? "",
                        operation: ParseEnum<OperationType>(mutEl, "operation", OperationType.Add),
                        parameters: parameters));
                }
            }

            return new CareerChoiceDefinition(
                id: el.Attribute("id")?.Value ?? "",
                groupId: el.Attribute("group_id")?.Value ?? "",
                type: ParseEnum<ChoiceType>(el, "type", ChoiceType.Passive),
                description: el.Attribute("description")?.Value ?? "",
                iconSprite: el.Attribute("icon_sprite")?.Value ?? "",
                passive: passive,
                mutations: mutations);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to parse choice: {ex.Message}");
            return null;
        }
    }

    // Load-time phantom-bonus gate (csharp-architecture.md "Config Providers MUST Validate").
    // Any choice whose PassiveEffect type has no runtime consumer is selectable but inert; warn
    // loudly so it surfaces in the log instead of silently doing nothing in-game.
    private void ValidatePassiveConsumers()
    {
        var unconsumed = new List<string>();
        foreach (var choice in _choices)
        {
            var passive = choice.Passive;
            if (passive == null) continue;
            if (!PassiveEffectConsumers.IsConsumed(passive.EffectType))
                unconsumed.Add($"{choice.Id} (type={passive.EffectType})");
        }

        if (unconsumed.Count > 0)
            _logger.LogWarning(
                $"CareerSystem: {unconsumed.Count} choice(s) reference a PassiveEffect type with NO runtime " +
                $"consumer — phantom bonus, selectable but inert: {string.Join(", ", unconsumed)}");
    }

    private void LoadAbilityTemplatesXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_ability_templates.xml");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: ability templates file not found at {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return;

            foreach (var el in root.Elements("AbilityTemplate"))
            {
                try
                {
                    var id = el.Attribute("id")?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    var template = new AbilityTemplateData
                    {
                        Id = id,
                        DisplayName = el.Attribute("display_name")?.Value ?? "",
                        Duration = ParseFloat(el, "duration", 8f),
                        Radius = ParseFloat(el, "radius", 10f),
                        MaxCharge = ParseFloat(el, "max_charge", 0f),
                        ParticleEffect = el.Attribute("particle_effect")?.Value ?? "",
                        SoundEffect = el.Attribute("sound_effect")?.Value ?? "",
                        TooltipDescription = el.Attribute("tooltip")?.Value ?? "",
                    };

                    _abilityTemplates[id] = template;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CareerConfig: failed to parse ability template element: {ex.Message}");
                }
            }

            _logger.LogInfo($"CareerSystem: Loaded {_abilityTemplates.Count} ability templates");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load ability templates XML: {ex.Message}");
        }
    }

    private void LoadAbilityTuningXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_ability_tuning.xml");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: ability tuning file not found at {path} — using defaults");
            _abilityTuning = AbilityTuningConfig.Default;
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null)
            {
                _abilityTuning = AbilityTuningConfig.Default;
                return;
            }

            var globalEl = root.Element("Global");
            var global = ParseGlobalTuning(globalEl);

            var infEl = root.Element("Infantry");
            var infantry = infEl != null
                ? new InfantryTuning(
                    ParseFloat(infEl, "damage_bonus", 15f),
                    ParseFloat(infEl, "damage_reduction", 10f),
                    ParseFloat(infEl, "radius", 50f))
                : InfantryTuning.Default;

            var rngEl = root.Element("Ranged");
            var ranged = rngEl != null
                ? new RangedTuning(
                    ParseFloat(rngEl, "speed_bonus", 15f),
                    ParseFloat(rngEl, "ranged_damage_bonus", 20f),
                    ParseFloat(rngEl, "draw_speed_bonus", 20f))
                : RangedTuning.Default;

            var cavEl = root.Element("Cavalry");
            var cavalry = cavEl != null
                ? new CavalryTuning(
                    ParseFloat(cavEl, "mount_speed_bonus", 20f),
                    ParseFloat(cavEl, "charge_damage_bonus", 25f),
                    ParseFloat(cavEl, "damage_bonus", 10f))
                : CavalryTuning.Default;

            _abilityTuning = new AbilityTuningConfig(global, infantry, ranged, cavalry);
            _logger.LogInfo($"CareerSystem: Loaded ability tuning — Global(cooldown={global.CooldownSeconds}s) Infantry(dmg={infantry.DamageBonus},red={infantry.DamageReduction},r={infantry.Radius}) Ranged(spd={ranged.SpeedBonus},dmg={ranged.RangedDamageBonus},draw={ranged.DrawSpeedBonus}) Cavalry(mspd={cavalry.MountSpeedBonus},chrg={cavalry.ChargeDamageBonus},dmg={cavalry.DamageBonus})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load ability tuning XML: {ex.Message}");
            _abilityTuning = AbilityTuningConfig.Default;
        }
    }

    private const float MaxCooldownSeconds = 3600f;

    private GlobalTuning ParseGlobalTuning(XElement globalEl)
    {
        if (globalEl == null) return GlobalTuning.Default;

        var raw = globalEl.Attribute("cooldown_seconds")?.Value;
        if (raw == null) return GlobalTuning.Default;

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{raw}\"> is not a number — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        // float.TryParse admits NaN, +Infinity, -Infinity. The range checks below evaluate false for NaN
        // (NaN comparisons always yield false), which would let a NaN cooldown reach CareerAbility and
        // permanently break the activation gate. Reject non-finite values explicitly.
        if (float.IsNaN(seconds) || float.IsInfinity(seconds))
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{raw}\"> is not a finite number — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        if (seconds <= 0f)
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{seconds}\"> must be > 0 — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        if (seconds > MaxCooldownSeconds)
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{seconds}\"> exceeds maximum of {MaxCooldownSeconds}s — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        // Issue #104 Option B — min_cooldown_seconds is optional; floor for designer CooldownReduction
        // mutations. Default 5s. Same NaN/Infinity/range guards as cooldown_seconds (see
        // .claude/rules/csharp-architecture.md "Config Providers MUST Validate" + memory
        // feedback_clamp_nan_infinity_propagates.md — the rule has shipped three times now).
        var minRaw = globalEl.Attribute("min_cooldown_seconds")?.Value;
        var minSeconds = GlobalTuning.Default.MinCooldownSeconds;
        if (minRaw != null)
        {
            if (!float.TryParse(minRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMin))
                _logger.LogWarning($"CareerConfig: <Global min_cooldown_seconds=\"{minRaw}\"> is not a number — falling back to {minSeconds}s");
            else if (float.IsNaN(parsedMin) || float.IsInfinity(parsedMin))
                _logger.LogWarning($"CareerConfig: <Global min_cooldown_seconds=\"{minRaw}\"> is not finite — falling back to {minSeconds}s");
            else if (parsedMin < 0f)
                _logger.LogWarning($"CareerConfig: <Global min_cooldown_seconds=\"{parsedMin}\"> must be >= 0 — falling back to {minSeconds}s");
            else if (parsedMin > seconds)
                _logger.LogWarning($"CareerConfig: <Global min_cooldown_seconds=\"{parsedMin}\"> exceeds cooldown_seconds={seconds} — falling back to {minSeconds}s");
            else
                minSeconds = parsedMin;
        }

        return new GlobalTuning(seconds, minSeconds);
    }

    private static int ParseInt(XElement el, string attrName, int defaultValue)
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : defaultValue;
    }

    private static float ParseFloat(XElement el, string attrName, float defaultValue)
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        // Phase 9b #128 P2 — reject NaN/Infinity. Pre-fix only CooldownSeconds had this guard
        // (Career #31 fix); generic ParseFloat fed Duration/Radius/MaxCharge/DamageBonus etc.
        // NaN propagates: ExpiresAt = currentTime + NaN → IsExpired always false; NaN Radius →
        // all distance comparisons false. See feedback_clamp_nan_infinity_propagates.md.
        if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return defaultValue;
        if (float.IsNaN(result) || float.IsInfinity(result))
            return defaultValue;
        return result;
    }

    private static T ParseEnum<T>(XElement el, string attrName, T defaultValue) where T : struct
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return Enum.TryParse(val, true, out T result) ? result : defaultValue;
    }
}
