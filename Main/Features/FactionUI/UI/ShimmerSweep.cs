namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The timing of one light sweep across a main-menu title (#704), from Kysaro's
/// <c>MainMenuShimmer.Sweep</c>: after <c>firstDelay</c>, every <c>cycle</c> seconds a highlight eases
/// across the box in <c>sweepTime</c> seconds, from <c>margin</c> before its left edge to
/// <c>margin</c> past its right; the rest of the cycle it is parked off screen.
/// </summary>
public sealed class ShimmerSweep
{
    /// <summary>Kysaro's off-screen position for a sweep that is not running.</summary>
    public const float Parked = -400f;

    private readonly float _boxWidth;
    private readonly float _firstDelay;
    private readonly float _cycle;
    private readonly float _sweepTime;
    private readonly float _margin;

    public ShimmerSweep(float boxWidth, float firstDelay, float cycle, float sweepTime, float margin)
    {
        _boxWidth = boxWidth;
        _firstDelay = firstDelay;
        _cycle = cycle;
        _sweepTime = sweepTime;
        _margin = margin;
    }

    /// <summary>The sweep centre's horizontal offset at <paramref name="time"/> seconds after the menu opened.</summary>
    public float PositionAt(float time)
    {
        if (time < _firstDelay)
            return Parked;

        var intoCycle = (time - _firstDelay) % _cycle;
        if (!(intoCycle >= 0f && intoCycle < _sweepTime))
            return Parked;

        var progress = intoCycle / _sweepTime;
        var eased = progress * progress * (3f - 2f * progress);
        return -_margin + eased * (_boxWidth + 2f * _margin);
    }
}
