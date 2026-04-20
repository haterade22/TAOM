using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace TAOM.Features.Siege.Models;

public class TaomSiegeEventModel : DefaultSiegeEventModel
{
    private readonly ISiegeEngineAvailabilityService _availability;

    public TaomSiegeEventModel(ISiegeEngineAvailabilityService availability)
    {
        _availability = availability;
    }

    public override IEnumerable<SiegeEngineType> GetAvailableDefenderSiegeEngines(PartyBase party)
    {
        bool hasFirePerks =
            party.MobileParty.HasPerk(DefaultPerks.Engineering.Stonecutters, checkSecondaryRole: true)
         || party.MobileParty.HasPerk(DefaultPerks.Engineering.SiegeEngineer, checkSecondaryRole: true);

        foreach (var kind in _availability.GetDefenderEngines(hasFirePerks))
            yield return Resolve(kind);
    }

    private static SiegeEngineType Resolve(DefenderSiegeEngineKind kind) => kind switch
    {
        DefenderSiegeEngineKind.Ballista     => DefaultSiegeEngineTypes.Ballista,
        DefenderSiegeEngineKind.FireBallista => DefaultSiegeEngineTypes.FireBallista,
        DefenderSiegeEngineKind.Catapult     => DefaultSiegeEngineTypes.Catapult,
        DefenderSiegeEngineKind.FireCatapult => DefaultSiegeEngineTypes.FireCatapult,
        DefenderSiegeEngineKind.Trebuchet    => DefaultSiegeEngineTypes.Trebuchet,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
