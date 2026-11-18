using System;
using System.Xml;

namespace TAOM.Dependencies.Foundation;

/// <summary>Whether the running game is the Bannerlord version this TAOM build was made for.</summary>
public enum GameVersionMatch
{
    /// <summary>The game version or TAOM's pin could not be read: say nothing.</summary>
    Unknown,

    /// <summary>The game version matches TAOM's pin.</summary>
    Match,

    /// <summary>The game version differs from TAOM's pin.</summary>
    Mismatch,
}

/// <summary>
/// TAOM's DLLs are built against one Bannerlord version. On another version the engine's
/// Module.LoadSubModules fails TAOM.Client (or TAOM) at GetTypes or construction, and the player gets
/// the game's own "Error while loading TAOM" box: "submodule could not be loaded due to a missing or
/// conflicting dependency". Players do not read that as a version problem.
/// The supported version comes from TAOM's own SubModule.xml, the Native line of
/// DependedModuleMetadatas (<c>version="v1.5.7.*"</c>), so the pin travels with the TAOM build it
/// describes and needs no second copy here. The vanilla launcher ignores that block (it parses only
/// DependedModules), which is why nothing warns the player today. Pure: no engine types.
/// </summary>
public static class GameVersionCheck
{
    public const string NativeModuleId = "Native";

    /// <summary>
    /// The Native version pin and the module version from a TAOM SubModule.xml text. Each is null when
    /// absent. Never throws: a file that does not parse gives (null, null).
    /// </summary>
    public static (string? NativePin, string? TaomVersion) ReadSubModule(string? subModuleXml)
    {
        if (string.IsNullOrWhiteSpace(subModuleXml)) return (null, null);
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(subModuleXml);
            var root = doc.DocumentElement;
            if (root == null) return (null, null);

            var taomVersion = (root.SelectSingleNode("Version") as XmlElement)?.GetAttribute("value");
            string? pin = null;
            var metas = root.SelectNodes("DependedModuleMetadatas/DependedModuleMetadata");
            if (metas != null)
            {
                foreach (XmlNode node in metas)
                {
                    if (node is XmlElement e && string.Equals(e.GetAttribute("id"), NativeModuleId, StringComparison.OrdinalIgnoreCase))
                    {
                        pin = e.GetAttribute("version");
                        break;
                    }
                }
            }
            return (Blank(pin), Blank(taomVersion));
        }
        catch (XmlException)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Compares the game's version with a pin such as <c>v1.5.7.*</c>, <c>v1.5.7</c> or <c>1.5.*</c>.
    /// Every number the pin gives must be equal; a <c>*</c> part and anything after it match all.
    /// The game's change-set (the fourth number) is never compared. Unknown when the game version was
    /// not detected (major 0) or the pin does not parse, so a reading failure never shows a warning.
    /// </summary>
    public static GameVersionMatch Evaluate(string? pin, int major, int minor, int revision)
    {
        if (major <= 0) return GameVersionMatch.Unknown;
        var wanted = ParsePin(pin);
        if (wanted == null) return GameVersionMatch.Unknown;

        var game = new[] { major, minor, revision };
        for (int i = 0; i < wanted.Length && i < game.Length; i++)
        {
            if (wanted[i] == null) break;
            if (wanted[i] != game[i]) return GameVersionMatch.Mismatch;
        }
        return GameVersionMatch.Match;
    }

    /// <summary>
    /// The pin's numbers, null for a <c>*</c> part. Null when the pin is empty, has fewer than two
    /// parts before any <c>*</c>, or has a part that is neither a number nor <c>*</c>. A leading
    /// version letter (v, e, b...) is dropped, as the engine's ApplicationVersion.FromString does.
    /// </summary>
    internal static int?[]? ParsePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return null;
        var text = pin!.Trim();
        if (char.IsLetter(text[0])) text = text.Substring(1);
        var parts = text.Split('.');
        if (parts.Length < 2 || parts.Length > 4) return null;

        var result = new int?[parts.Length];
        int numbers = 0;
        bool wildcard = false;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "*") { wildcard = true; continue; }
            if (wildcard || !int.TryParse(parts[i], out var n) || n < 0) return null;
            result[i] = n;
            numbers++;
        }
        return numbers >= 2 ? result : null;
    }

    /// <summary>The pin as players see versions: <c>v1.5.7.*</c> becomes <c>v1.5.7</c>.</summary>
    public static string DisplayPin(string pin)
    {
        var text = pin.Trim();
        while (text.EndsWith(".*", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 2);
        return char.IsDigit(text[0]) ? "v" + text : text;
    }

    public const string Caption = "TAOM: wrong Bannerlord version";

    /// <summary>
    /// The warning text. Plain English with no language key, so the text also works in the native fallback box
    /// and when TAOM's own files are the problem. <paramref name="taomSkipped"/>: TAOM.Dependencies turned TAOM's code off and the game
    /// reached the main menu (the TAOM popup); otherwise the native box before the engine's own error box.
    /// </summary>
    public static string Text(string pin, string? taomVersion, int major, int minor, int revision, bool taomSkipped)
    {
        var wanted = DisplayPin(pin);
        var taom = string.IsNullOrEmpty(taomVersion) ? "This TAOM" : $"This TAOM ({taomVersion})";
        var what = taomSkipped
            ? "TAOM is turned off for this session, so the game would not work properly. Press Exit Game to close the game.\n\n"
            : "TAOM will most likely not load. If the game shows an error box after this one " +
              "(\"could not be loaded due to a missing or conflicting dependency\"), this is the reason.\n\n";
        return
            $"{taom} is made for Bannerlord {wanted}, but your game is v{major}.{minor}.{revision}.\n\n" +
            what +
            "To fix it, do one of these:\n" +
            $"- In Steam, right-click Mount & Blade II: Bannerlord, choose Properties, then Betas, and pick {wanted}. Let Steam update the game, then start again.\n" +
            "- Or, if a TAOM made for your game version is out, install it with the TAOM launcher.";
    }

    /// <summary>
    /// One paragraph that LoadErrorHint adds to the engine's own "could not be loaded due to a missing or conflicting
    /// dependency" box for TAOM. Most players who see that box run the wrong Bannerlord version. Plain English, for
    /// the reason <see cref="Text"/> gives. Never names a version that was not read.
    /// </summary>
    public static string LoadErrorHint(string? pin, string? taomVersion, int major, int minor, int revision)
    {
        const string cause = "This error is usually caused by the wrong Bannerlord version.";
        var taom = string.IsNullOrEmpty(taomVersion) ? "This TAOM" : $"This TAOM ({taomVersion})";
        if (ParsePin(pin) == null)
            return cause + " Check that your game is the Bannerlord version this TAOM is made for.";

        var wanted = DisplayPin(pin!);
        var steam = $" In Steam, right-click Mount & Blade II: Bannerlord, choose Properties, then Betas, and pick {wanted}.";
        if (major <= 0) return $"{cause} {taom} is made for Bannerlord {wanted}.{steam}";

        var game = $"v{major}.{minor}.{revision}";
        if (Evaluate(pin, major, minor, revision) == GameVersionMatch.Mismatch)
            return $"{cause} {taom} is made for Bannerlord {wanted}, but your game is {game}.{steam}";

        var lowerTaom = "t" + taom.Substring(1);
        return $"{cause} Your game is {game}, the version {lowerTaom} is made for, so the TAOM files may be mixed or out of date. " +
               "Install TAOM again with the TAOM launcher.";
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
}
