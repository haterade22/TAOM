namespace TAOM.Features.MarshalsWord.Domain;

internal enum VolleyCheck
{
    Hold,
    Release,
}

/// <summary>One judged window: which check, whether it passed, how many soldiers held and how many shots counted.</summary>
internal readonly record struct VolleyVerdict(VolleyCheck Check, bool Pass, int Holding, int Shots);

/// <summary>
/// The two volley checks of build-spec 7.3 for one company, as a pure state machine fed by the driver: a hold window of
/// <c>HoldWindowSeconds</c> with at least <c>VolleyMinHolding</c> soldiers holding and no word, and the release window after a word. A window
/// that cannot prove anything judges nothing.
/// </summary>
internal sealed class MarshalVolleyJudge
{
    private double _holdStart = double.NaN;
    private int _holdSmallest;
    private int _holdShotsAtStart;

    private bool _releaseOpen;
    private double _wordAt;
    private int _wordHolding;
    private int _releaseShotsAtWord;

    /// <summary>A word opened a release window that has not been judged yet.</summary>
    internal bool ReleaseOpen => _releaseOpen;

    internal VolleyVerdict? JudgeHold(double now, int holding, int shotsWhileHolding, double lastWord)
    {
        if (holding < MarshalTuning.VolleyMinHolding)
        {
            _holdStart = double.NaN;
            return null;
        }

        if (!double.IsNaN(_holdStart) && lastWord >= _holdStart)
            _holdStart = double.NaN;

        if (double.IsNaN(_holdStart))
        {
            _holdStart = now;
            _holdSmallest = holding;
            _holdShotsAtStart = shotsWhileHolding;
            return null;
        }

        _holdSmallest = System.Math.Min(_holdSmallest, holding);
        if (now - _holdStart < MarshalTuning.HoldWindowSeconds)
            return null;

        var shots = shotsWhileHolding - _holdShotsAtStart;
        var verdict = new VolleyVerdict(VolleyCheck.Hold, shots * MarshalTuning.HoldShotShareDenominator <= _holdSmallest, _holdSmallest, shots);
        _holdStart = double.NaN;
        return verdict;
    }

    /// <summary>A word opens a release window. A window still open keeps its own word, holders and baseline until it is judged, so a second word cannot erase a failure the first one owes.</summary>
    internal void NoteWord(double now, int holding, int shotsAfterWord)
    {
        if (_releaseOpen)
            return;
        _releaseOpen = holding >= MarshalTuning.VolleyMinHolding;
        _wordAt = now;
        _wordHolding = holding;
        _releaseShotsAtWord = shotsAfterWord;
    }

    internal VolleyVerdict? JudgeRelease(double now, int shotsAfterWord)
    {
        if (!_releaseOpen || now < _wordAt + MarshalTuning.WordJitter + MarshalTuning.ReleaseWindowPadSeconds)
            return null;

        _releaseOpen = false;
        var loosed = shotsAfterWord - _releaseShotsAtWord;
        return new VolleyVerdict(VolleyCheck.Release, loosed * 100 >= _wordHolding * MarshalTuning.ReleaseSharePercent, _wordHolding, loosed);
    }
}
