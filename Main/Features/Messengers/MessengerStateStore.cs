using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public class MessengerStateStore : IMessengerStateStore
{
    private readonly Dictionary<string, PendingMessenger> _messengers = new Dictionary<string, PendingMessenger>();
    private readonly IModLogger _logger;

    public MessengerStateStore(IModLogger logger)
    {
        _logger = logger;
    }

    public int Count => _messengers.Count;

    public void Add(PendingMessenger messenger)
    {
        if (messenger == null || string.IsNullOrEmpty(messenger.TargetHeroId))
            return;
        _messengers[messenger.TargetHeroId] = messenger;
    }

    public bool Remove(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return false;
        return _messengers.Remove(heroId);
    }

    public PendingMessenger Get(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        _messengers.TryGetValue(heroId, out var m);
        return m;
    }

    public bool Contains(string heroId) => !string.IsNullOrEmpty(heroId) && _messengers.ContainsKey(heroId);

    public IReadOnlyCollection<PendingMessenger> GetAll() => _messengers.Values;

    public void Clear() => _messengers.Clear();

    public Dictionary<string, string> Serialize()
    {
        var dict = new Dictionary<string, string>(_messengers.Count);
        foreach (var kvp in _messengers)
            dict[kvp.Key] = kvp.Value.Serialize();
        return dict;
    }

    public void Deserialize(IReadOnlyDictionary<string, string> data)
    {
        _messengers.Clear();
        if (data == null)
            return;

        foreach (var kvp in data)
        {
            if (PendingMessenger.TryDeserialize(kvp.Key, kvp.Value, out var messenger))
            {
                _messengers[kvp.Key] = messenger;
            }
            else
            {
                _logger?.LogWarning($"MessengerStateStore: dropped malformed entry for heroId={kvp.Key}");
            }
        }
    }
}
