using TaleWorlds.Core;

namespace TAOM.Features.BannerColorPersistence.Hooks;

/// <summary>
/// Recolours a portrait <see cref="CharacterCode"/> so the clan colours reach the screen (#993).
/// <c>CharacterCode.Code</c> has a private setter and is built when the code is created; the portrait
/// renderer and <c>CharacterImageIdentifier</c> read only that string, so assigning <c>Color1</c> and
/// <c>Color2</c> on a finished code changed nothing visible. The 8-argument <c>CreateFrom</c> bakes
/// the colours into <c>Code</c>, so the result is a rebuilt code.
/// </summary>
internal static class ClanColoredCharacterCode
{
    /// <summary>Returns <paramref name="code"/> itself when it is null, empty (hidden hero information
    /// returns <c>CreateEmpty</c>) or already carries both colours; otherwise a rebuilt copy.</summary>
    internal static CharacterCode Recolor(CharacterCode? code, uint color1, uint color2)
    {
        if (code == null || code.IsEmpty)
            return code!;
        if (code.Color1 == color1 && code.Color2 == color2)
            return code;

        return CharacterCode.CreateFrom(
            code.EquipmentCode, code.BodyProperties, code.IsFemale, code.IsHero,
            color1, color2, code.FormationClass, code.Race);
    }
}
