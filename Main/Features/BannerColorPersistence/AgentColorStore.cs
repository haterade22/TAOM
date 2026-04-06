using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.BannerColorPersistence;

public class AgentColorStore : IAgentColorStore
{
    private readonly Dictionary<int, ClanColorInfo> _colors = new();

    public void Register(int agentIndex, ClanColorInfo info) => _colors[agentIndex] = info;

    public bool TryGetColors(int agentIndex, out ClanColorInfo info) =>
        _colors.TryGetValue(agentIndex, out info);

    public void Clear() => _colors.Clear();
}
