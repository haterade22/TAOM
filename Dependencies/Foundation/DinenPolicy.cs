using System;
using System.Collections.Generic;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The decisions of <see cref="Dinen"/>, the Modding Kit's assert silencer, kept free of native calls so they can be
/// tested. Ported from QuietEditor (MIT, docs/reference/provenance-register.md); see docs/features/dinen.md.
/// </summary>
internal static class DinenPolicy
{
    /// <summary>The return value that lets the real <c>MessageBoxA</c> show the box.</summary>
    internal const int ShowBox = 0;

    private const int IdOk = 1;
    private const int IdIgnore = 5;
    private const int IdYes = 6;
    private const int IdNo = 7;
    private const int IdContinue = 11;
    private const string AlwaysIgnore = "Always Ignore?";

    private static readonly HashSet<string> Captions = new(StringComparer.Ordinal)
    {
        "ASSERT", "SAFE ASSERT", AlwaysIgnore, "Safe Mode", "*_*",
    };

    /// <summary>
    /// The button <c>MessageBoxA</c> returns for a box with this caption and style, or <see cref="ShowBox"/>. For the
    /// styles the v1.5.7 engine uses (docs/features/dinen.md "Answers"): Ignore an assert, OK to "Always Ignore?" (always
    /// ignore), No to "Safe Mode" and "*_*", OK to an RGL box. Yes to "Always Ignore?" covers a Yes/No style.
    /// A style that offers none of them, and every other caption, shows the box. Abort and Retry are never answered.
    /// </summary>
    internal static int AnswerFor(string? caption, uint style)
    {
        var title = (caption ?? string.Empty).Trim();
        if (!Captions.Contains(title) && title.IndexOf("RGL", StringComparison.OrdinalIgnoreCase) < 0)
            return ShowBox;

        return (style & 0xF) switch
        {
            0u or 1u => IdOk,
            2u => IdIgnore,
            3u or 4u => title == AlwaysIgnore ? IdYes : IdNo,
            6u => IdContinue,
            _ => ShowBox,
        };
    }

    /// <summary>
    /// The image offsets of the import address table slots through which a loaded PE32+ image calls
    /// <paramref name="function"/> of <paramref name="dll"/>. The readers take offsets from the image base. Ordinal
    /// imports are skipped. A descriptor with no import lookup table is skipped too: in a loaded image its address table
    /// holds resolved addresses, not name offsets. Delay-load imports are not read: the v1.5.7 TaleWorlds.Native.dll has none.
    /// </summary>
    internal static IReadOnlyList<int> FindImportSlots(
        Func<int, int> readInt32, Func<int, long> readInt64, Func<int, string> readAnsi, string dll, string function)
    {
        var slots = new List<int>();
        var header = readInt32(0x3C);
        var descriptor = readInt32(header + 24 + 112 + 8);
        if (descriptor == 0)
            return slots;

        for (; ; descriptor += 20)
        {
            var name = readInt32(descriptor + 12);
            if (name == 0)
                return slots;
            if (!string.Equals(readAnsi(name), dll, StringComparison.OrdinalIgnoreCase))
                continue;

            var nameTable = readInt32(descriptor);
            var addressTable = readInt32(descriptor + 16);
            if (nameTable == 0)
                continue;
            for (var offset = 0; ; offset += 8)
            {
                var thunk = readInt64(nameTable + offset);
                if (thunk == 0)
                    break;
                if (thunk > 0 && readAnsi((int)thunk + 2) == function)
                    slots.Add(addressTable + offset);
            }
        }
    }
}
