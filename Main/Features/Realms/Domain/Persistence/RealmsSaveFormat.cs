using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Persistence;

public sealed class SaveReadResult
{
    public SaveReadResult(TitleCatalog? catalog, TitleLedger? ledger, string? error, IReadOnlyList<string> warnings, int version = 0)
    {
        Catalog = catalog;
        Ledger = ledger;
        Error = error;
        Warnings = warnings;
        Version = version;
    }

    /// <summary>The format version the payload was written in, so the caller can bring older saves up to date.</summary>
    public int Version { get; }

    public TitleCatalog? Catalog { get; }
    public TitleLedger? Ledger { get; }

    /// <summary>Set when the payload cannot be used; the caller must keep the raw payload rather than overwrite it.</summary>
    public string? Error { get; }

    public IReadOnlyList<string> Warnings { get; }
    public bool Succeeded => Error == null;
}

/// <summary>
/// The campaign save payload: one tab-separated record per line, under a versioned header.
/// T = title, H = holder, C = claim, L = laws, D = default laws, P = pending succession, R = chronicle entry (v2),
/// O = office and its holder (v2, \0 when vacant).
/// Unknown record types are skipped with a warning so later versions can add records.
/// </summary>
public static class RealmsSaveFormat
{
    public const string Header = "TAOM.Realms";

    /// <summary>1: titles, holders, claims, laws, pending. 2: chronicle and offices; Gondor's throne made vacant on load of a 1.</summary>
    public const int Version = 2;
    private const string Null = "\\0";

    public static string Write(TitleCatalog catalog, TitleLedger ledger)
    {
        var lines = new List<string> { Join(Header, Version.ToString(CultureInfo.InvariantCulture)) };
        lines.Add(Join("D", ledger.DefaultLaws.Succession.ToString(), ledger.DefaultLaws.Gender.ToString()));
        foreach (var t in catalog.All)
            lines.Add(Join("T", t.Id, t.Rank.ToString(), t.Name, t.ParentId, t.SettlementId, t.KingdomId, t.CultureId));
        foreach (var holder in ledger.Holders.OrderBy(h => h.Key, StringComparer.Ordinal))
            lines.Add(Join("H", holder.Key, holder.Value));
        foreach (var c in ledger.AllClaims.OrderBy(c => c.TitleId, StringComparer.Ordinal).ThenBy(c => c.HeroId, StringComparer.Ordinal))
            lines.Add(Join("C", c.TitleId, c.HeroId, c.Kind.ToString(), c.CampaignDay.ToString("R", CultureInfo.InvariantCulture)));
        foreach (var laws in ledger.Laws.OrderBy(l => l.Key, StringComparer.Ordinal))
            lines.Add(Join("L", laws.Key, laws.Value.Succession.ToString(), laws.Value.Gender.ToString()));
        foreach (var p in ledger.Pending)
            lines.Add(Join("P", p.TitleId, p.Kind.ToString(), p.ReferenceId, p.PreviousHolderId));
        foreach (var chronicle in ledger.Chronicles.OrderBy(c => c.Key, StringComparer.Ordinal))
            foreach (var e in chronicle.Value)
                lines.Add(Join("R", chronicle.Key, e.CampaignDay.ToString("R", CultureInfo.InvariantCulture), e.Reason.ToString(), e.HeroId, e.PreviousHolderId));
        foreach (var office in ledger.KnownOffices.OrderBy(o => o, StringComparer.Ordinal))
            lines.Add(Join("O", office, ledger.OfficeHolderOf(office)));
        foreach (var primary in ledger.ChosenPrimaries.OrderBy(p => p.Key, StringComparer.Ordinal))
            lines.Add(Join("M", primary.Key, primary.Value));
        return string.Join("\n", lines);
    }

