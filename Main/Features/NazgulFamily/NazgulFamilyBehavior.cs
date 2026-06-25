using System.Linq;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.NazgulFamily;

/// <summary>
/// Defensive cleanup for saves created before the wraiths were made unmarriageable: on session
/// launch, severs any spouse / parent / child links a Ringwraith picked up via runtime marriage.
/// New campaigns never form these links (<see cref="Models.TaomMarriageModel"/> blocks the marriage),
/// so this is a no-op there.
/// </summary>
/// <remarks>
/// Entity-state matrix (this mutates Hero state on load): clearing family is idempotent and safe in
/// every wraith state — alive lord, dead/disabled, or prisoner — because severing a reference never
/// corrupts the hero (a wraith with no family is the intended end state). No state is skipped.
/// The <c>Spouse</c> setter is two-way (it also clears the partner's side); <c>Father</c>/<c>Mother</c>
/// nulling leaves the ex-parent's roster untouched, which is irrelevant to the wraith being family-free.
/// </remarks>
public sealed class NazgulFamilyBehavior : CampaignBehaviorBase
{
    private readonly INazgulRegistry _registry;
    private readonly IModLogger _logger;

    public NazgulFamilyBehavior(INazgulRegistry registry, IModLogger logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public override void RegisterEvents()
        => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        int cleared = 0;
        foreach (var hero in Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes))
        {
            if (hero == null || !_registry.IsWraith(hero.StringId)) continue;
            if (ClearFamily(hero)) cleared++;
        }

        if (cleared > 0)
            _logger.LogInfo($"NazgulFamily: severed family links on {cleared} Ringwraith(s) from a pre-feature save.");
    }

    private static bool ClearFamily(Hero wraith)
    {
        bool changed = false;

        if (wraith.Spouse != null) { wraith.Spouse = null; changed = true; } // setter clears both sides
        if (wraith.Father != null) { wraith.Father = null; changed = true; }
        if (wraith.Mother != null) { wraith.Mother = null; changed = true; }

        foreach (var child in wraith.Children.ToList())
        {
            if (child.Father == wraith) { child.Father = null; changed = true; }
            if (child.Mother == wraith) { child.Mother = null; changed = true; }
        }
        if (wraith.Children.Count > 0) { wraith.Children.Clear(); changed = true; }

        return changed;
    }
}
