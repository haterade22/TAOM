using System.Collections.Generic;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.CompanionTactics.Roles;

/// <summary>
/// Boundary decorator that mutates sealed VM types to show role indicators. Each method
/// is no-op when the VM doesn't represent a player-clan/companion hero. Encapsulates ALL
/// reflection (TypeIconData property write) so the Harmony postfixes stay thin.
/// </summary>
public interface IRoleTooltipDecorator
{
    void DecoratePartyCharacter(PartyCharacterVM vm);
    void DecorateOOBHeroItem(OrderOfBattleHeroItemVM vm);
    void AppendRoleToCaptainTooltip(OrderOfBattleHeroItemVM vm, List<TooltipProperty> tooltip);
}
