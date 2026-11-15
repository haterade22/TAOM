namespace TAOM.Features.LongDefeat.Coop;

/// <summary>What BannerlordCoop's session events reach in TAOM, through TAOM.CoopCompat (#1280). Every call comes on the game thread.</summary>
public interface ILongDefeatCoopPort
{
    void Attach(ILongDefeatCoopLink link);

    void OnMissionOpenRequested(string sessionId, byte[] payload);

    void OnHostChanged(string sessionId, int epoch, bool isLocalHost);

    void OnMissionEndRequested(string sessionId);

    void OnRunEnded(string sessionId, int epoch, int reason, int wave, int wavesCleared, int kills);
}
