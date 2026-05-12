using TaleWorlds.Library;

namespace TAOM.Features.EditorCacheRebuild.Caching;

public interface IPathReuseCache
{
    int HitCount { get; }
    int MissCount { get; }
    int Count { get; }

    void Store(string id1, bool isPort1, string id2, bool isPort2, NavigationPath path);

    bool TryGet(string id1, bool isPort1, string id2, bool isPort2, out NavigationPath path);

    void Clear();
}
