using System.Collections.Generic;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// A co-op guest's rung of the lord's gear ladder as the co-op server holds it (docs/features/coop-interop.md, "Lords'
/// Ladder for co-op players"). The guest's own copy of the ladder state is the one from its join and goes stale, so its armoury lists the
/// ladder from this. <see cref="MaterialId"/> is set when the rung waits for lord's materials (not done yet, a material
/// and a piece to award exist); <see cref="Choices"/> only when the rung is done and waits to be claimed, at most
/// <see cref="MaxChoices"/> ids of at most <see cref="MaxIdLength"/> characters.
/// </summary>
/// <param name="CanClaim">The armoury's level on the server is at least <paramref name="LordLevel"/>.</param>
public sealed record GuestLadderView(
    LadderSlot Slot,
    bool Ready,
    bool CanClaim,
    int LordLevel,
    string? MaterialId,
    int Needed,
    int Carried,
    IReadOnlyList<string> Choices)
{
    public const int MaxChoices = 64;

    public const int MaxIdLength = 128;
}

/// <summary>
/// What the co-op server did with a guest's ladder hand-in or claim, carried in <see cref="GuestTradeReply.Status"/>.
/// The values travel on the wire as ints: never renumber.
/// </summary>
public enum GuestLadderOutcome
{
    Done = 0,

    /// <summary>The guest's rung is not the one it saw: another slot, already done, or not done yet.</summary>
    RungChanged = 1,

    /// <summary>The party carries fewer lord's materials than the rung needs.</summary>
    NotEnough = 2,

    /// <summary>The armoury is below the lord level, so its armourer cannot fit the piece.</summary>
    ArmouryLevel = 3,

    /// <summary>The piece is not one the rung awards this hero.</summary>
    NotAChoice = 4,

    /// <summary>The server's "Lords' Ladder for co-op players" switch is off, or Armour Acquisition is.</summary>
    Off = 5,

    /// <summary>Every check passed but the server could not take the materials or give the piece.</summary>
    NotDone = 6,
}
