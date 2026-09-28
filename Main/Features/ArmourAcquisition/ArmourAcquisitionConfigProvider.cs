using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// Loads and validates armour_acquisition_config.xml (csharp-architecture.md, "Config Providers MUST
/// Validate"): every number is range-checked, every float NaN-checked, the gate levels order-checked,
/// every class name checked against the known set. A bad field reverts to its compiled default with a
/// warning, and one summary warning follows any reversion. A missing file is the compiled default; a
/// file that does not parse is the compiled default with an error. Reuse.Singleton: a change needs a
/// full restart.
/// </summary>
public sealed class ArmourAcquisitionConfigProvider : IArmourAcquisitionConfigProvider
{
    private const string ConfigSubfolder = "armour_acquisition";
    private const string ConfigFileName = "armour_acquisition_config.xml";
    private const string Tag = "[ArmourAcquisition]";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private ArmourAcquisitionConfig? _config;
    private int _reverted;

    public ArmourAcquisitionConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public ArmourAcquisitionConfig GetConfig() => _config ??= Load();

    private ArmourAcquisitionConfig Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, ConfigSubfolder, ConfigFileName);
        if (!File.Exists(path))
        {
            _logger.LogInfo($"{Tag} No {ConfigFileName} at {path}; using the compiled defaults.");
            return ArmourAcquisitionConfig.Default;
        }

        XElement root;
        try
        {
            root = XDocument.Load(path).Root ?? throw new InvalidDataException("empty document");
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} {ConfigFileName} does not parse ({ex.Message}); using the compiled defaults.");
            return ArmourAcquisitionConfig.Default;
        }

        var d = ArmourAcquisitionConfig.Default;
        var enabled = ReadBool(root, "enabled", d.Enabled);
        var (heavy, elite, lord) = ReadGate(root.Element("Gate"), d);
        var recipes = ReadRecipes(root.Element("Upgrades"), d);
        var named = ReadNamedWeapons(root.Element("NamedWeapons"), d);

        var lordEvent = root.Element("LordEvent");
        var chance = ReadFloat(lordEvent, "chance", d.LordEventChance, 0f, 1f);
        var cooldown = ReadInt(lordEvent, "cooldown_days", d.LordEventCooldownDays, 0, 3650);
        var leaveRelation = ReadInt(lordEvent, "leave_relation", d.LordEventLeaveRelation, 0, 100);

        var visiting = root.Element("VisitingArmourer");
        var visitChance = ReadFloat(visiting, "chance_per_day", d.VisitChancePerDay, 0f, 1f);
        var visitDays = ReadInt(visiting, "duration_days", d.VisitDurationDays, 1, 365);
        var visitBonus = ReadInt(visiting, "level_bonus", d.VisitLevelBonus, 0, ArmourAcquisitionConfig.MaxArmouryLevel);

        var harnessCooldown = ReadInt(root.Element("LordHarness"), "offer_cooldown_days", d.HarnessOfferCooldownDays, 0, 3650);

        if (_reverted > 0)
            _logger.LogWarning($"{Tag} {_reverted} value(s) in {ConfigFileName} were refused and reverted to their "
                               + "compiled defaults; see the warnings above.");

        var config = new ArmourAcquisitionConfig(enabled, heavy, elite, lord, recipes, named,
            chance, cooldown, leaveRelation, visitChance, visitDays, visitBonus, harnessCooldown);
        _logger.LogInfo($"{Tag} Config loaded: enabled={enabled}, gate heavy {heavy} / elite {elite} / lord {lord}, "
                        + $"{recipes.Count} upgrade recipe(s), {named.Count} named weapon(s).");
        return config;
    }

    private (int heavy, int elite, int lord) ReadGate(XElement? gate, ArmourAcquisitionConfig d)
    {
        var max = ArmourAcquisitionConfig.MaxArmouryLevel;
        var heavy = ReadInt(gate, "heavy", d.HeavyLevel, 0, max);
        var elite = ReadInt(gate, "elite", d.EliteLevel, 0, max);
        var lord = ReadInt(gate, "lord", d.LordLevel, 0, max);
        if (heavy <= elite && elite <= lord)
            return (heavy, elite, lord);
        Revert($"<Gate> levels must be in order heavy <= elite <= lord (got {heavy} / {elite} / {lord}); "
               + $"using {d.HeavyLevel} / {d.EliteLevel} / {d.LordLevel}.");
        return (d.HeavyLevel, d.EliteLevel, d.LordLevel);
    }

    private IReadOnlyDictionary<ArmourClass, UpgradeRecipe> ReadRecipes(XElement? upgrades, ArmourAcquisitionConfig d)
    {
        if (upgrades == null)
            return d.Recipes;

        var recipes = new Dictionary<ArmourClass, UpgradeRecipe>();
        foreach (var el in upgrades.Elements("Upgrade"))
        {
            var raw = el.Attribute("target")?.Value;
            if (!ArmourClassRules.TryParse(raw, out var target)
                || target == ArmourClass.Light || target == ArmourClass.Civilian || target == ArmourClass.Named)
            {
                Revert($"<Upgrade target=\"{raw}\"> is not a class a piece can be upgraded into (medium, heavy, elite, lord); skipped.");
                continue;
            }
            if (recipes.ContainsKey(target))
            {
                Revert($"duplicate <Upgrade target=\"{raw}\">; keeping the first.");
                continue;
            }

            d.Recipes.TryGetValue(target, out var fallback);
            var gold = ReadInt(el, "gold", fallback?.Gold ?? 0, 0, 1_000_000);
            var share = ReadFloat(el, "value_share", fallback?.ValueShare ?? 0f, 0f, 1f);
            var resource = ReadFloat(el, "special_resource", fallback?.SpecialResource ?? 0f, 0f, 10_000f);
            recipes[target] = new UpgradeRecipe(target, gold, share, resource, ReadMaterials(el, raw));
        }
        return recipes;
    }

    private IReadOnlyList<UpgradeMaterial> ReadMaterials(XElement upgrade, string? target)
    {
        var materials = new List<UpgradeMaterial>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in upgrade.Elements("Material"))
        {
            var item = m.Attribute("item")?.Value?.Trim();
            if (item is null || item.Length == 0)
            {
                Revert($"<Upgrade target=\"{target}\"> has a <Material> with no item id; skipped.");
                continue;
            }
            var rawCount = m.Attribute("count")?.Value;
            if (!int.TryParse(rawCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                || count < 1 || count > 999)
            {
                Revert($"<Upgrade target=\"{target}\"> material '{item}' count=\"{rawCount}\" is not 1 to 999; skipped.");
                continue;
            }
            if (!seen.Add(item))
            {
                Revert($"<Upgrade target=\"{target}\"> lists material '{item}' twice; keeping the first.");
                continue;
            }
            materials.Add(new UpgradeMaterial(item, count));
        }
        return materials;
    }

    private IReadOnlyCollection<string> ReadNamedWeapons(XElement? named, ArmourAcquisitionConfig d)
    {
        if (named == null)
            return d.NamedWeapons;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in named.Elements("Item"))
        {
            if (item.Attribute("id")?.Value?.Trim() is { Length: > 0 } id)
                ids.Add(id);
        }
        return ids;
    }

    private bool ReadBool(XElement el, string attr, bool fallback)
    {
        var raw = el.Attribute(attr)?.Value;
        if (raw == null)
            return fallback;
        if (bool.TryParse(raw.Trim(), out var value))
            return value;
        Revert($"{attr}=\"{raw}\" is not true or false; using {fallback}.");
        return fallback;
    }

    private int ReadInt(XElement? el, string attr, int fallback, int min, int max)
    {
        var raw = el?.Attribute(attr)?.Value;
        if (raw == null)
            return fallback;
        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= min && value <= max)
            return value;
        Revert($"{el!.Name}: {attr}=\"{raw}\" is not a whole number from {min} to {max}; using {fallback}.");
        return fallback;
    }

    private float ReadFloat(XElement? el, string attr, float fallback, float min, float max)
    {
        var raw = el?.Attribute(attr)?.Value;
        if (raw == null)
            return fallback;
        if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && FiniteFloatValidator.IsFiniteInRange(value, min, max))
            return value;
        Revert($"{el!.Name}: {attr}=\"{raw}\" is not a finite number from {min} to {max}; using {fallback}.");
        return fallback;
    }

    private void Revert(string message)
    {
        _reverted++;
        _logger.LogWarning($"{Tag} {ConfigFileName}: {message}");
    }
}
