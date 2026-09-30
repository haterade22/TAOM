using System;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Chooses each line's paint for the current map mode. The caller groups provinces before selection,
/// so a line's two "realms" are realms (political), sides (Free Peoples and Shadow) or relation groups
/// (war). The player's own frontier takes the gold cord only in the political mode. In the war mode a
/// front, where the player's side meets an enemy, burns (brighter where the player's own land stands
/// on it), and every other line keeps the chosen look in its relation colours.
/// </summary>
public static class BorderStyleSelector
{
    public static LinePaint PaintFor(
        RealmBorderLine line,
        MapMode mode,
        bool heraldic,
        string? playerRealm,
        bool gildPlayerRealm,
        Func<string, uint> colourOf)
    {
        if (line == null)
            throw new ArgumentNullException(nameof(line));
        if (colourOf == null)
            throw new ArgumentNullException(nameof(colourOf));

        uint left = colourOf(line.LeftRealm), right = colourOf(line.RightRealm);
        var style = heraldic ? LineStyle.Heraldic : LineStyle.Atlas;
        if (mode == MapMode.War)
        {
            if (!RelationGroups.IsFront(line.LeftRealm, line.RightRealm))
                return new LinePaint(style, left, right, Gilded: false, Emphasised: false);
            bool ownFront = line.LeftRealm == RelationGroups.Own || line.RightRealm == RelationGroups.Own;
            return new LinePaint(LineStyle.WarFront, left, right, Gilded: false, Emphasised: ownFront);
        }

        bool players = playerRealm != null && (line.LeftRealm == playerRealm || line.RightRealm == playerRealm);
        bool gilded = mode == MapMode.Political && gildPlayerRealm && players;
        return new LinePaint(style, left, right, gilded, Emphasised: false);
    }
}
