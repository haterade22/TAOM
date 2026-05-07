namespace TAOM.Features.EquipPresets.Models;

/// <summary>Outcome of <see cref="IEquipmentPresetService.SaveCurrent"/>.</summary>
public enum SaveOutcome
{
    Saved,
    MaxReached,
    Disabled,
    NoActiveHero,
    InvalidName,
}

/// <summary>Outcome of <see cref="IEquipmentPresetService.UpdatePreset"/>.</summary>
public enum UpdateOutcome
{
    Updated,
    NotFound,
    Disabled,
    NoActiveHero,
}

/// <summary>Outcome of <see cref="IEquipmentPresetService.DeletePreset"/>.</summary>
public enum DeleteOutcome
{
    Deleted,
    NotFound,
    Disabled,
}
