using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Coop;

/// <summary>
/// What a Long Defeat co-op run (#1280) asks of BannerlordCoop's session API. TAOM.dll names no Coop type, so TAOM.CoopCompat
/// implements this over Coop's <c>SessionLauncher</c> and hands it to <see cref="ILongDefeatCoopPort.Attach"/>.
/// </summary>
public interface ILongDefeatCoopLink
{
    /// <summary>Hosts a session under <see cref="LongDefeatCoopContract.ModId"/> with this payload.</summary>
    void Host(byte[] payload);

    void Join(string address, int port);

    /// <summary>The behaviours Coop adds to the session mission; empty without a running session.</summary>
    IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId);

    /// <summary>Asks the server to close the session. False unless this member is the host.</summary>
    bool Close();

    /// <summary>Sends the run's result to the other members. False when no session mission is live.</summary>
    bool SendRunEnded(string sessionId, int epoch, int reason, int wave, int wavesCleared, int kills);
}
