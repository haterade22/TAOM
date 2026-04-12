using System;
using System.IO;
using Newtonsoft.Json;

namespace TAOM.Features;

public class TaomSettings
{
    private static TaomSettings _instance;
    private static string _settingsPath;

    public static TaomSettings Instance => _instance;

    // --- Encyclopedia ---
    public bool ShowAllEncyclopediaCharacters { get; set; } = true;

    // --- Troop Weight ---
    public bool EnableTroopWeight { get; set; } = true;

    // --- War of the Ring ---
    public bool WarOfTheRingEnabled { get; set; } = true;
    public int Phase1TriggerDay { get; set; } = 30;
    public int Phase2TriggerDay { get; set; } = 45;
    public bool TestMode { get; set; }

    // --- Battle Balance / Troop Power ---
    public bool EnableCustomTroopPower { get; set; } = true;
    public bool OverrideVanillaTierPower { get; set; }
    public float Tier7Power { get; set; } = 2.91f;
    public float Tier8Power { get; set; } = 3.26f;
    public float Tier9Power { get; set; } = 3.61f;
    public float Tier10Power { get; set; } = 3.96f;
    public float HeroMultiplier { get; set; } = 1.5f;
    public float MountedMultiplier { get; set; } = 1.2f;

    // --- Battle Balance / Casualty Ratios ---
    public bool EnableCustomCasualtyRatios { get; set; } = true;
    public float PlayerBluntDamageChance { get; set; } = 0.30f;
    public float AIBluntDamageChance { get; set; } = 0.10f;
    public bool EnableCulturalSurvivalBonuses { get; set; } = true;

    // --- Siege Defense ---
    public bool EnableSiegeDefenseEvents { get; set; } = true;
    public int SiegeDefenseResponseDays { get; set; } = 3;

    // --- AI Strategic Intelligence ---
    public bool EnableArmyStrategicIntelligence { get; set; } = true;
    public float ArmyCommitmentMultiplier { get; set; } = 4.0f;
    public float ArmyPriorityBoost { get; set; } = 3.0f;
    public float EvilFactionAggressionScale { get; set; } = 1.0f;
    public float LongRangePriorityBoostScale { get; set; } = 1.0f;
    public float ArmyBorderProximityFloor { get; set; } = 0.15f;

    // --- Time Acceleration ---
    public int FastForwardMultiplier { get; set; } = 4;
    public int ExtraFastForwardMultiplier { get; set; } = 8;
    public int CtrlSpaceMultiplier { get; set; } = 16;

    public static void Initialize(string moduleDataPath)
    {
        _settingsPath = Path.Combine(moduleDataPath, "configs", "taom_settings.json");
        _instance = LoadFrom(_settingsPath);
    }

    public static void Reset()
    {
        _instance = null;
        _settingsPath = null;
    }

    public static TaomSettings LoadFrom(string path)
    {
        if (!File.Exists(path))
            return new TaomSettings();

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new TaomSettings();

            return JsonConvert.DeserializeObject<TaomSettings>(json) ?? new TaomSettings();
        }
        catch (Exception ex)
        {
            TaleWorlds.Library.Debug.Print($"[TAOM] TaomSettings: Failed to load from {path}: {ex.Message}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
            return new TaomSettings();
        }
    }

    public static void SaveTo(TaomSettings settings, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}
