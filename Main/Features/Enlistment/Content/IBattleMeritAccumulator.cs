using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// Hands the mission-side merit sample to the campaign side. The mission behavior submits
/// exactly once at mission end; the content behavior consumes it at the map-event end that
/// follows. Never persisted — a save mid-battle simply loses the sample (correct: merit is
/// for fought-through battles).
/// </summary>
public interface IBattleMeritAccumulator
{
    void Submit(MeritSample sample);

    /// <summary>Returns and clears the pending sample, or null when none.</summary>
    MeritSample Consume();
}

public class BattleMeritAccumulator : IBattleMeritAccumulator
{
    private MeritSample _pending;

    public void Submit(MeritSample sample) => _pending = sample;

    public MeritSample Consume()
    {
        var sample = _pending;
        _pending = null;
        return sample;
    }
}
