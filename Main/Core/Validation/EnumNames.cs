using System;

namespace TAOM.Core.Validation;

/// <summary>
/// Turns a config string into an enum by member NAME only: surrounding whitespace is trimmed and
/// case is ignored. <c>Enum.TryParse</c> also accepts a number ("1") and a comma list
/// ("Left, Right"), and <c>Enum.IsDefined</c> is true for any defined number, so a typo became a
/// live value: "1" read as a SignatureStrikes Overhead row (Codex review 114, F2), and a padded,
/// numeric or combined BannerBearers formation entry never matched the service's name compare, so
/// as the only entry it switched every bearer off (the Codex review 130 follow-up).
/// </summary>
public static class EnumNames
{
    public static bool TryParse<TEnum>(string? name, out TEnum value) where TEnum : struct, Enum
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
