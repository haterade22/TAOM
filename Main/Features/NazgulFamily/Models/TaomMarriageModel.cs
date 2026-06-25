using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.NazgulFamily.Models;

/// <summary>
/// Makes the Ringwraiths (Witch-King + Nazgûl) ineligible for marriage, so they never acquire a
/// spouse over campaign time — and therefore never have children — by overriding exactly the two
/// methods the engine's marriage paths consult. Their PREDEFINED family (vanilla <c>heroes.xml</c>
/// seeds the nine wraiths into a self-contained family graph) is removed at the data layer by
/// <c>characters/heroes.xslt</c>; this model blocks any FUTURE runtime marriage
/// (<c>RomanceCampaignBehavior</c> → <c>MarriageAction</c>, both gated by these methods), and TAOM's
/// initial child generation already excludes both wraith cultures (<c>mordor</c> + <c>dolguldur</c>).
/// Every non-wraith decision falls through to vanilla <see cref="DefaultMarriageModel"/>.
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
