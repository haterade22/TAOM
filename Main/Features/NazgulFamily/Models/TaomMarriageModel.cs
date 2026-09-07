using TAOM.Features.MarriageAlignment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.NazgulFamily.Models;

/// <summary>
/// TAOM's single <see cref="MarriageModel"/>. It carries TWO independent rules, because the engine
/// resolves exactly one model of a given type (the backwards scan over registered models), so a
/// second <c>AddModel</c> would silently shadow whichever rule registered first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 1 — Ringwraiths.</b> The Witch-King + Nazgûl are ineligible for marriage, so they never
/// acquire a spouse over campaign time and therefore never have children. Their PREDEFINED family
/// (vanilla <c>heroes.xml</c> seeds the nine wraiths into a self-contained family graph) is removed
/// at the data layer by <c>characters/heroes.xslt</c>; this model blocks any FUTURE runtime marriage,
/// and TAOM's initial child generation already excludes both wraith cultures (<c>mordor</c> +
/// <c>dolguldur</c>).
/// </para>
/// <para>
/// <b>Rule 2 — alignment (#542).</b> A Free-aligned hero may not marry an Evil-aligned one, keyed on
/// culture via <see cref="IMarriageAlignmentService"/>. Vanilla has no such concept:
/// <c>RomanceCampaignBehavior.CheckNpcMarriages</c> draws the partner clan uniformly from
/// <c>Clan.All</c> and gates only on war and clan relation, which is how a Gondor lord came to marry
/// a Misty Mountain orc. The rule sits on <c>IsCoupleSuitableForMarriage</c> alone because that is
/// the single chokepoint every path funnels through: the AI daily tick (via
/// <c>NpcCoupleMarriageChance</c>), player courtship and the marriage barter (via
/// <c>MarriageCourtshipPossibility</c>), <c>MarriageOfferCampaignBehavior</c>, and
/// <c>MarriageAction.ApplyInternal</c>. <c>IsSuitableForMarriage</c> stays wraith-only: it is a
/// single-hero predicate and cannot express a pair rule.
/// </para>
/// Every other decision falls through to vanilla <see cref="DefaultMarriageModel"/>.
/// </remarks>
public sealed class TaomMarriageModel : DefaultMarriageModel
{
    private readonly INazgulRegistry _registry;
    private readonly IMarriageAlignmentService _marriageAlignment;

    public TaomMarriageModel(INazgulRegistry registry, IMarriageAlignmentService marriageAlignment)
    {
        _registry = registry;
        _marriageAlignment = marriageAlignment;
    }

    public override bool IsSuitableForMarriage(Hero maidenOrSuitor)
        => !IsWraith(maidenOrSuitor) && base.IsSuitableForMarriage(maidenOrSuitor);

    public override bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero)
        => !IsWraith(firstHero) && !IsWraith(secondHero)
           && !IsCrossAlignment(firstHero, secondHero)
           && base.IsCoupleSuitableForMarriage(firstHero, secondHero);

    private bool IsWraith(Hero hero) => hero != null && _registry.IsWraith(hero.StringId);

    // Boundary conversion (ADR-007): the sealed Hero stops here and only culture StringIds cross
    // into the service. "Involves the player clan" covers the player's siblings and children too,
    // not just Hero.MainHero, because the courtship dialogue proposes matches for the whole clan.
    private bool IsCrossAlignment(Hero firstHero, Hero secondHero)
    {
        if (firstHero == null || secondHero == null) return false;

        var involvesPlayerClan = firstHero.Clan == Clan.PlayerClan || secondHero.Clan == Clan.PlayerClan;

        return _marriageAlignment.IsMarriageBlocked(
            firstHero.Culture?.StringId,
            secondHero.Culture?.StringId,
            involvesPlayerClan);
    }
}
