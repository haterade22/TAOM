using TAOM.Adapters;

namespace TAOM.Features.BannerColorPersistence;

public interface IAgentColorStore
{
    void Register(int agentIndex, ClanColorInfo info);
    bool TryGetColors(int agentIndex, out ClanColorInfo info);
    void Clear();
}
