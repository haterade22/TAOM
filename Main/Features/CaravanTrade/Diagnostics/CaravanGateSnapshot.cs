namespace TAOM.Features.CaravanTrade.Diagnostics;

/// <summary>
/// Everything the parking verdict needs, read from the sealed engine types at the console-command
/// boundary (ADR-007) so <see cref="CaravanGateDiagnosticsService"/> stays TaleWorlds-free and
/// 100% unit-testable.
///
/// Every field here is a PUBLIC engine getter — no reflection is involved anywhere in this
/// diagnostic. <c>MobilePartyAi.IsAlerted</c>, <c>.DoNotMakeNewDecisions</c> and <c>.IsDisabled</c>
/// are all public-get/private-set, and the rest hang off <c>MobileParty</c> directly.
/// </summary>
public class CaravanGateSnapshot
{
    public string CaravanId { get; set; }
    public string CaravanName { get; set; }

    /// <summary>Where it is parked. Null when the caravan is on the open map.</summary>
    public string CurrentSettlementId { get; set; }

    /// <summary>
    /// Its AI target, shown for context only.
    ///
    /// Note this is NOT the property the engine's exit check keys on. That check
    /// (<c>MobileParty.CheckExitingSettlementParallel</c> :4085-4104) tests
    /// <c>ShortTermTargetSettlement</c> — a derived property backed by
    /// <c>Ai.AiBehaviorPartyBase?.Settlement</c> — and only consults <c>TargetSettlement</c> as part
    /// of a compound condition. They usually agree, which is why <c>TargetSettlement</c> is still
    /// worth printing, but a verdict must never be derived from it.
    /// </summary>
    public string TargetSettlementId { get; set; }

    /// <summary>
    /// <c>ShortTermBehavior == AiBehavior.Hold</c>. Set by <c>SetMoveModeHold()</c>, which
    /// <c>CaravansCampaignBehavior.OnSiegeEventStarted</c> (:317-326) applies to every caravan
    /// inside a settlement that comes under siege. It is the second clause of the engine's exit
    /// guard, so a held party cannot leave no matter what the caravan behavior decides.
    ///
    /// Transient on the normal path: <c>Hold</c> is not in <c>HourlyTickParty</c>'s early-return set
    /// (:625), so once the siege lifts the caravan re-decides within a few hours and the resulting
    /// <c>SetMoveGoToSettlement</c> clears it. It becomes permanent only in combination — a held
    /// caravan that also scores zero everywhere never gets the fresh order that would release it.
    /// Report it, but do not read it as "stuck forever" on its own.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// The wounded/alert/siege ladder lives inside the <c>CurrentSettlement.IsFortification</c>
    /// branch of <c>HourlyTickParty</c>; a caravan on the open map re-decides through a different
    /// path, so those gates must not be applied to it.
    /// </summary>
    public bool IsInFortification { get; set; }

    public bool IsCurrentSettlementUnderSiege { get; set; }
    public bool IsBlockadeActive { get; set; }
    public bool HasNavalNavigationCapability { get; set; }

    public bool HasMapEvent { get; set; }
    public bool IsPartyTradeActive { get; set; }
    public bool DoNotMakeNewDecisions { get; set; }
    public bool IsAiDisabled { get; set; }
    public bool IsAlerted { get; set; }

    /// <summary><c>ShortTermBehavior == AiBehavior.FleeToPoint</c>.</summary>
    public bool IsFleeing { get; set; }

    /// <summary>Quest caravans skip the engine's 1-in-3 hourly roll entirely.</summary>
    public bool IsCurrentlyUsedByAQuest { get; set; }

    public int TotalManCount { get; set; }
    public int TotalWounded { get; set; }

    public int PartyTradeGold { get; set; }
    public int CargoItemCount { get; set; }
}

/// <summary>
/// Why a caravan is not moving, named after the engine site that stops it. Ordered by the engine's
/// own evaluation order so the first one that fires is the one worth acting on.
/// </summary>
public enum CaravanGate
{
    /// <summary>Free to move. If it is still parked, the cause is not in this list.</summary>
    NotBlocked,

    /// <summary><c>MobileParty.CheckExitingSettlementParallel</c> — cannot physically exit at all.</summary>
    AiDisabled,

    /// <summary>
    /// <c>ShortTermBehavior == Hold</c> — the second clause of the same exit guard. Applied to every
    /// caravan in a settlement that comes under siege, and it persists after the siege lifts until a
    /// new movement order replaces it.
    /// </summary>
    HeldInPlace,

    /// <summary>In a battle. Transient and expected.</summary>
    InMapEvent,

    /// <summary>
    /// <c>!IsPartyTradeActive</c>. Only <c>InitializePartyTrade</c> ever sets it, and
    /// <c>CaravanPartyComponent.ConvertPartyToCaravanParty</c> does not call it — a converted
    /// caravan can be inert for the whole campaign.
    /// </summary>
    TradeInactive,

    /// <summary>
    /// <c>Ai.DoNotMakeNewDecisions</c>, set by five quest behaviors. If no quest is active this is a
    /// quest-cleanup bug, not an economy problem.
    /// </summary>
    DecisionsSuppressed,

    /// <summary>Siege on the settlement it is sheltering in.</summary>
    HoldingForSiege,

    /// <summary>Actively fleeing — <c>ShortTermBehavior == FleeToPoint</c>.</summary>
    Fleeing,

    /// <summary>
    /// <c>Ai.IsAlerted</c>, set only in the flee branch and recomputed every think, so a caravan
    /// sheltering from a nearby battle stays alerted for as long as the threat persists.
    /// </summary>
    Alerted,

    /// <summary>At or above 40% wounded: the leave chance is exactly zero until it heals.</summary>
    WoundedCannotLeave,

    /// <summary>Wounded enough to be slow, but it will leave eventually.</summary>
    WoundedUnlikelyToLeave,

    /// <summary>
    /// Broke AND empty. The buy half of the trade score is capped at <c>(int)(0.5 * PartyTradeGold)</c>
    /// and the sell half needs cargo, so every town scores exactly 0, the engine's strictly-greater
    /// test never passes, and no destination is ever set. Permanent without intervention.
    /// </summary>
    NoViableDestination,
}

/// <summary>The verdict plus the numbers a reader needs to act on it.</summary>
public class CaravanGateVerdict
{
    public CaravanGate Gate { get; set; }

    /// <summary>The engine's <c>num2</c> — the wounded-band chance, before the 1-in-3 roll.</summary>
    public float LeaveChancePerThink { get; set; }

    /// <summary>
    /// Chance per game-hour of actually departing: <see cref="LeaveChancePerThink"/> times the
    /// engine's <c>randomFloat &lt; 1/3</c> pre-roll, which quest caravans skip. Reporting the band
    /// alone would overstate a slow caravan's recovery threefold.
    /// </summary>
    public float EffectiveHourlyLeaveChance { get; set; }

    public float WoundedFraction { get; set; }

    public string Explanation { get; set; }
}
