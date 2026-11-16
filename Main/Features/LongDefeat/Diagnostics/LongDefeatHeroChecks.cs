using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The verdicts of the shop's heroes (#1325). <see cref="SpawnId"/>, key <c>&lt;mission&gt;h&lt;N&gt;</c> for the mission's Nth hero spawn, passes
/// when the body the engine made is a hero to it (or, in a campaign, a character with no Hero behind it: <c>campaign=1</c>, <c>is_hero=0</c>), has no mount, stands in the heroes' formation, has its health limit scaled by the
/// shop's factor, that formation is off AI control after the spawn (<c>held=1</c>) and it holds no soldier who is not a held hero
/// (<c>strangers=0</c>); <c>order=</c> names the formation's movement order and is reported, never judged. A spawn the field refused fails with
/// <c>problem=</c>. <see cref="FallId"/>, key <c>&lt;mission&gt;&lt;hero&gt;-f&lt;N&gt;</c>, is written when a held hero leaves the field, with
/// <c>cause=killed</c> or <c>cause=fled</c>. Every word of a detail is <c>name=value</c> (the sanitiser rule in <see cref="LongDefeatPostsChecks"/>).
/// <see cref="HuntersId"/>, key <c>&lt;mission&gt;hw&lt;wave&gt;</c>, is written at each wave clear of a mission that spawned a hero: the heroes'
/// formation (the units it holds, whether it is off AI control, its movement order and whether that is still the field's own order at the
/// wall, <c>own=</c>), how many standing heroes are within <see cref="HeroGroup.NearMetres"/> of that place (<c>near=</c>), what each standing
/// hero did (kills, health, the weapon class in his hand: <c>weapons=</c>) and the director's running totals of the holds (<c>retaken=</c>, <c>moved_out=</c>, <c>moved_back=</c>). It passes
/// when every standing hero is in the heroes' formation, the formation holds no one else, it is off AI control, and, once it has held the
/// field's own order for <see cref="HeroGroup.SettleSeconds"/>, every standing hero is near the place on the ground and in height. A
/// player's own order is never judged, and neither are heroes on their way after a new order. A hero the player controls (a take-over in a
/// campaign, #1366) counts as near: he stands where the player wants him. <c>kills</c> is the engine's KillCount,
/// which also counts knock-outs; it, <c>weapons=</c> and the totals are reported, never judged. With no hero standing it is skipped.
/// </summary>
internal static class LongDefeatHeroChecks
{
    internal const string SpawnId = "taom.long-defeat.hero";
    internal const string FallId = "taom.long-defeat.hero-fall";

    internal static CheckVerdict Spawned(HeroSpawn hero, LdHeroReport report)
    {
        var detail = $"hero={hero.Id} troop={hero.TroopId}";
        if (!report.Spawned)
            return LongDefeatChecks.Verdict(false, detail + " problem=" + Words(report.Problem ?? "unknown"));

        detail += $" is_hero={(report.IsHero ? 1 : 0)} campaign={(report.CampaignCharacter ? 1 : 0)} mounted={(report.Mounted ? 1 : 0)} formation={report.FormationIndex} held={(report.Held ? 1 : 0)}"
            + $" strangers={report.Strangers} order={(report.Order.Length > 0 ? Words(report.Order) : "none")}"
            + $" health_factor={report.HealthFactor.ToString("0.00", CultureInfo.InvariantCulture)} wanted_factor={hero.HealthFactor.ToString("0.00", CultureInfo.InvariantCulture)}"
            + $" wielded={(report.Wielded.Length > 0 ? Words(report.Wielded) : "none")}";
        var scaled = Math.Abs(report.HealthFactor - hero.HealthFactor) <= 0.01f;
        return LongDefeatChecks.Verdict(report.IsHero != report.CampaignCharacter && !report.Mounted && report.FormationIndex == HeroGroup.FormationIndex && report.Held && report.Strangers == 0 && scaled, detail);
    }

    internal const string HuntersId = "taom.long-defeat.hunters";

    internal static CheckVerdict Hunters(int wave, LdHeroesRead read, LdHuntersTotals totals)
    {
        var facts = read.Heroes;
        if (facts.Count == 0)
            return new CheckVerdict(CheckResult.Skip, $"w={wave} reason=none-standing");

        var inGroup = facts.Count(f => f.FormationIndex == HeroGroup.FormationIndex);
        var near = facts.Count(f => f.PlayerControlled || (f.Distance <= HeroGroup.NearMetres && f.Height <= HeroGroup.NearHeightMetres));
        var settled = read.SettledSeconds >= 0f ? (int)Math.Min(read.SettledSeconds, 99999f) : 0;
        var detail = $"w={wave} standing={facts.Count} in_group={inGroup} units={read.GroupUnits} held={(read.GroupHeld ? 1 : 0)} order={Words(read.GroupOrder)}"
            + $" own={(read.OwnOrder ? 1 : 0)} settled={settled} near={near} kills={facts.Sum(f => f.Kills)}"
            + " by=" + string.Join(",", facts.Select(f => f.Id + ":" + f.Kills))
            + " health=" + string.Join(",", facts.Select(f => f.Id + ":" + f.HealthShare.ToString("0.00", CultureInfo.InvariantCulture)))
            + " weapons=" + string.Join(",", facts.Select(f => f.Id + ":" + Words(f.Weapon)))
            + $" retaken={totals.Retaken} moved_out={totals.MovedOut} moved_back={totals.MovedBack}";
        var placed = !read.OwnOrder || !(read.SettledSeconds >= HeroGroup.SettleSeconds) || near == facts.Count;
        return LongDefeatChecks.Verdict(inGroup == facts.Count && read.GroupUnits == inGroup && read.GroupHeld && placed, detail);
    }

    internal static CheckVerdict Fell(string heroId, bool loss) => new(CheckResult.Pass, $"hero={heroId} cause={(loss ? "killed" : "fled")}");

    private static string Words(string text) => string.Join("_", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
