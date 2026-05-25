using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

public sealed record ProcessSnapshot(
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long GcTotalMemoryBytes,
    int Gen0CollectionCount,
    int Gen1CollectionCount,
    int Gen2CollectionCount,
    int ProcessThreadCount,
    int ManagedThreadCount,
    double UptimeSeconds,
    ThrowingThreadSnapshot ThrowingThread);

public sealed record ThrowingThreadSnapshot(
    int ManagedThreadId,
    string? Name,
    bool IsBackground,
    string ApartmentState);
