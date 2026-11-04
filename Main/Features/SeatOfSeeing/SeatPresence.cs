namespace TAOM.Features.SeatOfSeeing;

/// <summary>Whether the player's view is in the Seat right now; the order-panel patches read it to be a no-op while it is off.</summary>
internal static class SeatPresence
{
    private static volatile bool _seated;

    internal static bool IsSeated => _seated;

    internal static void Set(bool seated) => _seated = seated;
}
