using System;
using System.Globalization;
using TAOM.Core.Validation;
using TAOM.Features.HeroRace.Configuration;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace.Cheats;

/// <summary>
/// Argument parsing and validation for the race-framing console commands, kept separate from the
/// <c>[CommandLineArgumentFunction]</c> statics so it can be unit tested.
///
/// <para>The console statics are entry points (ADR-002): the engine invokes them across a native
/// reverse-P/Invoke, so nothing there is reachable from a test. Leaving the parsing inline made the
/// only arithmetic in the tuner, the nudge bound check, reachable solely through a live game. This
/// class is the seam that fixes that, matching how <c>CultureConversionCheats</c> exposes its
/// formatting for <c>CultureConversionCheatsFormatTests</c>.</para>
///
/// <para>Race validity is passed in as a predicate rather than resolved from IoC here, so the
/// tests do not need a container.</para>
/// </summary>
internal static class RacePositionTuningParser
{
    internal const string MountPrefix = "mount_";

    internal static bool TryParseSurface(string raw, out RacePositionSurface surface, out string error)
    {
        surface = RacePositionSurface.Avatar;
        error = null;

        switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "avatar":
                surface = RacePositionSurface.Avatar;
                return true;
            case "image":
                surface = RacePositionSurface.Image;
                return true;
            default:
                error = $"Unknown surface '{raw}'. Expected 'avatar' or 'image'.";
                return false;
        }
    }

    internal static bool TryParseAxis(string raw, out string axis, out string error)
    {
        axis = (raw ?? string.Empty).Trim().ToLowerInvariant();
        error = null;

        if (axis == "h" || axis == "v" || axis == "z")
            return true;

        error = $"Unknown axis '{raw}'. Expected h, v or z.";
        axis = null;
        return false;
    }

    internal static bool TryParseOffset(string raw, string label, out float value, out string error)
    {
        error = null;

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            value = 0f;
            error = $"Could not read {label} '{raw}' as a number.";
            return false;
        }

        if (!IsInRange(value))
        {
            error = $"{label} {value} is outside "
                  + $"[{RacePositionConfigValidator.MinOffset}, {RacePositionConfigValidator.MaxOffset}].";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the race argument. <c>"."</c> means the race the on-screen tableau is showing, which
    /// the caller supplies as <paramref name="liveRaceName"/> (null when nothing is on screen).
    /// </summary>
    /// <remarks>
    /// The race is validated against the engine's own race table. Without that, a typo creates a row
    /// that looks accepted, gets persisted by <c>taom.save_race_offsets</c>, and is then dead in the
    /// config forever because no lookup will ever ask for it.
    /// </remarks>
    internal static bool TryResolveRace(
        string raw,
        RacePositionSurface surface,
        Func<string, bool> isValidRaceName,
        string liveRaceName,
        out string race,
        out string error)
    {
        race = null;
        error = null;

        var value = (raw ?? string.Empty).Trim();

        if (value == ".")
        {
            if (string.IsNullOrEmpty(liveRaceName))
            {
                error = "No character tableau has been shown yet, so '.' cannot resolve a race. "
                      + "Open an inventory or party screen first, or name the race explicitly.";
                return false;
            }

            race = liveRaceName.ToLowerInvariant();
            return true;
        }

        if (string.IsNullOrEmpty(value))
        {
            error = "Expected a race name.";
            return false;
        }

        var candidate = value.ToLowerInvariant();
        var isMount = candidate.StartsWith(MountPrefix, StringComparison.Ordinal);

        // The 2D portrait surface has no mount row anywhere: CharacterSpawnerService only ever calls
        // ResolveImage with a plain race name. Accepting mount_ here would create a row that reports
        // success, persists, and is read by nothing.
        if (isMount && surface == RacePositionSurface.Image)
        {
            error = "The 'image' surface has no mount row: the 2D portrait path never looks one up. "
                  + "mount_ offsets exist only on the 'avatar' surface.";
            return false;
        }

        var bareRace = isMount ? candidate.Substring(MountPrefix.Length) : candidate;

        if (string.IsNullOrEmpty(bareRace))
        {
            error = "Expected a race name after 'mount_'.";
            return false;
        }

        if (isValidRaceName != null && !isValidRaceName(bareRace))
        {
            error = $"'{bareRace}' is not a race this game knows about. "
                  + "Run taom.print_race_offsets to see the race the current tableau is showing, "
                  + "or use '.' to target it.";
            return false;
        }

        race = candidate;
        return true;
    }

    /// <summary>
    /// Applies a nudge to one axis and reports whether the result is in range. The row is NOT
    /// mutated: an out-of-range nudge must leave the row untouched rather than clamp, or repeated
    /// nudges silently stop having an effect.
    /// </summary>
    internal static bool TryNudge(
        RacePositionConfigItem item,
        string axis,
        float delta,
        out float horizontal,
        out float vertical,
        out float zoom)
    {
        horizontal = item.Horizontal;
        vertical = item.Vertical;
        zoom = item.Zoom;

        if (!FiniteFloatValidator.IsFinite(delta))
            return false;

        if (axis == "h") horizontal = item.Horizontal + delta;
        else if (axis == "v") vertical = item.Vertical + delta;
        else if (axis == "z") zoom = item.Zoom + delta;
        else return false;

        return IsInRange(horizontal) && IsInRange(vertical) && IsInRange(zoom);
    }

    internal static bool IsInRange(float value)
        => FiniteFloatValidator.IsFiniteInRange(
            value, RacePositionConfigValidator.MinOffset, RacePositionConfigValidator.MaxOffset);

    internal static string Format(RacePositionConfigItem item)
        => $"h={item.Horizontal:0.000} v={item.Vertical:0.000} z={item.Zoom:0.000}";
}
