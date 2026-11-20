using System;
using System.Collections.Generic;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Features.Stamina.Cheats;

/// <summary>Whose stamina <c>taom.set_stamina</c> changes.</summary>
public enum StaminaCheatTarget
{
    Player,
    Allies,
    Enemies,
    All,
}

/// <summary>Reads the two words of <c>taom.set_stamina</c>. Pure, so a mistyped word has a test.</summary>
public static class StaminaCheatArgs
{
    /// <summary>
    /// <paramref name="band"/> is null for the word "auto": the rules decide again. False with a message when a word
    /// is missing or not known.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> args, out StaminaBand? band, out StaminaCheatTarget target, out string error)
    {
        band = null;
        target = StaminaCheatTarget.Player;
        error = "";
        if (args == null || args.Count != 2)
        {
            error = "Expected a band and a target.";
            return false;
        }

        var bandWord = (args[0] ?? "").Trim();
        if (!string.Equals(bandWord, "auto", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryName(bandWord, out StaminaBand parsed))
            {
                error = $"Unknown band \"{args[0]}\".";
                return false;
            }

            band = parsed;
        }

        if (!TryName((args[1] ?? "").Trim(), out target))
        {
            target = StaminaCheatTarget.Player;
            error = $"Unknown target \"{args[1]}\".";
            return false;
        }

        return true;
    }

    // One of the enum's own names, in any case. Enum.TryParse is not used: it also reads a number ("3"), a sign
    // ("+1") and a comma list ("winded,tired") as a value.
    private static bool TryName<T>(string word, out T value)
        where T : struct
    {
        foreach (var name in Enum.GetNames(typeof(T)))
        {
            if (!string.Equals(name, word, StringComparison.OrdinalIgnoreCase))
                continue;
            value = (T)Enum.Parse(typeof(T), name);
            return true;
        }

        value = default;
        return false;
    }
}
