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

    /// <summary>The event window's illustration: a sprite name, the same for every announcement of the event. Null shows the text alone.</summary>
    public string? Image { get; set; }

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
