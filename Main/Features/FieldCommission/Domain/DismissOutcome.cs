namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// Verdict on dismissing a promoted companion back to the ranks (#540). Every value except
/// <see cref="Ok"/> names the state that refused it, so an entry point can log WHY rather than
/// only that it was refused. Only companions in the main party qualify; the rest of the entity
/// state matrix lives in <c>docs/features/field-commission.md</c>.
/// </summary>
public enum DismissOutcome
{
    Ok,

    /// <summary>The id is not in the promoted-hero list: an ordinary companion, whose path is
    /// vanilla's own fire line.</summary>
    NotPromoted,

    /// <summary>No living hero carries the id (dead or disabled; the load prune drops it).</summary>
    HeroGone,

    /// <summary>Alive, but no longer the player clan's companion.</summary>
    NotACompanion,

    /// <summary>Governor, party leader, caravan leader, refuge warden, prisoner or fugitive:
    /// anywhere but the main party.</summary>
    NotInMainParty,

    /// <summary>The main party is in a map event or siege, where the engine would only mark the
    /// hero for later and defer the removal.</summary>
    PartyInBattle,

    /// <summary>The origin troop no longer resolves, or resolves to a hero template: nothing
    /// sensible to refund.</summary>
    TroopUnresolved,

    /// <summary>The player is enlisted; symmetric with the offer pump, which sleeps while enlisted.</summary>
    PlayerEnlisted,

    /// <summary>Only from <c>Dismiss</c>: the adapter reported the engine did not remove the hero.
    /// Nothing else was touched.</summary>
    RemovalFailed,
}
