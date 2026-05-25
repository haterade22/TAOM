namespace TAOM.Features.CrashReport.Domain;

public sealed record AppDomainSnapshot(
    string FriendlyName,
    string BaseDirectory,
    string? RelativeSearchPath,
    bool IsFullyTrusted);

public sealed record EnvVarEntry(string Name, string Value);
