using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The outlines, planned by a pure diff and painted through an adapter four times a second of mission time.</summary>
internal sealed class MarshalOutlineService<TKey> : IMarshalOutlines
    where TKey : notnull
{
    private readonly IMarshalOutlineAdapter<TKey> _adapter;
    private readonly Dictionary<TKey, uint> _painted = new();
    private readonly Dictionary<TKey, uint> _wanted = new();
    private readonly List<KeyValuePair<TKey, uint>> _toPaint = new();
    private readonly List<TKey> _toClear = new();
    private double _nextPass = double.NegativeInfinity;

    internal MarshalOutlineService(IMarshalOutlineAdapter<TKey> adapter) => _adapter = adapter;

    /// <summary>How many units wear an outline this service painted.</summary>
    public int PaintedCount => _painted.Count;

    public void Pass(double now, bool show)
    {
        if (now < _nextPass || (!show && _painted.Count == 0))
            return;
        _nextPass = now + MarshalTuning.OutlinePassSeconds;
        Refresh(show);
    }

    /// <summary>Clears every painted unit at once, each on its own, and throws the first failure after the rest are done.</summary>
    public void ClearAll()
    {
        Exception? first = null;
        foreach (var unit in _painted.Keys)
        {
            try
            {
                _adapter.Paint(unit, null);
            }
            catch (Exception ex)
            {
                first ??= ex;
            }
        }

        _painted.Clear();
        if (first != null)
            ExceptionDispatchInfo.Capture(first).Throw();
    }

    /// <summary>Forgets what it painted with no native call. The finish of a mission calls it, because the agents go with the mission; after a fault it follows <see cref="ClearAll"/>.</summary>
    public void Drop()
    {
        _painted.Clear();
        _wanted.Clear();
        _toPaint.Clear();
        _toClear.Clear();
    }

    private void Refresh(bool show)
    {
        _wanted.Clear();
        if (show)
            _adapter.ReadWanted(_wanted);
        OutlineDiff.Plan(_painted, _wanted, _toPaint, _toClear);
        foreach (var unit in _toClear)
        {
            _adapter.Paint(unit, null);
            _painted.Remove(unit);
        }

        foreach (var paint in _toPaint)
        {
            _adapter.Paint(paint.Key, paint.Value);
            _painted[paint.Key] = paint.Value;
        }
    }
}
