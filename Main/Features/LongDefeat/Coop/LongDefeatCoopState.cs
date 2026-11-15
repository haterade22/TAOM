namespace TAOM.Features.LongDefeat.Coop;

/// <summary>A member's part in a Long Defeat co-op run. Coop elects the host when the missions load, so it is Pending until then.</summary>
internal enum LongDefeatCoopRole
{
    None,
    Pending,
    Host,
    Follower,
}

/// <summary>
/// The co-op session this process is in (#1280): the Coop link, the session id, the host epoch and this member's role. Written only by
/// <see cref="LongDefeatCoopRuns"/>. The launcher and the menu entry read it through <see cref="LongDefeatCoopRuns.State"/>; the run
/// host reads the role through <see cref="LongDefeatRunCoop.Role"/>.
/// </summary>
internal sealed class LongDefeatCoopState
{
    internal ILongDefeatCoopLink? Link { get; set; }

    internal string? SessionId { get; private set; }

    internal int Epoch { get; private set; }

    private LongDefeatCoopRole _role;

    /// <summary>Coop is loaded and its session API answered.</summary>
    public bool IsAvailable => Link != null;

    public LongDefeatCoopRole RoleFor(string? sessionId) =>
        sessionId != null && sessionId == SessionId ? _role : LongDefeatCoopRole.None;

    internal void Enter(string sessionId)
    {
        SessionId = sessionId;
        Epoch = 0;
        _role = LongDefeatCoopRole.Pending;
    }

    internal void SetRole(int epoch, LongDefeatCoopRole role)
    {
        Epoch = epoch;
        _role = role;
    }

    internal void Exit()
    {
        SessionId = null;
        Epoch = 0;
        _role = LongDefeatCoopRole.None;
    }
}
