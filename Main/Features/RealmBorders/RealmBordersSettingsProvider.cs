using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.RealmBorders;

/// <summary>
/// Production <see cref="IRealmBordersSettings"/> over the MCM <c>TaomSettings</c> singleton,
/// re-validated here: a NaN, out-of-range or inverted fade distance falls back to the shipped
/// default rather than reaching the fade or the mesh widths, with one warning per bad value (the
/// settings are read every map frame, so a warning per read would flood the log). Config Providers
/// MUST Validate.
/// </summary>
public sealed class RealmBordersSettingsProvider : IRealmBordersSettings
{
    public const float DefaultWidthScale = 1f;
    public const float DefaultFadeStartDistance = 45f;
    public const float DefaultFullOpacityDistance = 110f;
    public const float DefaultFillStrength = 0.3f;
    public const float MinimumFillStrength = 0.05f;
    public const float MaximumFillStrength = 0.8f;

    /// <summary>The blend dropdown: "Material default", then the engine's modes in their enum order. Never reorder: MCM keeps the index.</summary>
    public static readonly string[] BlendModeChoices =
    {
        "Material default", "NoAlphaBlend", "Modulate", "AddAlpha", "Multiply", "Add", "Max", "Factor", "AddModulateCombined",
        "NoAlphaBlendNoWrite", "ModulateNoWrite", "GbufferAlphaBlend", "GbufferAlphaBlendWithVtResolve", "NoAlphaBlendNoAlphaWrite",
    };

    /// <summary>The material dropdown: "Automatic", then the renderer's candidates. Never reorder: MCM keeps the index.</summary>
    public static readonly string[] MaterialChoices = { "Automatic", "vertex_color_mat", "vertex_color_lighting" };

    /// <summary>Each realm's MCM colour field (Realm Borders/Realm Colours): its kingdom id, its label, its value.</summary>
    internal static readonly (string Realm, string Label, Func<TaomSettings, string?> Read)[] ColourFields =
    {
        ("empire_w", "Gondor", s => s.RealmColourGondor),
        ("vlandia", "Rohan", s => s.RealmColourRohan),
        ("empire", "Dunland", s => s.RealmColourDunland),
        ("isengard", "Isengard", s => s.RealmColourIsengard),
        ("empire_s", "Mordor", s => s.RealmColourMordor),
        ("aserai", "Harad", s => s.RealmColourHarad),
        ("umbar", "Umbar", s => s.RealmColourUmbar),
        ("shaghana", "Shaghâna", s => s.RealmColourShaghana),
        ("abanissa", "Âbanissa", s => s.RealmColourAbanissa),
        ("battania", "Khand", s => s.RealmColourKhand),
        ("khuzait", "Rhûn", s => s.RealmColourRhun),
        ("sturgia", "Dale", s => s.RealmColourDale),
        ("erebor", "Erebor", s => s.RealmColourErebor),
        ("rivendell", "Rivendell", s => s.RealmColourRivendell),
        ("lothlorien", "Lothlórien", s => s.RealmColourLothlorien),
        ("mirkwood", "Mirkwood", s => s.RealmColourMirkwood),
        ("lindon", "Lindon", s => s.RealmColourLindon),
        ("dolguldur", "Dol Guldur", s => s.RealmColourDolGuldur),
        ("gundabad", "Gundabad", s => s.RealmColourGundabad),
        ("mistymountainorcs", "Misty Mountain Orcs", s => s.RealmColourMistyMountainOrcs),
        ("goblin", "Goblins", s => s.RealmColourGoblins),
        ("bluecraig", "Goblins of Blue Craig", s => s.RealmColourBlueCraig),
    };

    /// <summary>The Your Realm field's slot in the colour arrays, after one slot per realm field.</summary>
    internal static int YourRealmSlot => ColourFields.Length;

    private const float MinimumWidthScale = 0.5f;
    private const float MaximumWidthScale = 3f;
    private const float MaximumFadeDistance = 5000f;

