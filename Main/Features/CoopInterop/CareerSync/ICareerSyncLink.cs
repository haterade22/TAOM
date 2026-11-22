namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>
/// The way to the co-op server for career sync, which TAOM.CoopCompat supplies. TAOM.dll names no Coop type, so the
/// relay attaches itself to <see cref="CareerSyncClient.Attach"/> when Coop is usable.
/// </summary>
public interface ICareerSyncLink
{
    /// <summary>False when this process is not a client in a session or has no network.</summary>
    bool SendToHost(CareerSyncSnapshot career, long sequence, bool joinReport);
}
