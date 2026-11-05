using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using TAOM.Core.Diagnostics;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Party;

/// <summary>
/// The per-screen character-code cache behind "Party Fast Open" (docs/features/shadowfax-screens.md). Vanilla rebuilds
/// one CharacterCode per row, and the same character appears in several rows and in every rebuild. Keys and values are
/// opaque objects compared by reference, so the service holds no TaleWorlds type and two Equals-equal objects never
/// collide.
/// <para>
/// The cache lives for one PartyVM session: from its constructor to its OnFinalize, or to the next PartyVM
/// constructor, whose <see cref="BeginScreen"/> clears it (Talk deactivates and reactivates the screen and abandons the
/// old view model without OnFinalize). Outside a session, or when "Party Fast Open" was off at
/// <see cref="BeginScreen"/>, every lookup misses and every store is ignored; Begin and End still run, because a toggle
/// gates input and output, never a state transition.
/// </para>
/// Main thread only (UI), so there are no locks. The settings provider never throws (its contract), so reads here are
/// not guarded.
/// </summary>
public sealed class PartyScreenFastPathService
{
    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly IStampClock _clock;
    private readonly Dictionary<CodeKey, object> _codes = new Dictionary<CodeKey, object>();

    private bool _timing;
    private bool _reported;
    private long _initTicks;
    private long _refreshTicks;
    private int _lookups;
    private int _codeHits;
    private int _codeMisses;

    public PartyScreenFastPathService(IShadowfaxScreensSettingsProvider settings, IStampClock clock)
    {
        _settings = settings;
        _clock = clock;
    }

    public bool SessionOpen { get; private set; }

    /// <summary>Whether the session opened with the toggle on.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Opens a session for a new PartyVM: clears the cache and counters, snapshots the toggle.</summary>
    public void BeginScreen()
    {
        _codes.Clear();
        _lookups = _codeHits = _codeMisses = 0;
        _initTicks = _refreshTicks = 0;
        _reported = false;
        SessionOpen = true;
        Enabled = _settings.PartyFastOpen;
        _timing = _settings.LogScreenOpenTimes;
    }

    /// <summary>Ends the session and drops everything cached. Safe to call twice or with no session.</summary>
    public void EndScreen()
    {
        _codes.Clear();
        SessionOpen = false;
        Enabled = false;
    }

    public bool TryGetCode(object character, int type, int side, out object? code)
    {
        code = null;
        if (!SessionOpen) return false;
        _lookups++;
        if (Enabled && _codes.TryGetValue(new CodeKey(character, type, side), out code))
        {
            _codeHits++;
            return true;
        }
        _codeMisses++;
        return false;
    }

    public void StoreCode(object character, int type, int side, object? code)
    {
        if (!SessionOpen || !Enabled || code == null) return;
        _codes[new CodeKey(character, type, side)] = code;
    }

    /// <summary>Starts a phase clock; 0 when the timer is off, no session is open or the open was already reported.</summary>
    public long BeginPhase() => SessionOpen && _timing && !_reported ? _clock.Now : 0;

    public void EndPhase(PartyPhase phase, long start)
    {
        if (start == 0 || !SessionOpen || _reported) return;
        var elapsed = _clock.Now - start;
        if (phase == PartyPhase.InitializeTroopLists) _initTicks += elapsed;
        else _refreshTicks += elapsed;
    }

    /// <summary>
    /// The detail for the open line, starting with the fast path the open actually ran with. Later phases (a Reset, a
    /// later RefreshValues) are not added to it. <c>lookups</c> counts character-code lookups, not rows.
    /// </summary>
    public string BuildOpenDetail()
    {
        _reported = true;
        return string.Format(CultureInfo.InvariantCulture,
            "fast open {0}; lookups {1}, codes {2} hit/{3} miss, init {4:0.0} ms, refresh {5:0.0} ms",
            Enabled ? "on" : "off", _lookups, _codeHits, _codeMisses,
            LoadTimeStampLines.ToMs(_initTicks, _clock.Frequency), LoadTimeStampLines.ToMs(_refreshTicks, _clock.Frequency));
    }

    private readonly struct CodeKey : System.IEquatable<CodeKey>
    {
        private readonly object _character;
        private readonly int _type;
        private readonly int _side;

        public CodeKey(object character, int type, int side)
        {
            _character = character;
            _type = type;
            _side = side;
        }

        public bool Equals(CodeKey other) =>
            ReferenceEquals(_character, other._character) && _type == other._type && _side == other._side;

        public override bool Equals(object? obj) => obj is CodeKey other && Equals(other);

        public override int GetHashCode() =>
            (RuntimeHelpers.GetHashCode(_character) * 397 ^ _type) * 397 ^ _side;
    }
}
