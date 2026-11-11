using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// A coronation (&lt;Coronations&gt;): a claimant restoring a realm title founds his own ruling house. The game layer makes
/// the clan and moves the fiefs; this part says which coronation applies and gives the king his seat's titles.
/// </summary>
public sealed partial class RealmsService
{
    /// <summary>The coronation that follows when the hero restores the title, if any.</summary>
    public CoronationConfig? CoronationFor(string titleId, string heroId) =>
        Config.Coronations.FirstOrDefault(c => c.TitleId == titleId && c.HeroId == heroId);

    /// <summary>
    /// The titles the king takes with his seat: each fief's own title and the lordships of its villages, then the
    /// coronation's further titles. Each one held by the former ruling house (<paramref name="ofFormerHouse"/>) or by
    /// nobody passes to the king; one held by anyone else stays with them.
    /// </summary>
    public IReadOnlyList<TitleEvent> SeatKing(CoronationConfig coronation, string kingId, Func<string?, bool> ofFormerHouse)
    {
        var events = new List<TitleEvent>();
        var titles = new List<TitleDefinition>();
        foreach (var fief in coronation.FiefIds)
        {
            var title = Catalog.ForSettlement(fief);
            if (title == null)
                continue;
            titles.Add(title);
            titles.AddRange(Catalog.ChildrenOf(title.Id).Where(c => c.Rank == TitleRank.Lordship));
        }

        titles.AddRange(coronation.TitleIds.Select(id => Catalog.Get(id)).Where(t => t != null).Select(t => t!));

        foreach (var title in titles.Distinct())
        {
            var holder = Ledger.HolderOf(title.Id);
            if (holder == kingId || (holder != null && !ofFormerHouse(holder)))
                continue;
            Ledger.RemovePending(title.Id);
            Ledger.SetHolder(title.Id, kingId);
            events.Add(Changed(TitleEventReason.Transfer, title.Id, kingId, holder));
        }

        return events;
    }
}
