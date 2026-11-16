using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms.Domain.Persistence;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms;

/// <summary>
/// Names a kingdom after the higher title its ruler holds (<see cref="RealmsService.RealmNameTitle"/>): Erebor becomes
/// Durin's Folk while its king holds that title, and takes its own name back when the title is lost. The kingdom's own
/// names are kept in the save until then. Names are set again every session, so a change of language or of the file
/// shows on the next load. A name the player gave in the kingdom screen is never replaced (<see cref="RenamedByPlayer"/>).
/// </summary>
internal sealed class KingdomNames
{
    private Dictionary<string, KingdomNameRecord> _originals = new(StringComparer.Ordinal);

    /// <summary>The title each kingdom was named after this session; not saved.</summary>
    private readonly Dictionary<string, string> _applied = new(StringComparer.Ordinal);

    /// <summary>The name Realms gave each kingdom this session; not saved.</summary>
    private readonly Dictionary<string, string> _appliedNames = new(StringComparer.Ordinal);

    /// <summary>
    /// True while the kingdom still has the name Realms set (or no name was set this session). False means other code,
    /// such as another mod, renamed it since, and that name stays when the title is lost (#1297). A kingdom screen
    /// rename never reaches this test: <see cref="RenamedByPlayer"/> catches it first.
    /// </summary>
    internal static bool StillNamedByRealms(string? currentName, string? appliedName) =>
        appliedName == null || string.Equals(currentName, appliedName, StringComparison.Ordinal);

    /// <summary>The raw text of Native's str_generic_kingdom_name, which the kingdom screen's rename sets.</summary>
    internal const string KingdomScreenText = "{=generickingdomname}";

    /// <summary>
    /// True for a name the player gave in the kingdom screen: KingdomManagementVM.OnChangeKingdomNameDone sets
    /// str_generic_kingdom_name with the typed name as its variable, and the save keeps both. Such a name beats every
    /// title name, in this session and after a load (#1316). The text without its variable is no player's name: it is
    /// what builds before #1316 could save as a kingdom's own name, and it shows an error, so a title may replace it.
    /// </summary>
    internal static bool RenamedByPlayer(string? rawName, bool hasVariables) =>
        hasVariables && rawName != null && rawName.StartsWith(KingdomScreenText, StringComparison.Ordinal);

    /// <summary>
    /// False for an own name saved by a build before #1316 as the kingdom screen's text without its variable: brought
    /// back, it would show an error, so the kingdom keeps the name it has.
    /// </summary>
    internal static bool Restorable(string ownName) => !ownName.StartsWith(KingdomScreenText, StringComparison.Ordinal);

    /// <summary>
    /// The text kept for a kingdom's own name: the raw text with its {=key}, so it follows a change of language, but the
    /// resolved text when the name has variables set (Attributes), which the record cannot keep (#1316). A settlement
    /// name always has variables set, so it is read by <see cref="WorldSnapshotReader.SettlementNameText"/> instead.
    /// </summary>
    internal static string OwnText(string? rawName, bool hasVariables, Func<string> resolved, string fallback) =>
        rawName == null ? fallback : hasVariables ? resolved() : rawName;

    internal static string OwnText(TextObject? name, string fallback) =>
        OwnText(name?.Value, name?.Attributes is { Count: > 0 }, () => name!.ToString(), fallback);

    public string Save() => KingdomNameRecords.Write(_originals);

    public void Load(string? text)
    {
        _originals = KingdomNameRecords.Read(text);
        _applied.Clear();
        _appliedNames.Clear();
    }

    /// <summary>
    /// Renames the kingdoms whose naming title changed, telling <paramref name="renamed"/> each kingdom's id. Cheap: a few
    /// title lookups per kingdom. A kingdom <paramref name="renames"/> refuses (the MCM option for its ruler) keeps,
    /// or takes back, its own name.
    /// </summary>
    public void Update(RealmsService service, Domain.Config.RealmsConfig config, Action<string>? renamed = null,
        Func<Kingdom, bool>? renames = null)
    {
        foreach (var kingdom in Kingdom.All)
        {
            var id = kingdom?.StringId;
            if (id == null || kingdom!.IsEliminated)
                continue;
            if (RenamedByPlayer(kingdom.Name?.Value, kingdom.Name?.Attributes is { Count: > 0 }))
            {
                // The player's own name stays, whatever title the ruler holds; there is nothing left to restore.
                if (_originals.Remove(id))
                    Debug.Print($"[TAOM.Realms] {id} keeps the name the player gave it.");
                _applied.Remove(id);
                _appliedNames.Remove(id);
                continue;
            }

            var title = renames == null || renames(kingdom) ? service.RealmNameTitle(id, kingdom.Leader?.StringId) : null;
            _applied.TryGetValue(id, out var current);
            if (title?.Id == current && (title != null || !_originals.ContainsKey(id)))
                continue;

            if (title == null)
            {
                var own = _originals[id];
                _appliedNames.TryGetValue(id, out var appliedName);
                _originals.Remove(id);
                _applied.Remove(id);
                _appliedNames.Remove(id);
                if (!StillNamedByRealms(kingdom.Name?.Value, appliedName))
                {
                    Debug.Print($"[TAOM.Realms] {id} lost its naming title but keeps the name another source gave it.");
                    continue;
                }
                if (!Restorable(own.Name))
                {
                    Debug.Print($"[TAOM.Realms] {id} lost its naming title; its saved own name has lost its text, so it keeps its current name.");
                    continue;
                }
                kingdom.ChangeKingdomName(new TextObject(own.Name), new TextObject(own.InformalName), new TextObject(own.FormalName));
                Debug.Print($"[TAOM.Realms] {id} takes its own name back.");
                renamed?.Invoke(id);
                continue;
            }

            if (!_originals.ContainsKey(id))
                _originals[id] = new KingdomNameRecord(OwnText(kingdom.Name, id), OwnText(kingdom.InformalName, id),
                    OwnText(kingdom.FormalName, id));
            var name = service.RealmNameFrom(title);
            kingdom.ChangeKingdomName(new TextObject(name), new TextObject(name),
                new TextObject(TitleNames.Of(title, config, service.Catalog)));
            _applied[id] = title.Id;
            _appliedNames[id] = name;
            Debug.Print($"[TAOM.Realms] {id} is named after {title.Id}.");
            renamed?.Invoke(id);
        }
    }
}
