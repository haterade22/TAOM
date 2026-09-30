
namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// How a realm's name is lettered on the map: spaced capitals in TAOM's <c>aniron</c> font when that
/// font has every glyph, the way Tolkien's maps letter a realm, and the name as written in the plain
/// font otherwise. Decided per name from the glyphs, not per language, so a Polish or Turkish name
/// without its special letters still gets the Tolkien lettering and a Chinese one never shows boxes.
/// </summary>
public static class RealmNameLettering
{
    /// <summary>The code points <c>Main/_Module/GUI/Fonts/aniron.fnt</c> carries (267 glyphs, read 2026-09-30).</summary>
    private static readonly (int First, int Last)[] AnironGlyphs =
    {
        (32, 126), (160, 172), (174, 174), (176, 180), (182, 255), (1025, 1025), (1040, 1103), (1105, 1105),
        (8216, 8222), (8226, 8226), (8242, 8243), (8364, 8364), (8482, 8482),
    };

    public static bool AnironCovers(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        foreach (char ch in text)
        {
            if (!Covered(ch))
                return false;
        }
        return true;
    }

    /// <summary>The label text and whether it takes the aniron font.</summary>
    public static (string Text, bool Aniron) Letter(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (string.Empty, false);
        string upper = name.Trim().ToUpperInvariant();
        if (!AnironCovers(upper))
            return (name.Trim(), false);

        return (string.Join<char>(" ", upper), true);
    }

    private static bool Covered(char ch)
    {
        foreach (var (first, last) in AnironGlyphs)
        {
            if (ch >= first && ch <= last)
                return true;
        }
        return false;
    }
}
