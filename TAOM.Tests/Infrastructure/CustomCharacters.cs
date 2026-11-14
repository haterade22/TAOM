using System;
using System.Collections.Generic;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The named companions that are not from Tolkien's books: characters made by and for the dev team and supporters.
/// Their creators decide how they look, so people-wide look passes leave them alone (owner, 2026-11-14: "i meant to
/// not touch the custom non lotr characters"). <see cref="HeroFaceKeys.HeroFaces"/> skips them, and
/// <c>CustomCharacterLookTests</c> pins each one's face as authored.
/// </summary>
internal static class CustomCharacters
{
    internal static readonly IReadOnlyCollection<string> Ids = new HashSet<string>(StringComparer.Ordinal)
    {
        "named_companion_swan",
        "named_companion_rcmcplayz",
        "named_companion_leovsilva",
        "named_companion_yotthani",
        "named_companion_whitegoat",
        "named_companion_igor",
        "named_companion_thyrell",
        "named_companion_haterade",
        "named_companion_hart",
        "named_companion_blackrose",
        "named_companion_spartan",
        "named_companion_solus",
        "named_companion_maztog",
        "named_companion_balakhor",
        "named_companion_elegost",
    };

    internal static bool Contains(string id) => ((HashSet<string>)Ids).Contains(id);
}
