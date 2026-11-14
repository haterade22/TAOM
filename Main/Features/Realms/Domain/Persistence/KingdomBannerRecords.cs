using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Realms.Domain.Persistence;

/// <summary>
/// Writes and reads the kingdoms' own banner codes for the save, kept while a title's banner flies in their place: one
/// line per kingdom, its id and banner code separated by a tab. A line that cannot be read is left out.
/// </summary>
public static class KingdomBannerRecords
{
    public static string Write(IReadOnlyDictionary<string, string> records) =>
        string.Join("\n", records.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => RealmsSaveFormat.Join(p.Key, p.Value)));

    public static Dictionary<string, string> Read(string? text)
    {
        var records = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text))
            return records;
        foreach (var line in text!.Split('\n'))
        {
            var fields = RealmsSaveFormat.Fields(line);
            if (fields.Length == 2 && !string.IsNullOrEmpty(fields[0]) && !string.IsNullOrEmpty(fields[1]))
                records[fields[0]!] = fields[1]!;
        }

        return records;
    }
}
