using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The shop's heroes on the field (#1325): each hero the run bought is spawned at the plan's home place, the run hears whether the field
/// took him (a refused spawn is paid back there), and a held hero who leaves the field is for sale again.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private const int HuntersRepairsLogged = 5;

    // #1370: how long a hero waits for the castle tactic's first lane pick before he spawns anyway. The pick came 0.1 s after the run
    // started in run 20261116-125931; a scene where no lane is active and no lane has a gate never makes one.
    private const float HeroLaneWaitSeconds = 5f;

    private readonly List<HeroSpawn> _heroesWaiting = new();
    private float _heroWaitSeconds;

    private int _heroSpawns;
    private int _heroFalls;
    private int _heroesLanded;
    private int _huntersRepairs;
    private LdHuntersTotals _huntersTotals = LdHuntersTotals.None;

    // The held heroes on the field: added at a spawn the field took, removed when he leaves it.
    private readonly HashSet<string> _heroesStanding = new(StringComparer.Ordinal);

    /// <summary>
    /// Where a body that joins no batch appears: the recorded foot muster, else the keep. The mounted lord's home point and a hero's place.
    /// With no place the engine would lay the body out from a deployment plan nobody built for its formation, so the caller refuses.
    /// </summary>
    private LdPlace? PlanHome => _plan.Muster?.Foot is { } foot ? LdPlace.From(foot) : _keepAt is { } keep ? LdPlace.At(keep) : null;

    // The heroes the run asks for wait until a lane holds a defender formation (#1370, ILongDefeatBattlefieldAdapter.DefenderLanesAssigned):
    // before that pick the castle tactic merges its foot formations into the heroes' formation 7, which is off AI control, and the
    // director's repairs then move soldiers out again all run. A shop hero is bought one frame after the pick at the earliest, so he
    // spawns at once. A later lane pick can still take formation 7 (TacticDefendCastle.cs:701-735); the hunters line's moved_out counts it.
    private void QueueHeroes(IReadOnlyList<HeroSpawn> heroes)
    {
        _heroesWaiting.AddRange(heroes);
        SpawnWaitingHeroes();
    }

    private void TickWaitingHeroes(float dt)
    {
        if (_heroesWaiting.Count == 0)
            return;

        _heroWaitSeconds += dt;
        SpawnWaitingHeroes();
    }

    private void SpawnWaitingHeroes()
    {
        if (_heroesWaiting.Count == 0)
            return;

        if (!_field.DefenderLanesAssigned())
        {
            if (_heroWaitSeconds < HeroLaneWaitSeconds)
                return;

            _logger.LogWarning($"{Tag}[Hero] no wall lane holds a defender formation after {HeroLaneWaitSeconds:0} s; the heroes spawn now");
        }

        var heroes = _heroesWaiting.ToArray();
        _heroesWaiting.Clear();
        _heroWaitSeconds = 0f;
        SpawnHeroes(heroes);
    }

    private void SpawnHeroes(IReadOnlyList<HeroSpawn> heroes)
    {
        foreach (var hero in heroes)
        {
            var report = PlanHome is { } home ? _field.SpawnHero(hero, home) : new LdHeroReport(false, "no home place");
            _run.HeroSpawned(hero.Id, report.Spawned);
            LongDefeatChecks.Record(_outcomes, LongDefeatHeroChecks.SpawnId, _mission + "h" + ++_heroSpawns, LongDefeatHeroChecks.Spawned(hero, report));
            if (report.Spawned)
            {
                _heroesLanded++;
                _heroesStanding.Add(hero.Id);
                _logger.LogInfo($"{Tag}[Hero] {hero.Id} ({hero.TroopId}) joins the defenders: formation {report.FormationIndex}, health x{report.HealthFactor:0.00}, holds {report.Wielded}");
            }
            else
                _logger.LogError($"{Tag}[Hero] {hero.Id} ({hero.TroopId}) could not be spawned ({report.Problem}); {HeroRefusedTail(InShop(hero.Id))}");
        }
    }

    private bool InShop(string heroId) => _plan.Shop.Heroes.Any(offer => offer.Id == heroId);

    internal static string HeroRefusedTail(bool sold) => sold ? "a price he cost is paid back" : "he was free, so nothing is paid back";

    internal static string HeroLeftText(string heroId, bool loss, bool sold) =>
        $"{heroId} {(loss ? "fell" : "fled")}; {(sold ? "a hero of the shop is for sale again" : "a free hero does not come back")}";

    // What the standing heroes did in the wave just cleared (#1337). A mission in which the field took no hero writes no line and reads
    // nothing; a refused spawn does not count.
    private void JudgeHunters(int wave)
    {
        if (_heroesLanded == 0)
            return;

        TickHunters();
        LongDefeatChecks.Record(_outcomes, LongDefeatHeroChecks.HuntersId, _mission + "hw" + wave, LongDefeatHeroChecks.Hunters(wave, _field.ReadHeroes(), _huntersTotals));
    }

    // Each tick while a hero stands (#1337). The field keeps the heroes' group whole and gives it the wall order again after a hand-back
    // (ILongDefeatBattlefieldAdapter.HoldHunters names the hand-backs). Every regain is logged; a membership repair only the first few times.
    private void TickHunters()
    {
        if (_heroesStanding.Count == 0)
            return;

        var hold = _field.HoldHunters();
        if (!hold.Acted)
            return;

        _huntersTotals = _huntersTotals.Add(hold);
        if (hold.Regained)
            _logger.LogInfo($"{Tag}[Hero] the heroes' group was taken back from the AI ({_huntersTotals.Retaken} so far)");
        if ((hold.MovedOut > 0 || hold.MovedBack > 0) && _huntersRepairs++ < HuntersRepairsLogged)
            _logger.LogInfo($"{Tag}[Hero] the heroes' group repaired: {hold.MovedOut} soldier(s) moved out, {hold.MovedBack} hero(es) moved back");
    }

    // Main thread: called from the deferred removal.
    private void HeroLeft(string heroId, bool loss)
    {
        _heroesStanding.Remove(heroId);
        _run.HeroDown(heroId, loss);
        LongDefeatChecks.Record(_outcomes, LongDefeatHeroChecks.FallId, _mission + heroId + "-f" + ++_heroFalls, LongDefeatHeroChecks.Fell(heroId, loss));
        _logger.LogInfo($"{Tag}[Hero] {HeroLeftText(heroId, loss, InShop(heroId))}");
    }
}
