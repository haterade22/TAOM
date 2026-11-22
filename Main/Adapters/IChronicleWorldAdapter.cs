namespace TAOM.Adapters;

/// <summary>
/// What the War Chronicle's event chain reads of the campaign map (docs/features/war-chronicle.md
/// "Events"): the clock, whether a settlement or kingdom id exists, who owns a target and who besieges
/// it. The services never touch <c>Settlement</c>, <c>Kingdom</c>, <c>SiegeEvent</c> or
/// <c>CampaignTime</c> (ADR-007). Kingdom wars and the live kingdom list come from
/// <see cref="IAllianceAdapter"/>.
/// </summary>
public interface IChronicleWorldAdapter
{
    /// <summary>
    /// Days since the campaign start, with the fraction; NaN with no campaign, and a non-finite engine value
    /// is passed through unmasked, so the caller's finite gate can skip the step.
    /// </summary>
    double GetElapsedDays();

    /// <summary>Campaign time in hours (<c>CampaignTime.Now.ToHours</c>); 0 with no campaign.</summary>
    double GetNowHours();

    /// <summary>A settlement with this StringId exists in the campaign.</summary>
    bool SettlementExists(string settlementId);

    /// <summary>A kingdom with this StringId exists in the campaign, eliminated or not.</summary>
    bool KingdomExists(string kingdomId);

    /// <summary>The StringId of the kingdom that owns the settlement; null when unknown or owned outside a kingdom.</summary>
    string? GetOwnerKingdomId(string settlementId);

    /// <summary>The StringId of the kingdom whose army besieges the settlement; null when it is not under siege by a kingdom.</summary>
    string? GetBesiegerKingdomId(string settlementId);

    /// <summary>True for a hero of this campaign who is alive (#1105 slice 3).</summary>
    bool IsHeroAlive(string heroId);

    /// <summary>True when the hero is the human player's own character.</summary>
    bool IsPlayerHero(string heroId);

    /// <summary>
    /// Kills the hero as a death in battle with no killer. Refused for a missing or dead hero, for the player's character and when the engine
    /// refuses the death; Deferred when the engine only marked him because his party is in a battle or besieges. Only Killed is a death: the
    /// caller asks again later for the other two, and a marked hero is killed by such a later call once his party is out of the battle and
    /// besieges nothing. A hero marked for an execution is left to that execution.
    /// </summary>
    ChronicleHeroDeath KillHeroInBattle(string heroId);
}

/// <summary>What <see cref="IChronicleWorldAdapter.KillHeroInBattle"/> did.</summary>
public enum ChronicleHeroDeath
{
    Killed,
    Deferred,
    Refused,
}
