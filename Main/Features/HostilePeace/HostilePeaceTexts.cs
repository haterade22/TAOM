using TaleWorlds.Localization;

namespace TAOM.Features.HostilePeace;

/// <summary>The player-facing words of the peacetime blocks. The village texts are the base game's own war texts, so they read
/// (and are translated) exactly as at war.</summary>
internal static class HostilePeaceTexts
{
    internal static TextObject NoTradeAgreement() =>
        new TextObject("{=taom_hostile_peace_no_trade}Your realms are enemies in all but war. They will not trade.");

    internal static TextObject NoCrossSideAlliance() =>
        new TextObject("{=taom_hostile_peace_no_alliance}Your realms stand on opposite sides of the war and cannot be allied.");

    internal static TextObject HostileVillageTrade() =>
        new TextObject("{=vYHVlydb}You cannot trade with a hostile village.");

    internal static TextObject HostileVillageRecruit() =>
        new TextObject("{=nHlqkdUC}You cannot recruit troops from a hostile village.");

    internal static TextObject HostileVillageWait() =>
        new TextObject("{=s7aas7L4}You cannot wait in a hostile village.");
}
