using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    // #769 E8: effects one option may carry. A third and later element is warned about and ignored.
    public const int MaxPassivesPerChoice = 2;

    /// <summary>
    /// Largest |magnitude| a passive may carry (#769 E11). Far above every shipped value (party size 50, daily gold
    /// 250); it exists so two accepted pips cannot sum to Infinity and a finite one cannot overflow the engine's later
    /// float-to-int conversions (loot, raid gold). A larger one is dropped with a warning, like an unknown troop_class.
    /// </summary>
    public const float MaxPassiveMagnitude = 10000f;

    private const int DefaultRetrainGoldBase = 1000;
    private const int DefaultRetrainGoldPerLevel = 250;
    private int _maxPerkPoints = 30;
    private int _retrainGoldBase = DefaultRetrainGoldBase;
    private int _retrainGoldPerLevel = DefaultRetrainGoldPerLevel;
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

    public int GetRetrainGoldBase()
    {
        EnsureLoaded();
        return _retrainGoldBase;
    }

    public int GetRetrainGoldPerLevel()
    {
        EnsureLoaded();
        return _retrainGoldPerLevel;
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

        // DEBUG, not INFO: CareerRegistry logs the identical four totals once the data has crossed
        // the provider→registry boundary, which is the more useful of the two.
        _logger.LogDebug($"CareerSystem: Loaded {_careers.Count} careers, {_groups.Count} groups, {_choices.Count} choices, maxPerkPoints={_maxPerkPoints}");
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
            _retrainGoldBase = ParseIntAtLeast(root, "retrain_gold_base", 0, DefaultRetrainGoldBase, "the career root");
            _retrainGoldPerLevel = ParseIntAtLeast(root, "retrain_gold_per_level", 0, DefaultRetrainGoldPerLevel, "the career root");

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
                        rank3Name: el.Attribute("rank3_name")?.Value ?? "",
                        keystoneIcon: el.Attribute("keystone_icon")?.Value ?? "",
                        pointLevels: ParsePointLevels(el),
                        treeVersion: ParseIntAtLeast(el, "tree_version", 1, 1, $"career '{el.Attribute("id")?.Value}'"));

                    // Issue #380 — no fallback glyph by design (the reference module's culture
                    // fallback regressed Rivendell to its clan-sigil ring). Missing attr =
                    // no medallion + this warning, so the authoring gap is visible.
                    if (string.IsNullOrEmpty(career.KeystoneIcon))
                        _logger.LogWarning($"CareerConfig: career '{career.Id}' has no keystone_icon — keystone medallion will not render");

                    // No per-career line: 49 of them restated what the load summary already totals,
                    // and every field is readable in taom_careers.xml. Parse FAILURES still log
                    // (catch block below), which is the case worth seeing.
                    _careers.Add(career);
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
                    var groupId = groupEl.Attribute("id")?.Value ?? "";
                    var choiceIds = new List<string>();
                    foreach (var choiceEl in groupEl.Elements("Choice"))
                    {
                        var choice = ParseChoice(choiceEl, groupId);
                        if (choice != null)
                        {
                            _choices.Add(choice);
                            choiceIds.Add(choice.Id);
                        }
                    }

                    var group = new CareerChoiceGroupDefinition(
                        id: groupId,
                        careerId: groupEl.Attribute("career_id")?.Value ?? "",
                        tier: ParseInt(groupEl, "tier", 1),
                        choiceIds: choiceIds,
                        displayName: groupEl.Attribute("display_name")?.Value ?? "",
                        maxPicks: ParseIntAtLeast(groupEl, "max_picks", 0, 0, $"group '{groupId}'"));

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

    // Membership is the nesting only (#766): groupId is the enclosing <ChoiceGroup> id, and
    // a root <Choice> passes "". A group_id attribute is never read for membership; a non-empty one
    // is a stale authoring leftover, so it is logged and ignored.
    private CareerChoiceDefinition ParseChoice(XElement el, string groupId = "")
    {
        try
        {
            var declared = el.Attribute("group_id")?.Value ?? "";
            if (declared.Length > 0)
                _logger.LogWarning(
                    $"CareerConfig: choice '{el.Attribute("id")?.Value}' carries group_id '{declared}'; " +
                    "ignored; membership is the enclosing ChoiceGroup.");

            var passives = ParsePassives(el, el.Attribute("id")?.Value);

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
                groupId: groupId,
                type: ParseEnum<ChoiceType>(el, "type", ChoiceType.Passive),
                description: el.Attribute("description")?.Value ?? "",
                iconSprite: el.Attribute("icon_sprite")?.Value ?? "",
                passives: passives,
                mutations: mutations);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to parse choice: {ex.Message}");
            return null;
        }
    }

    // #769 E8: an option's effects, in document order. Two authoring schemas exist and may be mixed: a
    // direct child <PassiveEffect ... magnitude=.../> and a plural wrapper
    // <PassiveEffects><PassiveEffect ... value=.../></PassiveEffects> (310 shipped choices, once
    // unparsed). Both feed one list. Only the first MaxPassivesPerChoice AUTHORED elements are read; the
    // rest are named in one warning and ignored, so a third effect can never half-apply. The cap counts
    // what the author wrote, not what survived parsing: an element dropped for a bad troop_class still
    // uses up one of the two places.
    private List<PassiveEffect> ParsePassives(XElement choiceEl, string? choiceId)
    {
        var elements = PassiveElements(choiceEl).ToList();
        if (elements.Count > MaxPassivesPerChoice)
            _logger.LogWarning(
                $"CareerConfig: choice '{choiceId}' holds {elements.Count} PassiveEffect " +
                $"elements; only the first {MaxPassivesPerChoice} are read, the rest are ignored.");

        var passives = new List<PassiveEffect>(MaxPassivesPerChoice);
        foreach (var passiveEl in elements.Take(MaxPassivesPerChoice))
        {
            var passive = ParsePassive(passiveEl, choiceId);
            if (passive != null)
                passives.Add(passive);
        }

        return passives;
    }

    private static IEnumerable<XElement> PassiveElements(XElement choiceEl)
    {
        foreach (var child in choiceEl.Elements())
        {
            if (child.Name == "PassiveEffect")
                yield return child;
            else if (child.Name == "PassiveEffects")
                foreach (var inner in child.Elements("PassiveEffect"))
                    yield return inner;
        }
    }

    // One <PassiveEffect>, or null when its troop_class cannot be read or its magnitude is out of bounds (the passive is dropped).
    private PassiveEffect? ParsePassive(XElement passiveEl, string? choiceId)
    {
        // An unrecognized type= must surface loudly, not silently coerce to Special (inert).
        // ParseEnum's fallback is for survival, not acceptance — mirror its case-insensitive
        // parse here so the gate and the parse can't disagree.
        var typeRaw = passiveEl.Attribute("type")?.Value;
        if (typeRaw != null && !Enum.TryParse<PassiveEffectType>(typeRaw, true, out _))
            _logger.LogWarning(
                $"CareerConfig: choice '{choiceId}' has unknown PassiveEffect " +
                $"type '{typeRaw}' — treated as Special (inert pip).");
        var type = ParseEnum<PassiveEffectType>(passiveEl, "type", PassiveEffectType.Special);

        if (!TryParseTroopClass(passiveEl, type, choiceId, out var troopClass))
            return null;

        // Accept value= as an alias for magnitude= (the wrapper schema uses value=).
        // magnitude= takes precedence when both are present.
        var magnitude = ParseFloat(passiveEl, "magnitude", ParseFloat(passiveEl, "value", 0f));
        if (Math.Abs(magnitude) > MaxPassiveMagnitude)
        {
            _logger.LogWarning(
                $"CareerConfig: choice '{choiceId}' has a {type} passive with magnitude '{passiveEl.Attribute("magnitude")?.Value ?? passiveEl.Attribute("value")?.Value}', " +
                $"beyond the bound of {MaxPassiveMagnitude.ToString(CultureInfo.InvariantCulture)}; the passive is dropped.");
            return null;
        }

        return new PassiveEffect(
            effectType: type,
            magnitude: magnitude,
            // operation= / is_percentage= were parsed-but-never-read (the consumer chooses
            // additive vs multiplicative per type); dropped. attack_type_mask IS consumed.
            attackTypeMask: ParseAttackTypeMask(passiveEl, type, choiceId),
            troopClass: troopClass);
    }

    // #769 E9. Absent keeps All. On a type no consumer passes a class for, the attribute is ignored
    // with a warning (restricting a pip that cannot be restricted would be a lie). On a class-filtered
    // type an unknown name FAILS CLOSED: false, the passive is dropped, because defaulting a typo to All
    // would widen an infantry bonus to the whole army.
    private bool TryParseTroopClass(XElement passiveEl, PassiveEffectType type, string? choiceId, out TroopClassMask troopClass)
    {
        troopClass = TroopClassMask.All;
        var raw = passiveEl.Attribute("troop_class")?.Value;
        if (raw == null) return true;

        if (!PassiveEffectConsumers.IsClassFiltered(type))
        {
            _logger.LogWarning(
                $"CareerConfig: choice '{choiceId}' has troop_class '{raw}' on a {type} passive, which reads no " +
                $"troop class; ignored (applies to every troop). Class-filtered types: " +
                $"{string.Join(", ", PassiveEffectConsumers.ClassFiltered)}.");
            return true;
        }

        if (TroopClassMapper.TryParse(raw, out var parsed))
        {
            troopClass = parsed;
            return true;
        }

        _logger.LogWarning(
            $"CareerConfig: choice '{choiceId}' has an unknown troop_class '{raw}' on a {type} passive " +
            "(names: Infantry, Ranged, Cavalry, All, separated by commas or pipes); the passive is dropped.");
        return false;
    }

    // Load-time phantom-bonus gate (csharp-architecture.md "Config Providers MUST Validate").
    // Any choice whose PassiveEffect type has no runtime consumer is selectable but inert; warn
    // loudly so it surfaces in the log instead of silently doing nothing in-game.
    private void ValidatePassiveConsumers()
    {
        var unconsumed = new List<string>();
        foreach (var choice in _choices)
        {
            foreach (var passive in choice.Passives)
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

        // Issue #383 — optional attribution threshold; ParseFloat already rejects NaN/Infinity.
        var minReportable = ParseFloat(globalEl, "min_reportable_bonus_damage", GlobalTuning.Default.MinReportableBonusDamage);
        if (minReportable < 0f)
        {
            _logger.LogWarning($"CareerConfig: <Global min_reportable_bonus_damage=\"{minReportable}\"> must be >= 0 — falling back to {GlobalTuning.Default.MinReportableBonusDamage}");
            minReportable = GlobalTuning.Default.MinReportableBonusDamage;
        }

        return new GlobalTuning(seconds, minSeconds, minReportable);
    }

    // #769 E5, E7 and E12: an optional whole-number attribute. A bad value must not silently turn a
    // fork into an unlimited group, a price free or absurd, or a redesigned tree into the old one,
    // so it warns, naming the owner and the attribute, and takes the default.
    private int ParseIntAtLeast(XElement el, string attrName, int min, int defaultValue, string owner)
    {
        var raw = el.Attribute(attrName)?.Value;
        if (raw == null) return defaultValue;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min)
            return value;

        _logger.LogWarning($"CareerConfig: {owner} has invalid {attrName} '{raw}'; expected a whole number of {min} or more, using {defaultValue}");
        return defaultValue;
    }

    // #769 E6: absent = empty (the legacy one-point-per-level formula). Every token must be a
    // positive integer and the list strictly ascending, otherwise the whole attribute is dropped.
    private IReadOnlyList<int> ParsePointLevels(XElement careerEl)
    {
        var raw = careerEl.Attribute("point_levels")?.Value;
        var levels = new List<int>();
        if (raw == null) return levels;

        foreach (var token in raw.Split(','))
        {
            if (!int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                || level <= 0
                || (levels.Count > 0 && level <= levels[levels.Count - 1]))
            {
                _logger.LogWarning($"CareerConfig: career '{careerEl.Attribute("id")?.Value}' has invalid point_levels '{raw}'; expected strictly ascending positive integers, using the one-point-per-level formula");
                return new List<int>();
            }
            levels.Add(level);
        }
        return levels;
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
        // NaN propagates: a NaN Duration makes PendingRestore.ExpiresAt NaN → the restore never
        // fires (>= NaN is always false); NaN Radius → all distance comparisons false.
        // See feedback_clamp_nan_infinity_propagates.md.
        if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return defaultValue;
        if (float.IsNaN(result) || float.IsInfinity(result))
            return defaultValue;
        return result;
    }

    // #613: the consumer branches on this string (Damage / Resistance masks), so an unknown value is
    // never coerced to the widest reading. Absent keeps All; unparseable warns and lands on None,
    // which matches no hit, and the shipped-XML gate refuses a None mask. On a type whose consumer passes
    // no hit (anything outside PassiveEffectConsumers.MaskGated, the three troop counters included) the
    // attribute is ignored with a warning, like troop_class on a non-class type: a None mask is "inert"
    // only where a hit is matched, and a valid narrower mask would show a restriction nothing applies.
    private AttackTypeMask ParseAttackTypeMask(XElement passiveEl, PassiveEffectType type, string? choiceId)
    {
        var raw = passiveEl.Attribute("attack_type_mask")?.Value;
        if (raw == null) return AttackTypeMask.All;
        if (!PassiveEffectConsumers.IsMaskGated(type))
        {
            _logger.LogWarning(
                $"CareerConfig: choice '{choiceId}' has attack_type_mask '{raw}' on a {type} passive, which reads " +
                $"no hit; ignored (applies to every hit). Mask-gated types: " +
                $"{string.Join(", ", PassiveEffectConsumers.MaskGated)}.");
            return AttackTypeMask.All;
        }

        if (AttackTypeMaskMatch.TryParse(raw, out var mask)) return mask;
        _logger.LogWarning(
            $"CareerConfig: choice '{choiceId}' has an unknown attack_type_mask '{raw}' " +
            "(names: Melee, Ranged, All, Cut, Pierce, Blunt, comma-separated); the pip is inert.");
        return AttackTypeMask.None;
    }

    private static T ParseEnum<T>(XElement el, string attrName, T defaultValue) where T : struct
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return Enum.TryParse(val, true, out T result) ? result : defaultValue;
    }
}
