using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

// Reflected snapshot of every MCM-registered settings provider (TAOM + third-party).
// Nested groups are flattened with dotted property paths.
public sealed record McmSettingsSnapshot(
    IReadOnlyList<McmProviderSnapshot> Providers);

public sealed record McmProviderSnapshot(
    string ProviderId,
    string DisplayName,
    string AssemblyName,
    IReadOnlyList<McmSettingEntry> Settings);

public sealed record McmSettingEntry(
    string PropertyPath,
    string ValueAsString,
    string TypeName);
