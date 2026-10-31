using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// What the client may touch of a running Fords of War match (build-spec 4.1): the human commander's view, the four commands
/// the AI uses too, a name lookup, the event feed and the camera numbers. The director fills it at mission start and clears it
/// at mission end; the client never sees <see cref="FordsMatch"/> or the director. Every member runs on the main thread and
/// takes the director's gate, because the tower hit hook calls into the same match from an engine thread. Inactive (before
/// <c>Fill</c> and after <c>Clear</c>) every command is refused with <see cref="CommandResult.NotLive"/>.
/// </summary>
internal sealed class FordsSession
{
    private const int FeedLimit = 256;

    private readonly Queue<MatchEvent> _feed = new();
    private readonly Dictionary<string, string> _names = new();
    private object _gate = new();
    private FordsMatch? _match;
    private IFordsBattlefieldAdapter? _battlefield;
    private int _humanSlot = -1;
    private Vec3f _cameraStart;
    private Vec3f _cameraLookAt;
    private IReadOnlyList<Vec3f> _polygon = new List<Vec3f>();

    /// <summary>A Fords match is running.</summary>
    public bool IsActive
    {
        get
        {
            lock (_gate)
                return _match != null;
        }
    }

    /// <summary>Above the human side's keep.</summary>
    public Vec3f CameraStart
    {
        get
        {
            lock (_gate)
                return _cameraStart;
        }
    }

    /// <summary>Toward the enemy base.</summary>
    public Vec3f CameraLookAt
    {
        get
        {
            lock (_gate)
                return _cameraLookAt;
        }
    }

    /// <summary>The playable polygon, to keep the camera inside the scene.</summary>
    public IReadOnlyList<Vec3f> BoundaryPolygon
    {
        get
        {
            lock (_gate)
                return _polygon;
        }
    }

    public MatchView? GetView()
    {
        lock (_gate)
            return _match?.GetView(_humanSlot);
    }

    public CommandResult TryBuyPack(int lane, string offerId)
    {
        lock (_gate)
            return _match?.TryBuyPack(_humanSlot, lane, offerId) ?? CommandResult.NotLive;
    }

    public CommandResult TryBuyHero(int lane, string heroId)
    {
        lock (_gate)
            return _match?.TryBuyHero(_humanSlot, lane, heroId) ?? CommandResult.NotLive;
    }

    public CommandResult TryBuyback(string heroId)
    {
        lock (_gate)
            return _match?.TryBuyback(_humanSlot, heroId) ?? CommandResult.NotLive;
    }

    public CommandResult TrySetLaneOrder(int lane, LaneOrder order)
    {
        lock (_gate)
            return _match?.TrySetLaneOrder(_humanSlot, lane, order) ?? CommandResult.NotLive;
    }

    /// <summary>Adds gold to the human slot (the dev console's grant). Returns what was added; 0 while inactive.</summary>
    public int AddGold(int amount)
    {
        lock (_gate)
            return _match?.GrantGold(_humanSlot, amount) ?? 0;
    }

    /// <summary>The character's name, read once through the battlefield adapter and kept; the id while inactive or when unknown.</summary>
    public string DisplayName(string characterId)
    {
        IFordsBattlefieldAdapter? battlefield;
        lock (_gate)
        {
            if (_names.TryGetValue(characterId, out var known))
                return known;
            battlefield = _battlefield;
        }

        if (battlefield == null)
            return characterId;

        var name = battlefield.CharacterName(characterId);
        lock (_gate)
        {
            if (_battlefield == battlefield)
                _names[characterId] = name;
        }

        return name;
    }

    /// <summary>The events since the last call, oldest first.</summary>
    public IReadOnlyList<MatchEvent> DrainEvents()
    {
        lock (_gate)
        {
            var drained = new List<MatchEvent>(_feed);
            _feed.Clear();
            return drained;
        }
    }

    internal void Fill(object gate, FordsMatch match, int humanSlot, IFordsBattlefieldAdapter battlefield,
        Vec3f cameraStart, Vec3f cameraLookAt, IReadOnlyList<Vec3f> polygon)
    {
        _gate = gate;
        lock (_gate)
        {
            _match = match;
            _humanSlot = humanSlot;
            _battlefield = battlefield;
            _cameraStart = cameraStart;
            _cameraLookAt = cameraLookAt;
            _polygon = polygon;
            _feed.Clear();
            _names.Clear();
        }
    }

    /// <summary>Adds events to the feed, dropping the oldest past <see cref="FeedLimit"/>. Ignored while inactive.</summary>
    internal void PushEvents(IEnumerable<MatchEvent> events)
    {
        lock (_gate)
        {
            if (_match == null)
                return;

            foreach (var e in events)
            {
                _feed.Enqueue(e);
                if (_feed.Count > FeedLimit)
                    _feed.Dequeue();
            }
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _match = null;
            _battlefield = null;
            _humanSlot = -1;
            _cameraStart = default;
            _cameraLookAt = default;
            _polygon = new List<Vec3f>();
            _feed.Clear();
            _names.Clear();
        }
    }
}
