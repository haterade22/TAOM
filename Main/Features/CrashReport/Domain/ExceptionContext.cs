using System;
using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

// Root aggregate for a single captured crash. Composed by CrashReportService
// from every collector + adapter. All fields are plain CLR types (no TaleWorlds
// types) per ADR-007. Designed for both PlainText rendering (clipboard + log) and
// JSON serialisation (Newtonsoft.Json) for the crash bundle ZIP.
public sealed record ExceptionContext(
    DateTime CapturedAtUtc,
    string CrashSignature,
    IdentitySnapshot Identity,
    ExceptionFrame? Exception,
    IReadOnlyList<StackFrameSnapshot> StackFrames,
    HarmonyCorrelationSnapshot Harmony,
    ModuleInventorySnapshot Modules,
    AssemblyInventorySnapshot Assemblies,
    CampaignSnapshot? Campaign,
    MissionSnapshot? Mission,
    TaomStateSnapshot Taom,
    McmSettingsSnapshot Mcm,
    ProcessSnapshot Process,
    // Nullable on purpose: null means the memory reader failed, and the renderer prints
    // "(unavailable)" rather than a fabricated 0. See SystemMemorySnapshot's remarks.
    SystemMemorySnapshot? SystemMemory,
    GpuSnapshot Gpu,
    DisplaySnapshot Display,
    OsSnapshot Os,
    AppDomainSnapshot AppDomain,
    IReadOnlyList<EnvVarEntry> EnvVars,
    FrameTimingSnapshot Performance,
    LogTailSnapshot Logs,
    IReadOnlyList<CollectorFailure> CollectorFailures);

// One per collector that threw while building its snapshot. Lets the renderer
// show "[unavailable: <reason>]" for the failed section without losing the report.
public sealed record CollectorFailure(
    string Collector,
    string ExceptionType,
    string Message);
