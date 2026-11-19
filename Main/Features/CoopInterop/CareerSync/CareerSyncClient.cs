using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;

namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>
/// A co-op client's side of career sync (docs/features/coop-interop.md, "Career sync"). After the join is ready it
/// reports its own hero's career to the co-op server once (the join report), then once per action that changed it: a
/// change only marks the hero, and <see cref="Flush"/> (called every frame by the relay) sends one report, so a career
/// switch that clears, sets, stamps and takes the root goes out as one. What the server sends is written as it is,
/// except an answer for the own hero that is older than the last report or arrives while a newer change waits.
/// </summary>
public sealed class CareerSyncClient
{
    private readonly ICareerDataService _data;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passives;
    private readonly ICoopSessionProvider _coop;
    private readonly IPlayerPartyAdapter _player;
    private readonly ICareerSyncNotice _notice;
    private readonly IModLogger _logger;
    private ICareerSyncLink? _link;
    private bool _joined;
    private bool _dirty;
    private long _sequence;
    private long _lastSent;

    public CareerSyncClient(ICareerDataService data, ICareerRegistry registry, ICareerPassiveService passives, ICoopSessionProvider coop,
        IPlayerPartyAdapter player, ICareerSyncNotice notice, IModLogger logger)
    {
        _data = data;
        _registry = registry;
        _passives = passives;
        _coop = coop;
        _player = player;
        _notice = notice;
        _logger = logger;
        _data.HeroCareerChanged += OnChanged;
    }

    /// <summary>The relay attaches itself, or null to detach. A new link waits for the next join.</summary>
    public void Attach(ICareerSyncLink? link)
    {
        _link = link;
        _joined = false;
        _dirty = false;
    }

    /// <summary>This client's campaign join finished: it plays its own hero, and its career book holds the saved career.</summary>
    public void OnJoined()
    {
        _joined = true;
        _dirty = false;
        Send(joinReport: true);
    }

    /// <summary>Sends one report when the own hero's career changed since the last one.</summary>
    public void Flush()
    {
        if (!_dirty) return;

        _dirty = false;
        Send(joinReport: false);
    }

    public void Receive(CareerSyncSnapshot career, long sequence, bool refused, bool known)
    {
        if (career == null || string.IsNullOrEmpty(career.HeroId)) return;

        if (career.HeroId == _player.GetMainHeroId())
        {
            if (sequence < _lastSent || _dirty) return;
            // With no saved copy the career stays on this PC (a career picked before career sync is not wiped), but the
            // server's save has none, so it is gone at the next join: the player is told which case it is.
            if (refused && known) _notice.ShowRefused();
            else if (refused) _notice.ShowRefusedUnsaved();
        }

        if (!known) return;

        _data.ApplyHeroCareer(career.ToData());
        _passives.RefreshCache(_data, _registry);
    }

    private void OnChanged(string heroId)
    {
        if (_link == null || !_joined || !_coop.IsCoopClient) return;
        if (string.IsNullOrEmpty(heroId) || heroId != _player.GetMainHeroId()) return;

        _dirty = true;
    }

    private void Send(bool joinReport)
    {
        var link = _link;
        var heroId = _player.GetMainHeroId();
        if (link == null || !_coop.IsCoopClient || string.IsNullOrEmpty(heroId)) return;

        var career = CareerSyncSnapshot.Of(_data.GetOrCreateData(heroId));
        _lastSent = ++_sequence;
        if (!link.SendToHost(career, _lastSent, joinReport))
            _logger.LogWarning($"[CareerSync] could not send the career of '{heroId}' to the co-op server.");
    }
}
