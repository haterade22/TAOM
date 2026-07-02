using System;
using System.Collections.Generic;
using TAOM.Features.HeroRace.Configuration;
using TaleWorlds.Library;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Pure race-position math for character/mount tableaus — extracted from
/// <see cref="CharacterTableauService"/> (R5, 2026-07-01), where the offset block was duplicated
/// four times (character + mount frames in both refresh paths) with zero tests. The config axis
/// mapping is deliberately unintuitive and now pinned by tests: <c>Horizontal</c> offsets
/// <c>origin.y</c>, <c>Vertical</c> offsets <c>origin.z</c>, <c>Zoom</c> offsets <c>origin.x</c>
/// (camera-relative naming from the donor CharacterAvatarPatch config, not world axes).
/// </summary>
public static class RaceTableauPositioning
{
    /// <summary>Case-insensitive race → position-offset lookup; empty/null race rows are skipped, duplicates last-wins.</summary>
    public static Dictionary<string, RacePositionConfigItem> BuildLookup(RacePositionConfig config)
    {
        var lookup = new Dictionary<string, RacePositionConfigItem>(StringComparer.OrdinalIgnoreCase);
        if (config?.Items != null)
        {
            foreach (var item in config.Items)
            {
                if (!string.IsNullOrEmpty(item.Race))
                {
                    lookup[item.Race] = item;
                }
            }
        }
        return lookup;
    }

    /// <summary>
    /// Returns <paramref name="baseFrame"/> shifted by the race's configured offsets; a null
    /// <paramref name="item"/> (race not configured) returns the base frame unchanged.
    /// </summary>
    public static MatrixFrame ApplyOffset(MatrixFrame baseFrame, RacePositionConfigItem item)
    {
        if (item == null)
            return baseFrame;

        var frame = new MatrixFrame(baseFrame.rotation, baseFrame.origin);
        frame.origin.y = frame.origin.y + item.Horizontal;
        frame.origin.z = frame.origin.z + item.Vertical;
        frame.origin.x = frame.origin.x + item.Zoom;
        return frame;
    }
}
