using System;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// DTO for a single shielded exception. Used by SaveShield + FailedModsCatalog
/// to attribute failures to the culprit mod assembly (walked from the exception's
/// stack frames) and persist to disk for user diagnosis.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports BetaDeps.Foundation.FailureRecord.
/// </summary>
public sealed class FailureRecord
{
    public DateTime When { get; set; }
    public string Category { get; set; } = string.Empty;       // "SAVE-LOAD", "MISSION-INIT", etc.
    public string ExceptionType { get; set; } = string.Empty;  // typeof(ex).Name
    public string Message { get; set; } = string.Empty;        // ex.Message (truncated)
    public string OwnerType { get; set; } = string.Empty;      // method.DeclaringType.FullName
    public string OwnerMethod { get; set; } = string.Empty;    // method.Name
    public string CulpritAssembly { get; set; } = string.Empty; // walked from stack frames; first non-engine asm
    public string CulpritFrame { get; set; } = string.Empty;   // top non-engine stack frame description
}
