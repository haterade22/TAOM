using TAOM.Core.Validation;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// Reads the fief-grant knobs off MCM (#458). Standard TAOM recipe: <c>TaomSettings.Instance?.Knob</c>
/// so the compiled default applies when MCM is not loaded, then <see cref="SettingClamp"/> for the
/// null-safe, NaN-safe clamp to the knob's valid range.
///
/// The ranges here MUST match the MCM attribute ranges in <c>TaomSettings</c>. They are duplicated on
/// purpose rather than shared: MCM's attributes take compile-time constants, so the clamp is the only
/// place a hand-edited settings JSON gets checked at all.
/// </summary>
public sealed class FiefGrantSettingsProvider : IFiefGrantSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableFiefGrantRebalance ?? true;

    public float CapturerBonus =>
        SettingClamp.Clamp(TaomSettings.Instance?.FiefGrantCapturerBonus, 2.5f, 1.0f, 5.0f);

    public float LandlessBonus =>
        SettingClamp.Clamp(TaomSettings.Instance?.FiefGrantLandlessBonus, 2.0f, 1.0f, 5.0f);

    public float ConcentrationPenalty =>
        SettingClamp.Clamp(TaomSettings.Instance?.FiefGrantConcentrationPenalty, 0.35f, 0.0f, 1.0f);

    public float CultureMatchBonus =>
        SettingClamp.Clamp(TaomSettings.Instance?.FiefGrantCultureMatchBonus, 1.5f, 1.0f, 3.0f);

    public float CultureMismatchPenalty =>
        SettingClamp.Clamp(TaomSettings.Instance?.FiefGrantCultureMismatchPenalty, 0.6f, 0.1f, 1.0f);

    public float RulingClanFactor =>
        SettingClamp.Clamp(TaomSettings.Instance?.FiefGrantRulingClanFactor, 0.75f, 0.1f, 2.0f);

    public float KingsVoteFiefShareCap =>
        SettingClamp.Clamp(TaomSettings.Instance?.FiefGrantKingsVoteFiefShareCap, 0.34f, 0.0f, 1.0f);

    public bool ApplyPenaltiesToPlayerClan =>
        TaomSettings.Instance?.FiefGrantApplyPenaltiesToPlayerClan ?? false;
}
