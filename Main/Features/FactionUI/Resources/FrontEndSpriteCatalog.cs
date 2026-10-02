using System;
using TAOM.Adapters;

namespace TAOM.Features.FactionUI.Resources;

/// <summary>
/// Classifies Kysaro's runtime images by name and reads their <c>.nine</c> border files (#704). The art
/// prefixes are the images his faction screen binds by data (a faction's emblem and territory, a
/// hero's portrait and reveal, a background). No prefab or brush names one of them directly, and every
/// image a prefab or brush does name is chrome (fs_minimap_big included: TAOMFactionScreen names it).
/// The main menu's prefab and brush file name only <c>mm_*</c> images, and nothing else names one, so
/// they are a group of their own.
/// </summary>
public static class FrontEndSpriteCatalog
{
    private static readonly string[] ArtPrefixes =
    {
        "fs_portrait_",
        "fs_reveal_",
        "fs_emblem_",
        "fs_territory_",
        "fs_bg_",
    };

    private static readonly char[] NineSeparators = { ' ', ',', '\r', '\n', '\t' };

    public static FrontEndSpriteKind Classify(string spriteName)
    {
        if (spriteName.StartsWith("ld_", StringComparison.Ordinal))
            return FrontEndSpriteKind.Resident;
        if (spriteName.StartsWith("gui_skills_icon_", StringComparison.Ordinal))
            return FrontEndSpriteKind.SkillIcon;
        if (spriteName.StartsWith("mm_", StringComparison.Ordinal))
            return FrontEndSpriteKind.MenuChrome;
        foreach (var prefix in ArtPrefixes)
        {
            if (spriteName.StartsWith(prefix, StringComparison.Ordinal))
                return FrontEndSpriteKind.Art;
        }
        return FrontEndSpriteKind.Chrome;
    }

    /// <summary>Exactly four non-negative integers, in the engine's left, right, top, bottom order.</summary>
    public static bool TryParseNinePatch(string? text, out NinePatch ninePatch)
    {
        ninePatch = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text!.Split(NineSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return false;

        var values = new int[4];
        for (var i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], out values[i]) || values[i] < 0)
                return false;
        }

        ninePatch = new NinePatch(values[0], values[1], values[2], values[3]);
        return true;
    }
}