    public static SaveReadResult Read(string payload)
    {
        var warnings = new List<string>();
        var lines = payload.Split('\n');
        var header = Fields(lines[0]);
        if (header.Length < 2 || header[0] != Header)
            return Failed("The payload has no TAOM.Realms header.", warnings);
        if (!int.TryParse(header[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) || version > Version)
            return Failed($"Save format version '{header[1]}' is newer than this build ({Version}).", warnings);

        var definitions = new List<TitleDefinition>();
        var ledger = new TitleLedger();
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
                continue;
            var f = Fields(lines[i]);
            if (!TryReadRecord(f, definitions, ledger))
                warnings.Add($"Line {i + 1} ('{f[0]}') could not be read and was skipped.");
        }

        var catalog = new TitleCatalog(definitions);
        warnings.AddRange(catalog.Problems);
        return new SaveReadResult(catalog, ledger, null, warnings, version);
    }

    private static bool TryReadRecord(string?[] f, List<TitleDefinition> definitions, TitleLedger ledger)
    {
        switch (f[0])
        {
            case "T" when f.Length >= 8 && f[1] != null && Enum.TryParse<TitleRank>(f[2], out var rank):
                definitions.Add(new TitleDefinition(f[1]!, rank, f[3] ?? string.Empty, f[4], f[5], f[6], f[7]));
                return true;
            case "H" when f.Length >= 3 && f[1] != null && f[2] != null:
                ledger.SetHolder(f[1]!, f[2]);
                return true;
            case "C" when f.Length >= 5 && f[1] != null && f[2] != null && Enum.TryParse<ClaimKind>(f[3], out var kind)
                          && double.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var day):
                ledger.AddClaim(new TitleClaim(f[1]!, f[2]!, kind, day));
                return true;
            case "L" when f.Length >= 4 && f[1] != null && TryLaws(f[2], f[3], out var laws):
                ledger.SetLaws(f[1]!, laws);
                return true;
            case "D" when f.Length >= 3 && TryLaws(f[1], f[2], out var defaults):
                ledger.DefaultLaws = defaults;
                return true;
            case "P" when f.Length >= 5 && f[1] != null && f[3] != null && Enum.TryParse<PendingKind>(f[2], out var pendingKind):
                ledger.AddPending(new PendingSuccession(f[1]!, pendingKind, f[3]!, f[4]));
                return true;
            case "R" when f.Length >= 6 && f[1] != null && double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var when)
                          && Enum.TryParse<Services.TitleEventReason>(f[3], out var reason):
                ledger.Record(f[1]!, new ChronicleEntry(when, reason, f[4], f[5]), int.MaxValue);
                return true;
            case "O" when f.Length >= 3 && f[1] != null:
                ledger.KnownOffices.Add(f[1]!);
                ledger.SetOfficeHolder(f[1]!, f[2]);
                return true;
            case "M" when f.Length >= 3 && f[1] != null && f[2] != null:
                ledger.SetChosenPrimary(f[1]!, f[2]);
                return true;
            default:
                return false;
        }
    }

    private static bool TryLaws(string? succession, string? gender, out TitleLaws laws)
    {
        laws = TitleLaws.Default;
        if (!Enum.TryParse<SuccessionLaw>(succession, out var s) || !Enum.TryParse<GenderLaw>(gender, out var g))
            return false;
        laws = new TitleLaws(s, g);
        return true;
    }

    private static SaveReadResult Failed(string error, List<string> warnings) => new(null, null, error, warnings);

    internal static string Join(params string?[] fields) => string.Join("\t", fields.Select(Escape));

    internal static string?[] Fields(string line) => line.Split('\t').Select(Unescape).ToArray();

    private static string Escape(string? value)
    {
        if (value == null)
            return Null;
        return value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private static string? Unescape(string value)
    {
        if (value == Null)
            return null;
        if (value.IndexOf('\\') < 0)
            return value;
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 == value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            var next = value[++i];
            builder.Append(next switch { 't' => '\t', 'n' => '\n', 'r' => '\r', _ => next });
        }

        return builder.ToString();
    }
}
