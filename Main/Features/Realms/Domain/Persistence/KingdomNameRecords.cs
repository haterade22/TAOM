using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Realms.Domain.Persistence;

/// <summary>A kingdom's own three names (the raw text with its {=id}), kept while a title names it otherwise.</summary>
public sealed class KingdomNameRecord
{
    public KingdomNameRecord(string name, string informalName, string formalName)
    {
        Name = name;
        InformalName = informalName;
        FormalName = formalName;
    }

    public string Name { get; }
    public string InformalName { get; }
    public string FormalName { get; }
}

/// <summary>
/// Writes and reads the kingdoms' own names for the save: one line per kingdom, its id and three names separated by
/// tabs. A line that cannot be read is left out.
/// </summary>
public static class KingdomNameRecords
{
    public static string Write(IReadOnlyDictionary<string, KingdomNameRecord> records) =>
        string.Join("\n", records.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => RealmsSaveFormat.Join(p.Key, p.Value.Name, p.Value.InformalName, p.Value.FormalName)));

    public static Dictionary<string, KingdomNameRecord> Read(string? text)
    {
        var records = new Dictionary<string, KingdomNameRecord>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text))
            return records;
        foreach (var line in text!.Split('\n'))
        {
            var fields = RealmsSaveFormat.Fields(line);
            if (fields.Length == 4 && fields.All(f => f != null) && fields[0]!.Length > 0)
                records[fields[0]!] = new KingdomNameRecord(fields[1]!, fields[2]!, fields[3]!);
        }

        return records;
    }
}
