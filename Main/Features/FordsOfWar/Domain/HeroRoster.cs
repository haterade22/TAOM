using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The heroes each side has fielded. At most MaxAlivePerSide may be alive at once; a dead hero keeps its entry and comes
/// back when its timer ends (or the owner buys it back), as soon as the side is under the limit. Respawn delay and
/// buyback price grow with the match minute. Gold is not handled here: the match charges the owner.
/// </summary>
internal sealed class HeroRoster
{
    private readonly FordsHeroConfig _config;
    private readonly List<HeroEntry> _entries = new();

    public HeroRoster(FordsHeroConfig config)
    {
        _config = config;
    }

    /// <summary>Every hero ever fielded, both sides, in the order they were fielded.</summary>
    public IReadOnlyList<HeroEntry> Heroes => _entries;

    public HeroEntry? Get(MatchSide side, string heroId) => _entries.FirstOrDefault(e => e.Side == side && e.HeroId == heroId);

    /// <summary>Heroes holding an alive slot: the alive ones, plus the dead ones whose buyback is paid and not yet spawned.</summary>
    public int AliveCount(MatchSide side) => _entries.Count(e => e.Side == side && e.HoldsSlot);

    /// <summary>Registers a hero as Alive. The caller spawns it with <see cref="HeroEntry.ToSpawnRequest"/>.</summary>
    public CommandResult TryField(MatchSide side, string heroId, int ownerSlot, int lane, int price)
    {
        if (Get(side, heroId) != null)
            return CommandResult.HeroAlreadyFielded;
        if (AliveCount(side) >= _config.MaxAlivePerSide)
            return CommandResult.HeroLimit;

        _entries.Add(new HeroEntry(side, heroId, ownerSlot, lane, price, HeroState.Alive, 0f));
        return CommandResult.Ok;
    }

    public float RespawnDelay(float minute) =>
        _config.RespawnBaseSeconds + _config.RespawnPerMinuteSeconds * Math.Max(0f, minute);

    public int BuybackPrice(int heroPrice, float minute)
    {
        var fraction = (double)_config.BuybackBaseFraction + (double)_config.BuybackPerMinuteFraction * Math.Max(0f, minute);
        return (int)Math.Round(Math.Max(0, heroPrice) * fraction, MidpointRounding.AwayFromZero);
    }

    /// <summary>The buyback price of a fielded hero; 0 when the hero is unknown.</summary>
    public int BuybackPrice(MatchSide side, string heroId, float minute)
    {
        var hero = Get(side, heroId);
        return hero == null ? 0 : BuybackPrice(hero.Price, minute);
    }

    /// <summary>Drops a hero's entry, so it holds no alive slot and the id can be fielded again. False for an unknown hero.</summary>
    public bool Remove(MatchSide side, string heroId)
    {
        var index = IndexOf(side, heroId);
        if (index < 0)
            return false;

        _entries.RemoveAt(index);
        return true;
    }

    /// <summary>Marks an alive hero dead and sets its respawn time. False for an unknown or already dead hero.</summary>
    public bool OnDied(MatchSide side, string heroId, float now, float minute)
    {
        var index = IndexOf(side, heroId);
        if (index < 0 || _entries[index].State == HeroState.Dead)
            return false;

        var delay = RespawnDelay(minute);
        _entries[index] = _entries[index] with { State = HeroState.Dead, RespawnAt = now + delay, RespawnTotal = delay };
        return true;
    }

    /// <summary>
    /// Makes a dead hero's respawn due now and reserves its alive slot until the spawn. Refused (HeroNotDead) for a hero
    /// that is not dead, already bought back, or whose respawn timer has ended. The caller charges the price.
    /// </summary>
    public CommandResult TryBuyback(MatchSide side, string heroId, float now)
    {
        var index = IndexOf(side, heroId);
        if (index < 0 || !_entries[index].CanBuyback(now))
            return CommandResult.HeroNotDead;

        _entries[index] = _entries[index] with { RespawnAt = now, BuybackPending = true };
        return CommandResult.Ok;
    }

    /// <summary>
    /// Revives dead heroes whose timer has ended, earliest first, while their side is under the limit, and returns one
    /// spawn request each. <paramref name="nextRequestId"/> hands out the request ids.
    /// </summary>
    public IReadOnlyList<SpawnRequest> DueRespawns(float now, Func<int> nextRequestId)
    {
        var requests = new List<SpawnRequest>();
        var due = _entries
            .Select((e, i) => (e, i))
            .Where(x => x.e.State == HeroState.Dead && x.e.RespawnAt <= now)
            .OrderBy(x => x.e.RespawnAt)
            .ThenBy(x => x.i)
            .Select(x => x.i)
            .ToList();

        foreach (var index in due)
        {
            var hero = _entries[index];
            // A paid buyback already holds its slot; only a natural respawn needs a free one.
            if (!hero.BuybackPending && AliveCount(hero.Side) >= _config.MaxAlivePerSide)
                continue;

            _entries[index] = hero with { State = HeroState.Alive, BuybackPending = false };
            requests.Add(_entries[index].ToSpawnRequest(nextRequestId()));
        }

        return requests;
    }

    private int IndexOf(MatchSide side, string heroId) => _entries.FindIndex(e => e.Side == side && e.HeroId == heroId);
}
