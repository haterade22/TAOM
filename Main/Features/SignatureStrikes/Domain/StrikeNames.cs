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
        // Enum.TryParse accepts "3" as well as "Left"; IsDefined keeps a bare number out.
        if (Enum.TryParse(name, ignoreCase: true, out direction)
            && Enum.IsDefined(typeof(StrikeDirection), direction)
            && direction != StrikeDirection.None)
            return true;

        direction = StrikeDirection.None;
        return false;
    }

    public static bool TryParseKind(string? name, out StrikeKind kind)
        => Enum.TryParse(name, ignoreCase: true, out kind) && Enum.IsDefined(typeof(StrikeKind), kind);
}
