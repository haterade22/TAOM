using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Features.HeroAbilities.Domain;

/// <summary>
/// One mission's hero balance tally: a <see cref="HeroCombatSheet"/> for each hero body, by reference. A body that is not
/// listed is ignored, so the mission logic can hand in every hit. The sheets outlive the bodies for the end-of-mission
/// summary. No engine type. Main thread only.
/// </summary>
public sealed class HeroCombatLedger<TBody> where TBody : class
{
    private readonly Dictionary<TBody, HeroCombatSheet> _byBody = new Dictionary<TBody, HeroCombatSheet>();

    public IEnumerable<HeroCombatSheet> All => _byBody.Values;

    public int Count => _byBody.Count;

    public HeroCombatSheet? Of(TBody body) => _byBody.TryGetValue(body, out var sheet) ? sheet : null;

    public HeroCombatSheet Add(TBody body, string heroId, float now)
    {
        if (_byBody.TryGetValue(body, out var known))
            return known;
        var serial = 1 + _byBody.Values.Count(s => s.HeroId == heroId);
        var sheet = new HeroCombatSheet(heroId, serial, now);
        _byBody[body] = sheet;
        return sheet;
    }

    // An arrow still in flight when he falls can land after it, so a fallen sheet still counts what he deals, as Kill does.
    public void Dealt(TBody hero, int damage)
    {
        var sheet = Of(hero);
        if (sheet == null || damage <= 0)
            return;
        sheet.Dealt += damage;
        sheet.HitsLanded++;
    }

    public void Taken(TBody hero, int damage)
    {
        var sheet = Live(hero);
        if (sheet == null || damage <= 0)
            return;
        sheet.Taken += damage;
        sheet.HitsTaken++;
    }

    // A kill can come in the frame he falls, so a fallen sheet still counts it.
    public void Kill(TBody hero)
    {
        var sheet = Of(hero);
        if (sheet != null)
            sheet.Kills++;
    }

    /// <summary>The body left the battle, downed (killed or knocked out) or not (fled): the sheet the first time, null for
    /// an unlisted body or a second removal.</summary>
    public HeroCombatSheet? Fell(TBody hero, float now, string killer, string blow, bool downed)
    {
        var sheet = Live(hero);
        if (sheet == null)
            return null;
        sheet.FellAt = now;
        sheet.Downed = downed;
        sheet.Killer = killer;
        sheet.Blow = blow;
        return sheet;
    }

    /// <summary>One line for each hero across all his bodies, with the rates per minute alive.</summary>
    public IEnumerable<string> Summary(float now)
    {
        foreach (var hero in _byBody.Values.GroupBy(s => s.HeroId).OrderBy(g => g.Key, System.StringComparer.Ordinal))
        {
            var alive = hero.Sum(s => s.SecondsAlive(now));
            var kills = hero.Sum(s => s.Kills);
            var dealt = hero.Sum(s => s.Dealt);
            var taken = hero.Sum(s => s.Taken);
            var minutes = alive / 60f;
            yield return string.Format(CultureInfo.InvariantCulture,
                "hero={0} bodies={1} falls={2} alive_s={3:F0} kills={4} dealt={5} taken={6} kills_per_min={7:F1} dealt_per_min={8:F0} taken_per_min={9:F0}",
                hero.Key, hero.Count(), hero.Count(s => s.Downed), alive, kills, dealt, taken,
                PerMinute(kills, minutes), PerMinute(dealt, minutes), PerMinute(taken, minutes));
        }
    }

    /// <summary>Once a second: each standing body's health limit, and whether the player now controls it.</summary>
    public void Sample(System.Func<TBody, float> limitOf, TBody? played)
    {
        foreach (var pair in _byBody)
        {
            if (pair.Value.FellAt.HasValue)
                continue;
            pair.Value.NoteHealth(limitOf(pair.Key));
            if (ReferenceEquals(played, pair.Key))
                pair.Value.Played = true;
        }
    }

    /// <summary>At mission end: a line for each body still standing, then the summary lines.</summary>
    public IEnumerable<string> EndLines(float now, System.Func<TBody, float> healthOf)
    {
        foreach (var pair in _byBody)
            if (!pair.Value.FellAt.HasValue)
                yield return pair.Value.Line(now, healthOf(pair.Key));
        foreach (var line in Summary(now))
            yield return "summary " + line;
    }

    public void Clear() => _byBody.Clear();

    private HeroCombatSheet? Live(TBody body)
    {
        var sheet = Of(body);
        return sheet == null || sheet.FellAt.HasValue ? null : sheet;
    }

    private static float PerMinute(int count, float minutes) => minutes > 0f ? count / minutes : 0f;
}
