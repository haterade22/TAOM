using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Holds a weak-ish reference to the currently-active <c>OrderOfBattleVM</c>. The reference
/// is captured by the OOBVM ctor postfix and cleared by OnFinalize. Boundary class — exposes
/// a sealed TaleWorlds VM type because the consumers (boundary patches, overlay service) all
/// live at the boundary layer; services do not see this interface.
/// </summary>
public interface IOrderOfBattleVMTracker
{
    OrderOfBattleVM Current { get; }
    void SetCurrent(OrderOfBattleVM vm);
    void ClearCurrent();
}
