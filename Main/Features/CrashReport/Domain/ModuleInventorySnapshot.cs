using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

public sealed record ModuleInventorySnapshot(
    IReadOnlyList<ModuleSnapshot> Modules);

public sealed record ModuleSnapshot(
    string Id,
    string Version,
    int LoadIndex,
    bool IsOfficial,
    string MainDllSha1,
    string? MainDllPath,
    string? ManifestPath,
    IReadOnlyList<string> DeclaredDependencies,
    bool HasDependencyOrderInversion,
    IReadOnlyList<string> XmlFilePaths);
