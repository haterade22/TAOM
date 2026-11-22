namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// What became of the soldier a lord had taken, when the field raised him (phase4-plan.md D40): handed back to the AI, already AI-driven (<c>Noop</c>, every body under autoplay), or none was taken. A soldier who
/// fell before the clear is <c>None</c> too: the run then orders a rise with no hand-back. <c>Dead</c> is only the field's answer for a soldier who stopped standing between the run's order and the hand-back.
/// </summary>
internal enum LdHandBack
{
    None,
    Done,
    Noop,
    Dead,
}

/// <summary>
/// The field's answer to a rise. <c>Spawned</c> is false when the engine refused the lord's spawn, and then a taken soldier is the player's again. <c>WasUsing</c>: the soldier handed back was using a machine or a ladder
/// (R23: his use is not stopped first, and the field says so).
/// </summary>
internal sealed record LdRise(bool Spawned, LdHandBack HandBack, bool WasUsing)
{
    public static LdRise Refused { get; } = new(false, LdHandBack.None, false);

    /// <summary>True when a taken soldier fights on under the AI: handed back, or already AI-driven.</summary>
    public bool HandedBack => HandBack == LdHandBack.Done || HandBack == LdHandBack.Noop;
}

/// <summary>The field's answer to the bot's take-over: whether a soldier was taken, and his troop. False when no soldier qualified.</summary>
internal readonly record struct LdTake(bool Took, string Troop);

internal enum LdSeatKind
{
    /// <summary>No general.</summary>
    None,

    /// <summary>The live main agent.</summary>
    Main,

    /// <summary>A live held hero.</summary>
    Hero,

    /// <summary>A body that is neither the main agent nor a live hero: the dead lord's.</summary>
    Stale,
}

/// <summary>Who the engine names as the player's team general now.</summary>
internal readonly record struct LdSeatRead(LdSeatKind Kind, string HeroId = "");

/// <summary>The controller of the main agent now (<c>Player</c>, <c>AI</c>, or <c>none</c> with no main agent) and how many other live humans of the player's side are also <c>Player</c>-controlled.</summary>
internal readonly record struct LdControlRead(string Controller, int OtherPlayerBodies);
