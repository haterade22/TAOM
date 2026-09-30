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

    private const float MinimumWidthScale = 0.5f;
    private const float MaximumWidthScale = 3f;
    private const float MaximumFadeDistance = 5000f;

    private const string WidthSetting = "Border Width";
    private const string FadeSetting = "Fade In From Camera Distance / Full Opacity Distance";

    private static readonly string DefaultWidthText = Text(DefaultWidthScale);
    private static readonly string DefaultFadeText = $"{Text(DefaultFadeStartDistance)} / {Text(DefaultFullOpacityDistance)}";

    private readonly IModLogger _logger;
    private readonly Dictionary<string, string> _warned = new Dictionary<string, string>();

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
