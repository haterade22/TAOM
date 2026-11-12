using System.Collections.Generic;

namespace TAOM.CoopCompat.Handshake;

/// <summary>
/// How the handshake tells the player something. The service takes this seam so it needs no game
/// UI; the real one writes to the in-game message list. A notice only warns: the handshake never
/// disconnects a peer (maintainer decision D2).
/// </summary>
public interface ISettingsNotice
{
    /// <summary>These TAOM option groups differ from the server's.</summary>
    void SettingsDiffer(IReadOnlyList<string> groups);

    /// <summary>The protocol, TAOM version or a module build differs from the server's. Louder than <see cref="SettingsDiffer"/>.</summary>
    void BuildMismatch();
}
