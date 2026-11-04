namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>What the control view does this frame.</summary>
internal enum FordsControlStep
{
    None,
    TakePointer,
    TakeFront,
    Return,
}

/// <summary>
/// The unattended drive of soldier control (arm key <c>control_check</c>), which no keyboard reaches in such a run. Three rounds take the
/// front soldier and hold it: the first hands it back alive, the second waits long enough for it to fall, the third takes control again after
/// that fall. A soldier that falls ends its round early. Pure: the view passes the mission clock and whether a soldier is controlled.
/// </summary>
internal sealed class FordsControlCheck
{
    internal const float RestSeconds = 15f;

    internal static readonly float[] Holds = { 30f, 180f, 15f };

    private int _round;
    private bool _holding;
    private float _due;

    public FordsControlCheck(float startSeconds) => _due = startSeconds;

    public bool Done => _round >= Holds.Length;

    public FordsControlStep Next(float missionTime, bool controlling)
    {
        if (Done)
            return FordsControlStep.None;

        if (!_holding)
        {
            if (!controlling)
                return missionTime >= _due ? FordsControlStep.TakeFront : FordsControlStep.None;

            _holding = true;
            _due = missionTime + Holds[_round];
            return FordsControlStep.None;
        }

        if (controlling)
            return missionTime >= _due ? FordsControlStep.Return : FordsControlStep.None;

        _holding = false;
        _round++;
        _due = missionTime + RestSeconds;
        return FordsControlStep.None;
    }
}
