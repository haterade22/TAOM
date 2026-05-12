using DryIoc;
using TAOM.Features.EditorCacheRebuild.Caching;
using TAOM.Features.EditorCacheRebuild.Checkpoint;
using TAOM.Features.EditorCacheRebuild.Diff;
using TAOM.Features.EditorCacheRebuild.Phase1;
using TAOM.Features.EditorCacheRebuild.Phase2;
using TAOM.Features.EditorCacheRebuild.Validation;

namespace TAOM.Features.EditorCacheRebuild;

public static class EditorCacheRebuildIoC
{
    public static void RegisterEditorCacheRebuildFeature(IContainer container)
    {
        container.Register<ICacheRebuildConfigProvider, CacheRebuildConfigProvider>(Reuse.Singleton);
        container.Register<IPathReuseCache, PathReuseCache>(Reuse.Singleton);
        container.Register<IPersistentPathCache, PersistentPathCache>(Reuse.Singleton);
        container.Register<SerialPhase1Builder>(Reuse.Singleton);
        container.Register<ParallelPhase1Builder>(Reuse.Singleton);
        container.Register<SerialPhase2Builder>(Reuse.Singleton);
        container.Register<ParallelPhase2Builder>(Reuse.Singleton);
        container.Register<ISmokeTestGate, SmokeTestGate>(Reuse.Singleton);
        container.Register<IValidationReportWriter, ValidationReportWriter>(Reuse.Singleton);
        container.Register<ICheckpointSerializer, CheckpointSerializer>(Reuse.Singleton);
        container.Register<ISettlementSnapshotStore, SettlementSnapshotStore>(Reuse.Singleton);
        container.Register<ISettlementDiffer, SettlementDiffer>(Reuse.Singleton);
        container.Register<IDistanceCacheBuilderService, CacheBuilderService>(Reuse.Singleton);
        container.Register<IRuntimeCacheRebuildService, RuntimeCacheRebuildService>(Reuse.Singleton);
    }
}
