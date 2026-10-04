using System;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.LocalizationOverride;

/// <summary>
/// Says whether the English override table applies to the text language that is active now: only in
/// English (#706).
/// <para>
/// The table exists because vanilla <c>MBTextManager.GetLocalizedText</c> returns a <c>{=ID}text</c>'s
/// inline text for English without reading any dictionary (v1.5.3 <c>MBTextManager.cs:264</c>). In every
/// other language it reads that language's rows, which TAOM translates, so an English override there
/// would hide the player's own language. The predicate is vanilla's own: an ordinal match on the
/// language id <c>"English"</c>.
/// </para>
/// <para>
/// The language is read on every call, not cached: the engine can change it while the game runs, and
/// vanilla compares it on every lookup too. The one piece of state is the last language seen, so the
/// INFO lines are written once per change and not once per lookup: one when the overrides go off for
/// another language, one when English comes back. A session that starts in English writes nothing, since
/// nothing was off before it. A text can resolve on any thread, so that latch is a single interlocked
/// field.
/// </para>
/// </summary>
internal sealed class OverrideLanguageGate
{
    // The id MBTextManager keeps for English, the value GetLocalizedText compares with ==.
    private const string EnglishLanguageId = "English";

    private readonly ITextLocalizerAdapter _localizer;
    private readonly Func<IModLogger> _logger;
    private string? _lastSeen;

    /// <param name="localizer">Reads the active text language.</param>
    /// <param name="logger">Resolved only when a line is written, so the container need not be ready
    /// when the first text resolves.</param>
    public OverrideLanguageGate(ITextLocalizerAdapter localizer, Func<IModLogger> logger)
    {
        _localizer = localizer;
        _logger = logger;
    }

    public bool AllowsOverrides()
    {
        string? language = _localizer.ActiveLanguage;
        if (language != _lastSeen)
            NoteLanguageChange(language);

        return language == EnglishLanguageId;
    }

    // The latch step of AllowsOverrides. Internal so a test can call it without that method's own
    // "language changed" check in front of it.
    internal void NoteLanguageChange(string? language)
    {
        // The exchange makes exactly one thread the one that saw the old value, so one thread logs.
        string? previous = Interlocked.Exchange(ref _lastSeen, language);

        // Nothing changed, or the first language seen is English: nothing was off before it, so say nothing.
        if (previous == language || (previous == null && language == EnglishLanguageId))
            return;

        string message = language == EnglishLanguageId
            ? "[LocalizationOverride] Text language is English again: the English string overrides apply"
            : $"[LocalizationOverride] Text language '{language}' is not English: the English string overrides "
              + "are skipped and vanilla reads that language's own rows";

        try
        {
            _logger().LogInfo(message);
        }
        catch (Exception)
        {
            // A log line is not worth a failed text lookup, and the latch above is already set, so a
            // logger that cannot be resolved costs one attempt rather than one per lookup.
        }
    }
}
