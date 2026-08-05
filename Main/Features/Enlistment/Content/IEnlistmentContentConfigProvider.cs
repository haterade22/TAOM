using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// Loads + validates the two enlistment content files under ModuleData/enlistment/:
/// enlistment_config.json (thresholds, tables, weights) and enlistment_duties.json (the
/// duty/incident content rows). Reuse.Singleton — edits require a full game restart.
/// Config-file failures revert per-field to compiled defaults; duty rows are validated
/// individually and SKIPPED with a warning on any unknown string-branch value (the M1
/// trap: a typo must never silently take a default path).
/// </summary>
public interface IEnlistmentContentConfigProvider
{
    EnlistmentContentConfig GetConfig();

    EnlistmentDutiesConfig GetDuties();
}
