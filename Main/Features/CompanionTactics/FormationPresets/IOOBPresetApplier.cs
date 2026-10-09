using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Reads the live Order of Battle into a preset (Save) and applies a preset back (Load). Boundary class: it
/// exposes the TaleWorlds VM type for the same reason <see cref="IOOBCaptainAutoAssigner"/> does. The mapping
/// itself is the pure <see cref="FormationPresetLayout"/>.
/// </summary>
public interface IOOBPresetApplier
{
    HoNFormationPreset Capture(OrderOfBattleVM vm, string name);

    PresetApplyResult Apply(OrderOfBattleVM vm, HoNFormationPreset preset);
}
