using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Applies and unwinds the commander's wars for an enlisted player (field report 5). Decision lives
/// in <see cref="ServiceWarPolicy"/>; this owns the two moments and the bookkeeping between them.
/// </summary>
public interface IServiceDiplomacyService
{
    /// <summary>Mirror the commander's wars at oath, recording what was ours to unwind later.</summary>
    void ApplyServiceWars(string commanderHeroId);

    /// <summary>Make peace with exactly the factions the mirror created, and forget the mirror.</summary>
    void UnwindServiceWars();
}

/// <inheritdoc />
public sealed class ServiceDiplomacyService : IServiceDiplomacyService
{
    private readonly IEnlistmentStore _store;
    private readonly IServiceDiplomacyAdapter _diplomacy;
    private readonly IModLogger _logger;

    public ServiceDiplomacyService(
        IEnlistmentStore store,
        IServiceDiplomacyAdapter diplomacy,
        IModLogger logger)
    {
        _store = store;
        _diplomacy = diplomacy;
        _logger = logger;
    }

    public void ApplyServiceWars(string commanderHeroId)
    {
        var record = _store.Record;

        // SNAPSHOT FIRST, and this ordering is the whole safeguard. The pre-oath enemy set has to be
        // captured BEFORE a single declaration lands, or the wars we are about to create would look
        // like wars the player brought with him and would never be unwound. Recording it once, at
        // oath, is also what stops the discharge becoming ServeAsSoldier's universal peace button.
        var enemiesAtOath = new List<string>(_diplomacy.GetPlayerEnemies());
        record.EnemiesAtOath = enemiesAtOath;

        // PIN THE IDENTITY THAT IS ABOUT TO DECLARE. Hero.MapFaction is Clan.Kingdom ?? Clan, so it
        // is not stable for the length of a term: a player independent at oath declares as his clan,
        // and if that clan joins a kingdom before discharge the unwind would resolve MapFaction live
        // and make peace on behalf of the KINGDOM — ending a war for every vassal in it as a side
        // effect of one soldier's discharge. UnwindServiceWars compares against this before touching
        // anything.
        record.OathFactionId = _diplomacy.GetPlayerFactionId();

        var toDeclare = ServiceWarPolicy.FactionsToDeclareWarOn(
            _diplomacy.GetEnemiesOf(commanderHeroId), enemiesAtOath);

        var declared = new List<string>();
        foreach (var factionId in toDeclare)
        {
            if (_diplomacy.DeclareWarOn(factionId))
                declared.Add(factionId);
        }

        // Only what ACTUALLY landed goes in the mirror. A declaration the engine refused (eliminated
        // faction, already at war through another path) must not leave a promise to make peace with
        // something we never fought — that peace would be a real, unearned diplomatic gift.
        record.MirroredWars = declared;

        _logger?.LogInfo(
            $"[Enlistment] service wars applied — mirrored {declared.Count} of {toDeclare.Count} " +
            $"(the player already held {enemiesAtOath.Count} of his own)");
    }

    public void UnwindServiceWars()
    {
        var record = _store.Record;
        var toPeace = ServiceWarPolicy.FactionsToPeaceOnDischarge(
            record.MirroredWars, record.EnemiesAtOath);

        var ended = 0;
        if (ShouldUnwindUnderCurrentIdentity(record.OathFactionId, toPeace.Count))
        {
            foreach (var factionId in toPeace)
            {
                if (_diplomacy.MakePeaceWith(factionId))
                    ended++;
            }
        }

        // Cleared whatever happened above. A mirror that survives discharge would be re-unwound at
        // the NEXT discharge, handing the player peace with factions he had since chosen to fight.
        record.MirroredWars = new List<string>();
        record.EnemiesAtOath = new List<string>();

        _logger?.LogInfo($"[Enlistment] service wars unwound — {ended} of {toPeace.Count} ended");
    }

    /// <summary>
    /// Refuse to make peace on behalf of a faction that did not declare the war.
    ///
    /// The failure this prevents is asymmetric and invisible. <c>Hero.MapFaction</c> resolves LIVE
    /// as <c>Clan.Kingdom ?? Clan</c>, so a player who swore while independent declared as his own
    /// clan; if his clan then joined a kingdom, the discharge would call
    /// <c>MakePeaceAction.Apply</c> on that KINGDOM and end the war for every vassal in it — an
    /// enormous diplomatic act triggered by one soldier leaving service, with nothing on screen to
    /// connect the two. The mirror is cleared either way: it names wars declared by an identity we
    /// are no longer speaking for, so it is not ours to unwind and not ours to keep.
    ///
    /// An empty pin is a save from before the field existed. Refusing there would strand a
    /// mid-service player in wars he can never undo, so it proceeds as before and says so.
    /// </summary>
    private bool ShouldUnwindUnderCurrentIdentity(string oathFactionId, int pending)
    {
        if (pending == 0)
            return true;

        if (string.IsNullOrEmpty(oathFactionId))
        {
            _logger?.LogInfo(
                "[Enlistment] no oath-faction pin on this record (saved before the pin existed) — "
                + "unwinding the service wars under the player's current faction, as previous builds did");
            return true;
        }

        var current = _diplomacy.GetPlayerFactionId();
        if (string.Equals(current, oathFactionId, System.StringComparison.Ordinal))
            return true;

        _logger?.LogWarning(
            $"[Enlistment] NOT unwinding {pending} service war(s): they were declared as "
            + $"'{oathFactionId}' and the player now speaks for '{current ?? "nothing"}'. Making peace "
            + "under the new identity would move a whole faction's diplomacy on the strength of one "
            + "soldier's discharge. The mirror is cleared without acting.");
        return false;
    }
}
