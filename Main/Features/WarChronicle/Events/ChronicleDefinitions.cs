using System.Collections.Generic;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>A narrative event happens once; a siege opens a window over a target town.</summary>
public enum ChronicleEventKind
{
    Narrative,
    Siege,
}

/// <summary>What an event's open day counts from.</summary>
public enum ChronicleAnchor
{
    /// <summary>Day 0 of the campaign.</summary>
    CampaignStart,

    /// <summary>The effective day of the War of the Ring's Isengard War, once that phase is reached.</summary>
    Phase1,

    /// <summary>The effective day of the Full War, once that phase is reached.</summary>
    Phase2,

    /// <summary>The day an earlier event resolved.</summary>
    Event,
}

/// <summary>
/// One validated event of <c>war_chronicle/war_chronicle.json</c> (docs/features/war-chronicle.md "The event chain").
/// Built only by <see cref="ChronicleConfigValidator"/>, so every field holds a checked value.
/// </summary>
public sealed class ChronicleEventDefinition
{
    public string Id { get; set; } = string.Empty;

    public ChronicleEventKind Kind { get; set; }

    public ChronicleAnchor Anchor { get; set; }

    /// <summary>The earlier event this one counts from; set only for <see cref="ChronicleAnchor.Event"/>.</summary>
    public string? AnchorEventId { get; set; }

    /// <summary>When set, the anchor event must have resolved with this state, or this event is Skipped.</summary>
    public ChronicleEventState? RequiresOutcome { get; set; }

    public int OffsetDays { get; set; }

    /// <summary>Siege only: how many days the window stays open.</summary>
    public int WindowDays { get; set; }

    /// <summary>Siege only: the target settlement's StringId.</summary>
    public string? TargetSettlementId { get; set; }

    /// <summary>Siege only: the attacking kingdoms, never containing the defender.</summary>
    public IReadOnlyList<string> Attackers { get; set; } = new string[0];

    /// <summary>Siege only: the kingdom that must own the target when the window opens.</summary>
    public string? Defender { get; set; }

    /// <summary>A <c>{=taom_wc_*}English</c> text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Siege only: the line shown when the window opens; may name {TARGET} and {DAYS}.</summary>
    public string? OpenText { get; set; }

    /// <summary>Siege only: the line shown when the window lapses (no effects).</summary>
    public string? LapsedText { get; set; }

    /// <summary>Narrative only.</summary>
    public ChronicleOutcomeDefinition? Occurred { get; set; }

    /// <summary>Siege only.</summary>
    public ChronicleOutcomeDefinition? Held { get; set; }

    /// <summary>Siege only.</summary>
    public ChronicleOutcomeDefinition? Fell { get; set; }

    /// <summary>Siege only, optional: the story mission the window offers to the player (#1105), and how it ends without one.</summary>
    public ChronicleStoryDefinition? Story { get; set; }

    public bool IsSiege => Kind == ChronicleEventKind.Siege;

    /// <summary>The outcome block for a resolution state; null for Lapsed, Skipped and the open states.</summary>
    public ChronicleOutcomeDefinition? OutcomeFor(ChronicleEventState state)
    {
        switch (state)
        {
            case ChronicleEventState.Occurred: return Occurred;
            case ChronicleEventState.Held: return Held;
            case ChronicleEventState.Fell: return Fell;
            default: return null;
        }
    }
}

/// <summary>
/// A siege window's story offer (#1105 slice 4): the Story Missions id it offers while Active, and the fate of a window that closes with no
/// assault: Held (the book's ending, announced with <see cref="FateText"/>) or Lapsed (the window's own lapsed line, as with no story).
/// </summary>
public sealed class ChronicleStoryDefinition
{
    public string MissionId { get; set; } = string.Empty;

    /// <summary>Held or Lapsed only.</summary>
    public ChronicleEventState Fate { get; set; } = ChronicleEventState.Held;

    /// <summary>A <c>{=taom_wc_*}English</c> line for a Held fate; null falls back to the Held outcome's text.</summary>
    public string? FateText { get; set; }
}

/// <summary>An Active story window the player has not declined: what the offer shows and launches.</summary>
public sealed class ChronicleStoryOffer
{
    public ChronicleStoryOffer(string eventId, string missionId, string title, string? targetSettlementId, IReadOnlyList<string> attackers)
    {
        EventId = eventId;
        MissionId = missionId;
        Title = title;
        TargetSettlementId = targetSettlementId;
        Attackers = attackers;
    }

    /// <summary>The window's attacking kingdoms in configuration order: a lost mission gives the target to the first that can take it.</summary>
    public IReadOnlyList<string> Attackers { get; }

    public string EventId { get; }

    public string MissionId { get; }

    public string Title { get; }

    public string? TargetSettlementId { get; }
}

/// <summary>What one resolution announces and pays.</summary>
public sealed class ChronicleOutcomeDefinition
{
    public string? Text { get; set; }

    public IReadOnlyList<ChronicleEffectDefinition> Effects { get; set; } = new ChronicleEffectDefinition[0];

    public ChronicleMomentumAward? Momentum { get; set; }
}

/// <summary>
/// One timed effect row. <see cref="Target"/> is <c>attacker</c>, <c>defender</c> (sieges only),
/// <c>free</c>, <c>evil</c>, or a kingdom id.
/// </summary>
public sealed class ChronicleEffectDefinition
{
    public string Target { get; set; } = string.Empty;

    public WarEffectKind Kind { get; set; }

    /// <summary>A fraction: 0.15 is +15%. Finite, non-zero, at most 0.25 either way.</summary>
    public float Magnitude { get; set; }

    public int DurationDays { get; set; }
}

/// <summary>An optional War of the Ring momentum award (M6) to one side, in displayed units.</summary>
public sealed class ChronicleMomentumAward
{
    public MomentumSide Side { get; set; }

    public int Units { get; set; }
}
