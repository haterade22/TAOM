namespace TAOM.Adapters;

/// <summary>The Eye of Sauron hunter's party (#765 M8), read from the live <c>MobileParty</c> on every use.</summary>
public sealed class HuntPartySnapshot
{
    public static readonly HuntPartySnapshot Missing = new();

    public bool Exists { get; set; }

    public bool IsActive { get; set; }

    public string? LeaderHeroId { get; set; }

    public bool InMapEvent { get; set; }

    /// <summary><c>Army != null || AttachedTo != null</c>: leading an army, or attached to one.</summary>
    public bool InArmy { get; set; }

    /// <summary>Besieging a settlement (a siege camp).</summary>
    public bool Besieging { get; set; }

    public bool InSettlement { get; set; }

    public bool IsDisbanding { get; set; }

    /// <summary><c>Ai.DoNotMakeNewDecisions</c>, whoever set it.</summary>
    public bool AiPinned { get; set; }

    /// <summary>Its default behaviour is to engage the player's party.</summary>
    public bool IsEngagingPlayer { get; set; }

    /// <summary>
    /// <c>Ai.DoNotAttackMainPartyUntil</c> is in the future: vanilla's promise to leave the player alone after a safe
    /// passage bribe (32 h), a lord paid off in conversation (24 h) or an escape from a lost encounter (2 h).
    /// </summary>
    public bool HoldsOffPlayer { get; set; }

    public int HeadCount { get; set; }

    /// <summary><c>Party.EstimatedStrength</c>.</summary>
    public float Strength { get; set; }
}
