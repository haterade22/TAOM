using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// The boundary behind <see cref="IPatrolRosterHook"/>: reads the roster's stack counts and the party's fresh size limit,
/// lets <see cref="PatrolRosterService"/> decide the new counts, and rebuilds the roster with the same characters in the same
/// order. The limit comes from the model slot, not <c>PartyBase.PartySizeLimit</c>, which is cached per version and may be
/// stale on the spawn call and right after a Guard House upgrade.
/// </summary>
public sealed class PatrolRosterHook : IPatrolRosterHook
{
    private readonly PatrolRosterService _service;

    public PatrolRosterHook(PatrolRosterService service) => _service = service;

    public TroopRoster FitToLimit(MobileParty party, TroopRoster baseRoster)
    {
        if (party == null || !party.IsPatrolParty || party.PatrolPartyComponent.IsNaval)
            return baseRoster;

        var counts = new int[baseRoster.Count];
        for (var i = 0; i < counts.Length; i++)
            counts[i] = baseRoster.GetElementNumber(i);

        var limit = (int)Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(party.Party, party.IsCurrentlyAtSea).ResultNumber;
        var fitted = _service.Fit(counts, limit);
        if (ReferenceEquals(fitted, counts))
            return baseRoster;

        var roster = TroopRoster.CreateDummyTroopRoster();
        for (var i = 0; i < fitted.Length; i++)
        {
            if (fitted[i] > 0)
                roster.AddToCounts(baseRoster.GetCharacterAtIndex(i), fitted[i]);
        }

        return roster;
    }
}
