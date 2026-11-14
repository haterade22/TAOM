using System;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>
/// How tall a title tree card must be to hold its title, holder and folded count when they wrap (#1255). The view model
/// has the text but cannot measure it, so this estimates each text's lines by a greedy word wrap at a per-character width
/// set wider than FS_Garamond's average: it may reserve a line too many, rarely one too few. The fonts, text widths and
/// text tops mirror the card groups of RealmsTitleTreePopup.xml (TitleTreePrefabTests pins them). Pure, so the preview
/// and the tests run it without the game.
/// </summary>
internal static class TitleTreeTextFit
{
    public const float RootTextTop = 104f;
    public const float RootTitleFont = 24f;
    public const float RootHolderFont = 17f;
    public const float RootSmallFont = 15f;
    public const float RootHolderGap = 2f;

    public const float NodeTextTop = 94f;
    public const float NodeTitleFont = 20f;
    public const float NodeSmallFont = 15f;

    /// <summary>The row's text column: the row less the pennant's 52 on the left and 4 on the right.</summary>
    public const float RowTextWidth = TitleTreeCanvasLayout.RowWidth - 56f;
    public const float RowTitleFont = 18f;
    public const float RowSmallFont = 15f;

    public const float RootTextWidth = TitleTreeCanvasLayout.RootWidth;
    public const float NodeTextWidth = TitleTreeCanvasLayout.NodeWidth;

    /// <summary>A character's width as a share of the font size; FS_Garamond measured 0.46 in the preview.</summary>
    public const float CharWidth = 0.55f;

    /// <summary>A line's height as a share of the font size, as measured in the preview.</summary>
    public const float LineHeight = 1.06f;

    /// <summary>Kept below the last line.</summary>
    public const float BottomPad = 2f;

    /// <summary>The card height for this text: the kind's own height, or more when its text needs it.</summary>
    public static float CardHeight(TitleTreeCardKind kind, string? title, string? holder, string? folded)
    {
        float need = kind switch
        {
            TitleTreeCardKind.Root => RootTextTop + Block(title, RootTitleFont, RootTextWidth) + RootHolderGap
                + Block(holder, RootHolderFont, RootTextWidth) + Block(folded, RootSmallFont, RootTextWidth),
            TitleTreeCardKind.Node => NodeTextTop + Block(title, NodeTitleFont, NodeTextWidth)
                + Block(holder, NodeSmallFont, NodeTextWidth) + Block(folded, NodeSmallFont, NodeTextWidth),
            _ => Block(title, RowTitleFont, RowTextWidth) + Block(holder, RowSmallFont, RowTextWidth)
                + Block(folded, RowSmallFont, RowTextWidth),
        };
        return Math.Max(TitleTreeCanvasLayout.DefaultHeightOf(kind), need + BottomPad);
    }

    /// <summary>The lines a greedy word wrap gives <paramref name="text"/> at this font size and width.</summary>
    public static int Lines(string? text, float fontSize, float width)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        float space = CharWidth * fontSize;
        int lines = 1;
        float used = 0f;
        foreach (var word in text!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            float length = word.Length * space;
            if (used > 0f && used + space + length > width)
            {
                lines++;
                used = length;
            }
            else
            {
                used += (used > 0f ? space : 0f) + length;
            }
        }
        return lines;
    }

    private static float Block(string? text, float fontSize, float width) => Lines(text, fontSize, width) * fontSize * LineHeight;
}
