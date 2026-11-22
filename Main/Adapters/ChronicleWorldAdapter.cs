using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// Production <see cref="IChronicleWorldAdapter"/>. Members read here, checked against the installed v1.5.4
/// decompile: <c>CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow</c> (float), <c>CampaignTime.Now.ToHours</c>,
/// <c>Settlement.Find</c> (<c>MBObjectManager.GetObject</c>, null for an unknown id),
/// <c>Settlement.MapFaction</c> (computed: <c>SettlementComponent?.MapFaction</c>), <c>Settlement.SiegeEvent</c>,
/// <c>SiegeEvent.BesiegerCamp</c> (a field), <c>BesiegerCamp.LeaderParty</c>, <c>MobileParty.MapFaction</c>
/// (computed) and <c>Kingdom.All</c> (<c>Campaign.Current.Kingdoms</c>, so guarded on the campaign). Every
/// computed getter is reached through <c>?.</c> (adapters.md); the chronicle service catches anything that
/// still throws.
/// </summary>
public sealed class ChronicleWorldAdapter : IChronicleWorldAdapter
{
    /// <summary>NaN with no campaign, and a non-finite engine value passes through, so the chronicle's finite gate skips the step.</summary>
    public double GetElapsedDays() =>
        Campaign.Current?.Models?.CampaignTimeModel?.CampaignStartTime.ElapsedDaysUntilNow ?? double.NaN;

    public double GetNowHours() => Campaign.Current == null ? 0d : CampaignTime.Now.ToHours;

    public bool SettlementExists(string settlementId) => Find(settlementId) != null;

    public bool KingdomExists(string kingdomId)
    {
        if (Campaign.Current == null || string.IsNullOrEmpty(kingdomId))
            return false;
        foreach (var kingdom in Kingdom.All)
        {
            if (kingdom != null && kingdom.StringId == kingdomId)
                return true;
        }

        return false;
    }

    public string? GetOwnerKingdomId(string settlementId) => (Find(settlementId)?.MapFaction as Kingdom)?.StringId;

    public string? GetBesiegerKingdomId(string settlementId) =>
        (Find(settlementId)?.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction as Kingdom)?.StringId;

    private static Settlement? Find(string settlementId) =>
        Campaign.Current == null || string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);

    public bool IsHeroAlive(string heroId) => FindAliveHero(heroId) != null;

    public bool IsPlayerHero(string heroId) => FindAliveHero(heroId)?.IsHumanPlayerCharacter == true;

    // ApplyByBattle(Hero victim, Hero killer, bool showNotification = true) takes a null killer: the engine's own PartyAgentOrigin.SetKilled passes one
    // (PartyAgentOrigin.cs:193). ApplyInternal returns with only a death mark while the victim's party is in a map event or besieges (KillCharacterAction.cs:51);
    // the end of that battle completes the mark (MapEventSide.cs:431-437), else AgingCampaignBehavior.DailyTickHero does (:101-113). It returns with nothing
    // done for a hero who cannot die (:34, an issue or quest veto) and for the human player (:56). So the result is read back from the hero.
    public ChronicleHeroDeath KillHeroInBattle(string heroId)
    {
        var hero = FindAliveHero(heroId);
        if (hero == null || hero.IsHumanPlayerCharacter)
            return ChronicleHeroDeath.Refused;

        KillCharacterAction.ApplyByBattle(hero, null);
        if (!hero.IsAlive)
            return ChronicleHeroDeath.Killed;

        return hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None ? ChronicleHeroDeath.Deferred : ChronicleHeroDeath.Refused;
    }

    private static Hero? FindAliveHero(string heroId)
    {
        if (Campaign.Current == null || string.IsNullOrEmpty(heroId))
            return null;

        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero != null && hero.StringId == heroId)
                return hero;
        }

        return null;
    }
}
