using System;
using TAOM.Core.Logging;

namespace TAOM.Features.MixedFormations;

/// <summary>
/// A setting string parsed into an enum only when it differs from the last value read, instead of on
/// every read. MCM returns the same string until the player edits it, so the per-frame cost is one
/// string compare.
/// <para>
/// What counts as a valid value is the caller's parser, not this class: it trims the text, answers
/// null, empty and whitespace as unusable without calling the parser, and remembers the last answer
/// including a refusal (the cycle hotkey's rule is <see cref="CycleHotkeyParser"/>).
/// </para>
/// <para>
/// Each time the setting changes to a value that does not parse, one WARNING is logged saying the name
/// was not recognised: the caller's feature then treats the setting as off, and the log is the only
/// place that says why. Not thread-safe: one instance belongs to one caller on one thread.
/// </para>
/// </summary>
public sealed class CachedEnumParse<TEnum> where TEnum : struct, Enum
{
    internal delegate bool Parser(string text, out TEnum value);

    private readonly Parser _parse;
    private readonly IModLogger? _logger;
    private readonly string? _label;
    private bool _hasResult;
    private string? _lastRaw;
    private bool _lastOk;
    private TEnum _lastValue;

    /// <param name="parse">Decides whether the trimmed, non-empty text names a usable value.</param>
    /// <param name="logger">Receives one WARNING each time the value changes to one that does not parse.</param>
    /// <param name="label">Names the setting at the start of that line, e.g. "[MixedFormations] cycle hotkey".</param>
    internal CachedEnumParse(Parser parse, IModLogger? logger = null, string? label = null)
    {
        _parse = parse;
        _logger = logger;
        _label = label;
    }

    public bool TryGet(string? raw, out TEnum value)
    {
        if (!_hasResult || !string.Equals(raw, _lastRaw, StringComparison.Ordinal))
        {
            var trimmed = raw?.Trim();
            _lastOk = !string.IsNullOrEmpty(trimmed) && _parse(trimmed!, out _lastValue);
            if (!_lastOk)
            {
                _lastValue = default;
                _logger?.LogWarning(
                    $"{_label} '{raw}' is not a recognised {typeof(TEnum).Name} name; it is ignored until the setting changes");
            }
            _lastRaw = raw;
            _hasResult = true;
        }

        value = _lastValue;
        return _lastOk;
    }
}
