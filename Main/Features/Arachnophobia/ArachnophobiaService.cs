using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Spider;

namespace TAOM.Features.Arachnophobia;

/// <summary>
/// Arachnophobia Mode (#1110, docs/features/arachnophobia-mode.md). Troop records cannot be deleted, because saves
/// refer to them by id, so the mode makes the spider troops unreachable. Once per game it finds every non-hero troop on
/// Monster.spider, remembers the ids, puts those troops on wargs and closes the routes that would show or create one.
/// Nothing here goes into a save: each game reloads this data from XML, so the mode off at the next load restores it.
/// </summary>
public sealed class ArachnophobiaService : IArachnophobiaService
{
    internal const string ApplyCheckId = "taom.arachnophobia.apply";

    private readonly ITroopMountCatalogAdapter _catalog;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private HashSet<string> _hidden = new(StringComparer.Ordinal);

    public ArachnophobiaService(ITroopMountCatalogAdapter catalog, IOutcomeLog outcomes, IModLogger logger)
    {
        _catalog = catalog;
        _outcomes = outcomes;
        _logger = logger;
    }

    public bool IsHidden(string? troopId) => troopId != null && _hidden.Contains(troopId);

    public void ApplyForGame(bool enabled, bool isCampaign)
    {
        _hidden = new HashSet<string>(StringComparer.Ordinal);
        if (!enabled)
            return;

        string scope = isCampaign ? "campaign" : "custom";
        try
        {
            List<string> missing = ArachnophobiaRules.RequiredItemIds.Where(id => !_catalog.ItemExists(id)).ToList();
            if (missing.Count > 0)
            {
                Fail(scope, "missing=" + string.Join(",", missing),
                    $"Spiders stay in this game: the Armory has no {string.Join(", ", missing)}.");
                return;
            }

            // A hero's battle set is its saved gear, so a write would go into the save (CharacterObject.cs:113-123).
            List<TroopMountRecord> riders = _catalog.ReadRiders(SpiderConfig.SpiderMonsterId).Where(r => !r.IsHero).ToList();
            if (riders.Count == 0)
            {
                Fail(scope, "riders=0", "No troop rides Monster.spider, so the mode has nothing to hide. "
                                        + "Each game must reload its troops from XML; this game did not, or the spider items moved.");
                return;
            }

            // Before the swap: the swap removes the Monster this set keys on. After a failure past this line, the routes
            // that read IsHidden (volunteers, garrison roots, the Emissary) stay closed; the edges and list flags may not.
            // The swap comes first because a missing spider matters more than a route that leads to a warg rider.
            _hidden = new HashSet<string>(riders.Select(r => r.TroopId), StringComparer.Ordinal);

            int sets = 0;
            foreach (TroopMountRecord rider in riders)
            {
                foreach (string spiderItem in rider.MountItemIds)
                    sets += _catalog.ReplaceMount(rider.TroopId, spiderItem, ArachnophobiaRules.WargFor(spiderItem), ArachnophobiaRules.HarnessItemId);
            }

            var failed = new List<string>();
            int edges = isCampaign ? CloseCampaignRoutes(riders, failed) : CloseCustomBattleRoute(riders, failed);

            string detail = $"hidden={_hidden.Count} sets={sets} edges={edges}";
            if (failed.Count > 0)
            {
                Fail(scope, $"{detail} failed={string.Join(",", failed)}",
                    $"A private engine setter is missing or a troop is gone; these troops stay reachable: {string.Join(", ", failed)}.");
                return;
            }

            _logger.LogInfo($"[Arachnophobia] Applied ({scope}): {detail}.");
            _outcomes.Pass(ApplyCheckId, scope, detail);
        }
        catch (Exception ex)
        {
            Fail(scope, "exception=" + ex.GetType().Name, $"Applying the mode stopped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private int CloseCampaignRoutes(List<TroopMountRecord> riders, List<string> failed)
    {
        int edges = DropUpgradeEdgesIntoHiddenTroops(failed);
        foreach (TroopMountRecord rider in riders)
            _catalog.HideFromEncyclopedia(rider.TroopId);
        return edges;
    }

    private int CloseCustomBattleRoute(List<TroopMountRecord> riders, List<string> failed)
    {
        foreach (TroopMountRecord rider in riders)
        {
            if (!_catalog.MarkObsolete(rider.TroopId))
                failed.Add(rider.TroopId);
        }
        return 0;
    }

    // Every edge into the hidden set goes, from a hidden troop too, so a leftover in an old save cannot upgrade into
    // another spider troop. Only hidden troops can end with no target: the brood husks, the only culture basic troops in
    // the set, have no edges in XML, so DefaultAlleyModel's UpgradeTargets[0] never meets an array this emptied.
    private int DropUpgradeEdgesIntoHiddenTroops(List<string> failed)
    {
        int dropped = 0;
        foreach (TroopUpgradeRecord troop in _catalog.ReadUpgrades())
        {
            if (!troop.TargetIds.Any(_hidden.Contains))
                continue;

            List<string> kept = troop.TargetIds.Where(id => !_hidden.Contains(id)).ToList();
            if (_catalog.SetUpgradeTargets(troop.TroopId, kept))
                dropped += troop.TargetIds.Count - kept.Count;
            else
                failed.Add(troop.TroopId);
        }
        return dropped;
    }

    private void Fail(string scope, string detail, string message)
    {
        _logger.LogError($"[Arachnophobia] {message}");
        _outcomes.Fail(ApplyCheckId, scope, detail);
    }
}
