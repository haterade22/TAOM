using TaleWorlds.InputSystem;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.MixedFormations;

/// <summary>
/// What the Mixed Formations cycle hotkey setting may say, and the cache that applies it every frame.
/// The text must be the NAME of one <see cref="InputKey"/> member, trimmed and in any case (<c>L</c>,
/// <c>f5</c>, <c>SemiColon</c>), and not <c>Invalid</c>. Anything else switches the hotkey off with one
/// WARNING per change (<see cref="CachedEnumParse{TEnum}"/>): a number in any spelling (<c>3</c>,
/// <c>+3</c>, <c>003</c>, <c>-1</c>), a list (<c>L, K</c>) and a chord (<c>Ctrl+L</c>).
/// <para>
/// By name only, through <see cref="EnumNames"/>, because <c>Enum.TryParse</c> takes numbers and ORs a
/// comma list into one value, and <c>Enum.IsDefined</c> is true for what comes out: <c>3</c> is the key
/// D2 and <c>L, K</c> is 38 | 37 = 39, the key SemiColon, so a typo became a different live key the
/// player never named, and a number no key has (<c>999</c>) went to <c>Input.IsKeyDown</c> every frame.
/// <c>Invalid</c> (-1) is a defined member that names no key, so the name lookup alone would keep it.
/// </para>
/// </summary>
internal static class CycleHotkeyParser
{
    internal static CachedEnumParse<InputKey> Create(IModLogger logger)
        => new CachedEnumParse<InputKey>(TryParseKeyName, logger, "[MixedFormations] cycle hotkey");

    private static bool TryParseKeyName(string text, out InputKey key)
    {
        if (EnumNames.TryParse(text, out key) && key != InputKey.Invalid)
            return true;

        key = default;
        return false;
    }
}