    private const string WidthSetting = "Border Width";
    private const string FadeSetting = "Fade In From Camera Distance / Full Opacity Distance";
    private const string FillStrengthSetting = "Realm Colour Strength";
    private const string YourRealmLabel = "Your Realm";

    private static readonly string DefaultWidthText = Text(DefaultWidthScale);
    private static readonly string DefaultFillStrengthText = Text(DefaultFillStrength);
    private static readonly string DefaultFadeText = $"{Text(DefaultFadeStartDistance)} / {Text(DefaultFullOpacityDistance)}";

    private readonly IModLogger _logger;
    private readonly Dictionary<string, string> _warned = new Dictionary<string, string>();
    private readonly string?[] _colourTexts = new string?[YourRealmSlot + 1];
    private readonly uint?[] _colours = new uint?[YourRealmSlot + 1];
    private readonly string?[] _currentTexts = new string?[YourRealmSlot + 1];
    private int _colourVersion;

    public RealmBordersSettingsProvider(IModLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool Enabled => TaomSettings.Instance?.EnableRealmBorders ?? true;

    public bool HeraldicBands => TaomSettings.Instance?.RealmBordersHeraldicBands ?? false;

    public bool GildPlayerRealm => TaomSettings.Instance?.RealmBordersGildPlayerRealm ?? true;

    public float WidthScale => CheckedWidthScale(TaomSettings.Instance?.RealmBordersWidthScale);

    public float FadeStartDistance => Fade().Start;

    public float FullOpacityDistance => Fade().Full;

    public bool DrawThroughTerrain => TaomSettings.Instance?.RealmBordersDrawThroughTerrain ?? true;

    public bool RealmNames => TaomSettings.Instance?.RealmBordersNames ?? true;

    public bool CrossingNotices => TaomSettings.Instance?.RealmBordersCrossingNotices ?? true;

    public bool FillLands => TaomSettings.Instance?.RealmBordersFillLands ?? true;

    public float FillStrength => CheckedFillStrength(TaomSettings.Instance?.RealmBordersFillStrength);

    public string? BlendMode => Choice(TaomSettings.Instance?.RealmBordersBlendMode?.SelectedIndex, BlendModeChoices);

    public string? MaterialName => Choice(TaomSettings.Instance?.RealmBordersMaterial?.SelectedIndex, MaterialChoices);

    public int ColourVersion
    {
        get
        {
            var settings = TaomSettings.Instance;
            for (int i = 0; i < ColourFields.Length; i++)
                _currentTexts[i] = settings == null ? null : ColourFields[i].Read(settings);
            _currentTexts[YourRealmSlot] = settings?.RealmColourYourRealm;
            return RefreshColours(_currentTexts);
        }
    }

    public uint? YourRealmColour => _colours[YourRealmSlot];

    public uint? ColourOverride(string realm)
    {
        for (int i = 0; i < ColourFields.Length; i++)
        {
            if (ColourFields[i].Realm == realm)
                return _colours[i];
        }
        return null;
    }

    /// <summary>
    /// Takes the colour fields' current texts (in <see cref="ColourFields"/> order, then Your Realm) and
    /// re-parses only the ones that changed; returns the version, bumped on any change. A malformed colour
    /// keeps the palette's and warns once.
    /// </summary>
    internal int RefreshColours(IReadOnlyList<string?> texts)
    {
        for (int i = 0; i < _colourTexts.Length; i++)
        {
            string? text = i < texts.Count ? texts[i] : null;
            if (string.Equals(text, _colourTexts[i], StringComparison.Ordinal))
                continue;
            _colourTexts[i] = text;
            _colours[i] = CheckedColour(i == YourRealmSlot ? YourRealmLabel : ColourFields[i].Label, text);
            _colourVersion++;
        }
        return _colourVersion;
    }

    private uint? CheckedColour(string label, string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            Report("Realm Colours > " + label, null, "the palette's");
            return null;
        }
        bool ok = RealmPaletteProvider.TryParseColour(trimmed, out uint colour);
        Report("Realm Colours > " + label, ok ? null : trimmed, "the palette's");
        return ok ? colour : (uint?)null;
    }

