using TAOM.Core.Validation;

namespace TAOM.Features.SignatureStrikes.Domain;

/// <summary>
/// The one place the config's direction, kind and origin STRINGS turn into enums, shared by the
/// validating provider (which drops unknown names with a warning) and the service (which indexes
/// the validated rows). Two parsers would drift; a typo the provider let through must never be a
/// row the service silently fails to match (the M1 trap, csharp-architecture.md). Parsing is by
/// member name only, through <see cref="EnumNames"/>.
/// </summary>
public static class StrikeNames
{
    public static bool TryParseDirection(string? name, out StrikeDirection direction)
    {
        if (EnumNames.TryParse(name, out direction) && direction != StrikeDirection.None)
            return true;

        direction = StrikeDirection.None;
        return false;
    }

    public static bool TryParseKind(string? name, out StrikeKind kind)
        => EnumNames.TryParse(name, out kind);

    public static bool TryParseOrigin(string? name, out StrikeOrigin origin)
        => EnumNames.TryParse(name, out origin);
}
