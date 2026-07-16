using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerBearers.Domain;

namespace TAOM.Features.BannerBearers;

// Validating boundary loader for banner_bearers/banner_bearers_config.json
// (CombatMechanicsConfigProvider pattern): missing file → defaults + warn; parse failure →
// defaults + error; parseable-but-invalid values revert per-field to the compiled default
// + warn (csharp-architecture.md "Config Providers MUST Validate").
//
// Not validated here, deliberately: banner ItemObject ids and race names. Both need engine
// registries (MBObjectManager / FaceGen) that are not populated at config-load time. Banner
// ids are validated at mission start by BannerBearerAssignmentMissionLogic, which warns once
// per unresolved id — without that a typo'd id is accepted silently by SetFormationBanner
// (its own check is a stripped Debug.Assert) and yields bannerless formations with no
// diagnostic. An unknown race name in ExcludedRaces simply never matches (permissive).
public class BannerBearerConfigProvider : IBannerBearerConfigProvider
{
    // Replace, not append-merge: Json.NET's default ObjectCreationHandling appends JSON
    // collection entries onto the constructor-populated defaults, so a single ExcludedRaces
    // entry would leave all five compiled defaults in place.
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    // The engine's bearer arrangement tables are RelativeFormationPosition[6].
    private const int EngineArrangementCap = 6;

    // BattleBannerBearersModel.GetMinimumFormationTroopCountToBearBanners floors at 2 in vanilla.
    private const int EngineMinimumFormationTroopCount = 2;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<BannerBearerConfig> _config;

    public BannerBearerConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<BannerBearerConfig>(LoadConfig);
    }

    public BannerBearerConfig GetConfig() => _config.Value;

    private BannerBearerConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "banner_bearers", "banner_bearers_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"BannerBearerConfigProvider: banner_bearers_config.json not found at {path}, using defaults");
            return new BannerBearerConfig();
        }

        BannerBearerConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<BannerBearerConfig>(json, SerializerSettings) ?? new BannerBearerConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"BannerBearerConfigProvider: Failed to parse banner_bearers_config.json: {ex.Message}");
            return new BannerBearerConfig();
        }

        return Validate(parsed);
    }

    private BannerBearerConfig Validate(BannerBearerConfig parsed)
    {
        var defaults = new BannerBearerConfig();
        var rejected = false;

        var sanitized = new BannerBearerConfig
        {
            Enabled = parsed.Enabled,
            MinimumFormationTroopCount = ValidateRange(
                parsed.MinimumFormationTroopCount, defaults.MinimumFormationTroopCount,
                EngineMinimumFormationTroopCount, 100, nameof(parsed.MinimumFormationTroopCount), ref rejected),
            MaxBearersPerFormation = ValidateRange(
                parsed.MaxBearersPerFormation, defaults.MaxBearersPerFormation,
                1, EngineArrangementCap, nameof(parsed.MaxBearersPerFormation), ref rejected),
            InfantryBannerPerSoldiers = ValidatePerSoldiers(
                parsed.InfantryBannerPerSoldiers, defaults.InfantryBannerPerSoldiers,
                nameof(parsed.InfantryBannerPerSoldiers), ref rejected),
            RangedBannerPerSoldiers = ValidatePerSoldiers(
                parsed.RangedBannerPerSoldiers, defaults.RangedBannerPerSoldiers,
                nameof(parsed.RangedBannerPerSoldiers), ref rejected),
            CavalryBannerPerSoldiers = ValidatePerSoldiers(
                parsed.CavalryBannerPerSoldiers, defaults.CavalryBannerPerSoldiers,
                nameof(parsed.CavalryBannerPerSoldiers), ref rejected),
            HorseArcherBannerPerSoldiers = ValidatePerSoldiers(
                parsed.HorseArcherBannerPerSoldiers, defaults.HorseArcherBannerPerSoldiers,
                nameof(parsed.HorseArcherBannerPerSoldiers), ref rejected),
            OtherBannerPerSoldiers = ValidatePerSoldiers(
                parsed.OtherBannerPerSoldiers, defaults.OtherBannerPerSoldiers,
                nameof(parsed.OtherBannerPerSoldiers), ref rejected),
            ExcludedRaces = ValidateList(parsed.ExcludedRaces, defaults.ExcludedRaces, nameof(parsed.ExcludedRaces), ref rejected),
            CultureBanners = ValidateMap(parsed.CultureBanners, defaults.CultureBanners, nameof(parsed.CultureBanners), ref rejected),
            DefaultBannerItemId = parsed.DefaultBannerItemId ?? defaults.DefaultBannerItemId,
        };

        if (rejected)
            _logger.LogWarning("BannerBearerConfigProvider: banner_bearers_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("BannerBearerConfigProvider: Loaded banner_bearers_config.json");

        return sanitized;
    }

    private int ValidateRange(int value, int fallback, int min, int max, string field, ref bool rejected)
    {
        if (value < min || value > max)
        {
            _logger.LogWarning($"BannerBearerConfigProvider: {field} = {value} is outside [{min}, {max}], reverting to {fallback}");
            rejected = true;
            return fallback;
        }

        return value;
    }

    // 0 is a valid "disable banners for this class" switch, so the floor is 0 not 1.
    private int ValidatePerSoldiers(int value, int fallback, string field, ref bool rejected) =>
        ValidateRange(value, fallback, 0, 1000, field, ref rejected);

    private List<string> ValidateList(List<string> value, List<string> fallback, string field, ref bool rejected)
    {
        if (value == null)
        {
            _logger.LogWarning($"BannerBearerConfigProvider: {field} is null, reverting to defaults");
            rejected = true;
            return fallback;
        }

        return value;
    }

    private Dictionary<string, string> ValidateMap(
        Dictionary<string, string> value, Dictionary<string, string> fallback, string field, ref bool rejected)
    {
        if (value == null)
        {
            _logger.LogWarning($"BannerBearerConfigProvider: {field} is null, reverting to defaults");
            rejected = true;
            return fallback;
        }

        return value;
    }
}
