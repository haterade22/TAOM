namespace TAOM.Features.FactionPower;

/// <summary>
/// The keys and list-item ids of the faction power part of the Advanced Start Options screen. A screen key is what the engine
/// writes into a new campaign when the player changes the value; the keys the store reads (<c>taom_fp_&lt;culture&gt;</c>) stay
/// separate, so the screen can change later without a save migration.
/// </summary>
public static class FactionPowerScreenKeys
{
    public const string Category = "taom_realm_power";

    public const string PresetOption = "taom_fp_preset";
    public const string StandardItem = "taom_fp_preset_standard";
    public const string EvilItem = "taom_fp_preset_evil50";
    public const string FreeItem = "taom_fp_preset_free50";
    public const string CustomItem = "taom_fp_preset_custom";

    public static readonly string[] PresetItems = { StandardItem, EvilItem, FreeItem, CustomItem };

    public const string SliderPrefix = "taom_fp_s_";

    public static string SliderKey(string cultureId) => SliderPrefix + cultureId;
}
