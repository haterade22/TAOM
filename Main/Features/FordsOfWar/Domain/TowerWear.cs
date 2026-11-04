namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// How much a long match has worn the walls down. Even sides fight mid-lane and rarely reach a tower, so without this a match between them would not end. Up to the start minute
/// the factor is 1, and after it the factor rises by the rate each minute. A rate of 0 turns it off.
/// </summary>
internal sealed class TowerWear
{
    private readonly float _startMinutes;
    private readonly float _perMinute;

    public TowerWear(float startMinutes, float perMinute)
    {
        _startMinutes = startMinutes;
        _perMinute = perMinute;
    }

    public bool HasBegun(float minute) => _perMinute > 0f && minute > _startMinutes;

    public float Factor(float minute) => HasBegun(minute) ? 1f + (minute - _startMinutes) * _perMinute : 1f;
}
