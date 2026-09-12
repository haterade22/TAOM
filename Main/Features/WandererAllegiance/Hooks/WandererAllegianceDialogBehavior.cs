using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.WandererAllegiance.Hooks;

/// <summary>
/// Thin dialogue registration (ADR-002) for a wanderer refusing to be hired across the Free/Evil
/// line (#575). Vanilla's hire is one player line into the <c>companion_hire</c> token
/// (<c>LordConversationsCampaignBehavior</c> :797) answered by one NPC line at the default priority
/// 100 (:820, whose condition only sets text variables and always returns true).
/// <c>ConversationManager.GetSentenceOptions</c> returns the FIRST NPC line on the active token whose
/// condition is true, walking the list sorted priority-descending, so the two lines below, at 110
/// and each gated on the verdict, pre-empt vanilla's reply exactly when a refusal applies and are
/// invisible otherwise. Vanilla's player line is untouched, so vanilla's own gates (a wanderer, not
/// already a companion, no party, not a prisoner) still run, and there is no Harmony patch.
///
/// Both lines return to <c>lord_pretalk</c>, the same exit vanilla uses for its own "no deal" rows
/// (:821 capacity full, :823 cannot afford); <c>hero_pretalk_2</c> (:767) says "Is there anything
/// else?" and loops back to <c>hero_main_options</c>. No gold moves and nothing is written, which is
/// why this has no co-op authority gate: a refusal is a rule every peer should see.
/// </summary>
public class WandererAllegianceDialogBehavior : CampaignBehaviorBase
{
    private const int Priority = 110; // above vanilla's companion_hire reply at the default 100

    private readonly IWandererAllegianceService _service;
    private readonly IModLogger _logger;

    public WandererAllegianceDialogBehavior(IWandererAllegianceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddDialogLine(
            "taom_wa_refuse_free",
            "companion_hire",
            "lord_pretalk",
            "{=taom_wa_refuse_free}No. I know whose war you fight, and I will not carry a blade for the Shadow, not for any price. Find another sword.",
            () => Verdict() == WandererHireVerdict.RefusedByFreeWanderer,
            null,
            Priority);

        starter.AddDialogLine(
            "taom_wa_refuse_evil",
            "companion_hire",
            "lord_pretalk",
            "{=taom_wa_refuse_evil}You march with the Elf-friends. My own people would have my head for taking your coin, and yours would sooner see me hanged than armed. No.",
            () => Verdict() == WandererHireVerdict.RefusedByEvilWanderer,
            null,
            Priority);
    }

    // Boundary conversion only: four ids into the pure service. Read live on every call, never
    // cached, because Player Switcher changes MainHero and PlayerClan mid-session. Hero.Culture is a
    // plain field and Clan.Culture / Clan.Kingdom are bare auto-property reads, so the ?. chains
    // cannot trip a computed getter; Hero.MainHero and Clan.PlayerClan ARE computed statics, which is
    // what the try/catch is for. A throw defers to vanilla hiring, safe here because nothing has
    // been mutated.
    private WandererHireVerdict Verdict()
    {
        try
        {
            var wanderer = Hero.OneToOneConversationHero;
            var playerClan = Clan.PlayerClan;
            return _service.Evaluate(
                wanderer?.StringId,
                wanderer?.Culture?.StringId,
                playerClan?.Kingdom?.StringId,
                playerClan?.Culture?.StringId ?? Hero.MainHero?.Culture?.StringId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[WandererAllegiance] could not evaluate the hire verdict, deferring to vanilla hiring: {ex}");
            return WandererHireVerdict.Allowed;
        }
    }
}
