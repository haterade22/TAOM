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
        LoadAbilityTemplatesXml();

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
                        chargeType: ParseEnum<ChargeType>(el, "charge_type", ChargeType.CooldownOnly),
                        maxCharge: ParseInt(el, "max_charge", 100),
                        minClanTier: ParseInt(el, "min_clan_tier", 0),
                        rootChoiceId: el.Attribute("root_choice_id")?.Value ?? "",
                        eligibleCultureIds: cultureIds,
                        choiceGroupIds: groupIds);

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
                        choiceIds: choiceIds);

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
            var passiveEl = el.Element("PassiveEffect");
            if (passiveEl != null)
            {
                passive = new PassiveEffect(
                    effectType: ParseEnum<PassiveEffectType>(passiveEl, "type", PassiveEffectType.Special),
                    magnitude: ParseFloat(passiveEl, "magnitude", 0f),
                    operation: ParseEnum<OperationType>(passiveEl, "operation", OperationType.Add),
                    isPercentage: ParseBool(passiveEl, "is_percentage", false),
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
                        SpriteName = el.Attribute("sprite")?.Value ?? "",
                        Cooldown = ParseFloat(el, "cooldown", 0f),
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
        return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result : defaultValue;
    }

    private static bool ParseBool(XElement el, string attrName, bool defaultValue)
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return bool.TryParse(val, out var result) ? result : defaultValue;
    }

    private static T ParseEnum<T>(XElement el, string attrName, T defaultValue) where T : struct
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return Enum.TryParse(val, true, out T result) ? result : defaultValue;
    }
}
