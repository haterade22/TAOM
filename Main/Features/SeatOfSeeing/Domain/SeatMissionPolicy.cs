namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What sort of mission is running, for the Seat's policy (build-spec section 4).</summary>
internal enum SeatMissionKind
{
    Battle,
    Hideout,
    Tournament,
    Arena,
    Settlement,
    Other,
}

/// <summary>
/// The facts <see cref="SeatMissionPolicy"/> decides from. <paramref name="NetworkSession"/> is true for the engine's network session or a
/// live co-op session; <paramref name="ForeignCamera"/> is true when another mod's view owns the camera (build-spec 5.6).
/// </summary>
internal readonly record struct SeatMissionFacts(SeatMissionKind Kind, bool NetworkSession, bool ForeignCamera);

/// <summary>What the Seat may do in a mission; the default allows nothing.</summary>
internal readonly record struct SeatPermissions(bool Camera, bool FightOn, bool Orders, bool Time, bool TakeSoldier);

/// <summary>
/// One pure function decides what the Seat may do in a mission, and everything else asks it (build-spec section 4). A battle
/// gets everything; a hideout loses the hero's hand-off; a tournament, arena, town or other mission gets the camera only; a
/// network session keeps the camera and the orders its kind allows and loses the hand-off and mission time; another camera gets
/// nothing. A dedicated server never asks: the only reader of the facts is in TAOM.Client, which does not load there.
/// </summary>
internal static class SeatMissionPolicy
{
    internal static SeatPermissions Decide(in SeatMissionFacts facts)
    {
        if (facts.ForeignCamera)
            return default;

        var battle = facts.Kind == SeatMissionKind.Battle;
        var orders = battle || facts.Kind == SeatMissionKind.Hideout;
        var singlePlayer = !facts.NetworkSession;
        return new SeatPermissions(Camera: true, FightOn: battle && singlePlayer, Orders: orders, Time: orders && singlePlayer, TakeSoldier: battle && singlePlayer);
    }
}
