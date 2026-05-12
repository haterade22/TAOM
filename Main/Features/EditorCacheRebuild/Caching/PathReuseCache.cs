using System.Collections.Concurrent;
using System.Threading;
using TaleWorlds.Library;

namespace TAOM.Features.EditorCacheRebuild.Caching;

public class PathReuseCache : IPathReuseCache
{
    private readonly ConcurrentDictionary<SortedPathKey, NavigationPath> _store = new();
    private int _hits;
    private int _misses;

    public int HitCount => _hits;
    public int MissCount => _misses;
    public int Count => _store.Count;

    public void Store(string id1, bool isPort1, string id2, bool isPort2, NavigationPath path)
    {
        var key = new SortedPathKey(id1, isPort1, id2, isPort2);
        _store[key] = NavigationPathCloner.Clone(path);
    }

    public bool TryGet(string id1, bool isPort1, string id2, bool isPort2, out NavigationPath path)
    {
        var key = new SortedPathKey(id1, isPort1, id2, isPort2);
        if (_store.TryGetValue(key, out path!))
        {
            Interlocked.Increment(ref _hits);
            return true;
        }
        Interlocked.Increment(ref _misses);
        path = null!;
        return false;
    }

    public void Clear()
    {
        _store.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }
}
