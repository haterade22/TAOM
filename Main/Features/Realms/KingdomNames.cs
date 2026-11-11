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
/// shows on the next load.
/// </summary>
internal sealed class KingdomNames
{
    private Dictionary<string, KingdomNameRecord> _originals = new(StringComparer.Ordinal);

    /// <summary>The title each kingdom was named after this session; not saved.</summary>
    private readonly Dictionary<string, string> _applied = new(StringComparer.Ordinal);

    public string Save() => KingdomNameRecords.Write(_originals);

    public void Load(string? text)
    {
        _originals = KingdomNameRecords.Read(text);
        _applied.Clear();
    }

    /// <summary>
    /// Renames the kingdoms whose naming title changed, telling <paramref name="renamed"/> each kingdom's id. Cheap: a few
    /// title lookups per kingdom.
    /// </summary>
    public void Update(RealmsService service, Domain.Config.RealmsConfig config, Action<string>? renamed = null)
    {
        foreach (var kingdom in Kingdom.All)
        {
            var id = kingdom?.StringId;
            if (id == null || kingdom!.IsEliminated)
                continue;
            var title = service.RealmNameTitle(id, kingdom.Leader?.StringId);
            _applied.TryGetValue(id, out var current);
            if (title?.Id == current && (title != null || !_originals.ContainsKey(id)))
                continue;

            if (title == null)
            {
                var own = _originals[id];
                kingdom.ChangeKingdomName(new TextObject(own.Name), new TextObject(own.InformalName), new TextObject(own.FormalName));
                _originals.Remove(id);
                _applied.Remove(id);
                Debug.Print($"[TAOM.Realms] {id} takes its own name back.");
                renamed?.Invoke(id);
                continue;
            }

            if (!_originals.ContainsKey(id))
                _originals[id] = new KingdomNameRecord(kingdom.Name?.Value ?? id, kingdom.InformalName?.Value ?? id,
                    kingdom.FormalName?.Value ?? id);
            var name = service.RealmNameFrom(title);
            kingdom.ChangeKingdomName(new TextObject(name), new TextObject(name),
                new TextObject(TitleNames.Of(title, config, service.Catalog)));
            _applied[id] = title.Id;
            Debug.Print($"[TAOM.Realms] {id} is named after {title.Id}.");
            renamed?.Invoke(id);
        }
    }
}
