namespace TAOM.Features.EditorCacheRebuild.Caching;

public interface IPersistentPathCache
{
    bool TryLoad(string filePath, uint expectedSceneCrc, uint expectedNavMeshCrc, IPathReuseCache target);

    void Save(string filePath, uint sceneCrc, uint navMeshCrc, IPathReuseCache source);
}
