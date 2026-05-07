using System.Collections.Generic;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public interface IMessengerStateStore
{
    void Add(PendingMessenger messenger);
    bool Remove(string heroId);
    PendingMessenger Get(string heroId);
    bool Contains(string heroId);
    IReadOnlyCollection<PendingMessenger> GetAll();
    int Count { get; }
    void Clear();

    Dictionary<string, string> Serialize();
    void Deserialize(IReadOnlyDictionary<string, string> data);
}
