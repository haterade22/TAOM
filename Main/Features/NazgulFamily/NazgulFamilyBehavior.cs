using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.NazgulFamily;

/// <summary>
/// Severs any family links a Ringwraith carries on session launch — the runtime companion to the
/// data strip in <c>characters/heroes.xslt</c> (which removes the vanilla spouse/father/mother seed
/// from all nine wraith Hero records).
/// </summary>
/// <remarks>
/// Family for these lords is Hero-level (vanilla <c>heroes.xml</c> wires the nine wraiths into a
/// self-contained graph: Witch-King↔lord_1_16 with children 155/28/38; Khamûl↔lord_1_48_1 with
/// children 48_2/48_3). TAOM's <c>heroes.xslt</c> now strips spouse/father/mother for all nine, so a
/// NEW campaign seeds them family-free and this behaviour is a no-op there; <see cref="Models.TaomMarriageModel"/>
/// then blocks any future runtime marriage. This behaviour exists for saves created BEFORE the strip,
/// whose serialized Hero state still carries the family — it severs those links on load.
/// <para>
/// Entity-state matrix (this mutates Hero state on load): the whole graph is internal to the nine
/// registered wraith ids (Khamûl, lord_1_48, is registered), so no NON-wraith is widowed or left with
/// a dangling child link. Clearing is idempotent and safe in every wraith state — alive lord, dead/
/// disabled, prisoner — because severing a reference never corrupts the hero (a family-free wraith is
/// the intended end state). The <c>Spouse</c> setter clears the partner's side; <c>Father</c>/<c>Mother</c>
/// setters are asymmetric on null (they do NOT remove the child from the parent's list), so we remove
/// the wraith from the ex-parent's <c>Children</c> explicitly; and we sever the <c>ExSpouses</c> residual
/// the <c>Spouse</c> setter leaves (no live engine path ever removes an entry) so the wraith
/// shows no ex-spouse in the encyclopedia.
/// </para>
/// </remarks>
public sealed class NazgulFamilyBehavior : CampaignBehaviorBase
{
    private readonly INazgulRegistry _registry;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public NazgulFamilyBehavior(INazgulRegistry registry, ICoopSessionProvider coop, IModLogger logger)
    {
        _registry = registry;
        _coop = coop;
        _logger = logger;
    }

    public override void RegisterEvents()
        => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Family links are shared hero state: the server severs them (#1086).
        if (!_coop.IsAuthority)
            return;
        int cleared = 0;
        foreach (var hero in Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes))
        {
            if (hero == null || !_registry.IsWraith(hero.StringId)) continue;
            if (ClearFamily(hero)) cleared++;
        }

        if (cleared > 0)
            _logger.LogInfo($"NazgulFamily: severed family links on {cleared} Ringwraith(s) from a pre-strip save.");
    }

    private bool ClearFamily(Hero wraith)
    {
        bool changed = false;

        if (wraith.Spouse != null) { wraith.Spouse = null; changed = true; } // setter clears both sides
        if (ClearExSpouses(wraith)) changed = true;                          // sever the residual the setter leaves

        var father = wraith.Father;
        if (father != null) { father.Children.Remove(wraith); wraith.Father = null; changed = true; }
        var mother = wraith.Mother;
        if (mother != null) { mother.Children.Remove(wraith); wraith.Mother = null; changed = true; }

        foreach (var child in wraith.Children.ToList())
        {
            if (child.Father == wraith) { child.Father = null; changed = true; }
            if (child.Mother == wraith) { child.Mother = null; changed = true; }
        }
        if (wraith.Children.Count > 0) { wraith.Children.Clear(); changed = true; }

        return changed;
    }

    // Remove the wraith from every ex-spouse's list and clear its own. The Spouse setter appends to this list
    // (v1.5.6 Hero.cs:906) and no live engine path removes an entry. ExSpouses returns Hero's own list, typed
    // MBReadOnlyList<Hero>, which is a List<Hero> in v1.5.6, so the mutation needs no reflection; if the engine
    // ever makes it truly read-only, this stops compiling instead of degrading silently. Null on a hero a co-op
    // client replicated without its constructor (coop-compat-gap-ledger R6): nothing to clear.
    private bool ClearExSpouses(Hero wraith)
    {
        try
        {
            var mine = wraith.ExSpouses;
            if (mine == null || mine.Count == 0) return false;
            foreach (var ex in mine.ToList())
                ex?.ExSpouses?.Remove(wraith);
            mine.Clear();
            return true;
        }
        catch (System.Exception e)
        {
            _logger.LogWarning($"NazgulFamily: could not clear ExSpouses for {wraith.StringId}: {e.Message}");
            return false;
        }
    }
}
