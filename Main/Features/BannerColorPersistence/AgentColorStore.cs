using System.Collections.Concurrent;
using TAOM.Adapters;

namespace TAOM.Features.BannerColorPersistence;

public class AgentColorStore : IAgentColorStore
{
    private readonly ConcurrentDictionary<int, ClanColorInfo> _colors = new();

    public void Register(int agentIndex, ClanColorInfo info) =>
        _colors.AddOrUpdate(agentIndex, info, (_, __) => info);

    public bool TryGetColors(int agentIndex, out ClanColorInfo info) =>
        _colors.TryGetValue(agentIndex, out info);

    public void Clear() => _colors.Clear();
}
