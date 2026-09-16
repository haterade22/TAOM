using System;

namespace TAOM.Features.SignatureStrikes.Domain;

/// <summary>
/// The one place the config's direction and kind STRINGS turn into enums, shared by the
/// validating provider (which drops unknown names with a warning) and the service (which indexes
/// the validated rows). Two parsers would drift; a typo the provider let through must never be a
/// row the service silently fails to match (the M1 trap, csharp-architecture.md).
/// </summary>
public static class StrikeNames
{
    public static bool TryParseDirection(string? name, out StrikeDirection direction)
    {
        if (TryParseByName(name, out direction) && direction != StrikeDirection.None)
            return true;

        direction = StrikeDirection.None;
        return false;
    }

    public static bool TryParseKind(string? name, out StrikeKind kind)
        => TryParseByName(name, out kind);

    // By NAME only. Enum.TryParse also accepts "1" and "Left, Right", and Enum.IsDefined is true
    // for any defined numeric value, so a typo like "1" would silently become a live Overhead row
    // (Codex review 114, F2). Matching the member names is the only reading the doc promises.
    private static bool TryParseByName<TEnum>(string? name, out TEnum value) where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmed = name!.Trim();
            foreach (var member in Enum.GetNames(typeof(TEnum)))
            {
                if (string.Equals(member, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    value = (TEnum)Enum.Parse(typeof(TEnum), member);
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