    /// <summary>A dropdown's choice by index; the first entry, or an index out of range, is null (the default).</summary>
    internal static string? Choice(int? index, string[] choices) =>
        index.HasValue && index.Value > 0 && index.Value < choices.Length ? choices[index.Value] : null;

    /// <summary>
    /// Both fade distances, as a pair: when either is missing, not finite, out of range, or the start
    /// is not below the full-opacity distance, both revert to the defaults, so one bad value cannot
    /// leave a half-default pair that fades backwards.
    /// </summary>
    internal static (float Start, float Full) SaneFade(float? start, float? full) =>
        ValidFade(start, full) ? (start!.Value, full!.Value) : (DefaultFadeStartDistance, DefaultFullOpacityDistance);

    private static bool ValidFade(float? start, float? full) =>
        start.HasValue && full.HasValue
        && FiniteFloatValidator.IsFiniteInRange(start.Value, 0f, MaximumFadeDistance)
        && FiniteFloatValidator.IsFiniteInRange(full.Value, 0f, MaximumFadeDistance)
        && start.Value < full.Value;

    /// <summary>The width multiplier, or the default with a warning when the setting is unusable.</summary>
    internal float CheckedWidthScale(float? raw)
    {
        bool bad = raw.HasValue && !FiniteFloatValidator.IsFiniteInRange(raw.Value, MinimumWidthScale, MaximumWidthScale);
        Report(WidthSetting, bad ? Text(raw) : null, DefaultWidthText);
        return Sane(raw, MinimumWidthScale, MaximumWidthScale, DefaultWidthScale);
    }

    /// <summary>The tint strength, or the default with a warning when the setting is unusable.</summary>
    internal float CheckedFillStrength(float? raw)
    {
        bool bad = raw.HasValue && !FiniteFloatValidator.IsFiniteInRange(raw.Value, MinimumFillStrength, MaximumFillStrength);
        Report(FillStrengthSetting, bad ? Text(raw) : null, DefaultFillStrengthText);
        return Sane(raw, MinimumFillStrength, MaximumFillStrength, DefaultFillStrength);
    }

    /// <summary>The fade pair, or both defaults with a warning when the pair is unusable. No settings is no warning.</summary>
    internal (float Start, float Full) CheckedFade(float? start, float? full)
    {
        bool bad = (start.HasValue || full.HasValue) && !ValidFade(start, full);
        Report(FadeSetting, bad ? $"{Text(start)} / {Text(full)}" : null, DefaultFadeText);
        return SaneFade(start, full);
    }

    internal static float Sane(float? raw, float min, float max, float fallback)
    {
        if (!raw.HasValue)
            return fallback;
        return FiniteFloatValidator.IsFiniteInRange(raw.Value, min, max) ? raw.Value : fallback;
    }

    private (float Start, float Full) Fade() =>
        CheckedFade(TaomSettings.Instance?.RealmBordersFadeStartDistance, TaomSettings.Instance?.RealmBordersFullOpacityDistance);

    /// <summary>
    /// Warns once when a setting turns unusable (<paramref name="badValue"/> set), again only when its
    /// value changes; a usable value (null) re-arms it. Text is built only for a bad value, since the
    /// settings are read every map frame.
    /// </summary>
    private void Report(string setting, string? badValue, string fallback)
    {
        if (badValue == null)
        {
            if (_warned.Count > 0)
                _warned.Remove(setting);
            return;
        }
        if (_warned.TryGetValue(setting, out var last) && last == badValue)
            return;
        _warned[setting] = badValue;
        _logger.LogWarning($"[RealmBorders] MCM Realm Borders > {setting} = {badValue} is out of range or not a number; using {fallback}.");
    }

    private static string Text(float? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "(unset)";
}
