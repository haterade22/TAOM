using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.NazgulFamily.Models;

/// <summary>
/// Makes the Ringwraiths (Witch-King + Nazgûl) ineligible for marriage, so they never acquire a
/// spouse over campaign time — and therefore never have children. Runtime marriage
/// (<c>RomanceCampaignBehavior</c> → <c>MarriageAction</c>) is the only family source for these
/// lords: their predefined family is stripped by <c>lords.xslt</c> (each lord is rebuilt with only
/// explicit attributes) and TAOM's initial child generation already excludes the <c>mordor</c>
/// culture. Every non-wraith decision falls through to vanilla <see cref="DefaultMarriageModel"/>.
/// </summary>
public sealed class TaomMarriageModel : DefaultMarriageModel
{
    private readonly INazgulRegistry _registry;

    public TaomMarriageModel(INazgulRegistry registry) => _registry = registry;

    public override bool IsSuitableForMarriage(Hero maidenOrSuitor)
        => !IsWraith(maidenOrSuitor) && base.IsSuitableForMarriage(maidenOrSuitor);

    public override bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero)
        => !IsWraith(firstHero) && !IsWraith(secondHero)
           && base.IsCoupleSuitableForMarriage(firstHero, secondHero);

    private bool IsWraith(Hero hero) => hero != null && _registry.IsWraith(hero.StringId);
}
