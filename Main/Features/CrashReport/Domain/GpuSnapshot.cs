using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

public sealed record GpuSnapshot(
    IReadOnlyList<GpuAdapterEntry> Adapters);

public sealed record GpuAdapterEntry(
    string Name,
    string? DriverVersion,
    string? DriverDate,
    long? AdapterRamBytes,
    string? VideoProcessor);
