using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.BattleCorpses;

/// <summary>
/// The player-facing half of <see cref="BattleSettingsAdvisor"/> (#701): one inquiry on the first main
/// menu of a session when the ragdoll or corpse option is above the recommendation, and the result
/// message for both the inquiry and the MCM button. Nothing is written unless the player clicks Apply.
/// </summary>
public static class BattleSettingsAdviceNotifier
{
    public static void OfferIfRisky(BattleSettingsAdvisor advisor, IModLogger logger)
    {
        try
        {
            if (!advisor.ShouldOffer()) return;

            InformationManager.ShowInquiry(new InquiryData(
                titleText: new TextObject("{=taom_bc_advice_title}TAOM: battle performance settings").ToString(),
                text: WithVanillaLabels(new TextObject("{=taom_bc_advice_body}Your {RAGDOLLS} or {CORPSES} option is set higher than TAOM recommends. Players have reported freezes in large battles at high values, and lowering these two options stopped them. Recommended: {RAGDOLLS} 5, {CORPSES} {LOW}. A setting already lower is kept. Apply now? You can change them back at any time in Options, {PERFORMANCE}. This notice can be turned off in Mod Options, TAOM, Performance.")).ToString(),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: true,
                affirmativeText: new TextObject("{=taom_bc_advice_apply}Apply").ToString(),
                negativeText: new TextObject("{=taom_bc_advice_keep}Keep mine").ToString(),
                affirmativeAction: () => Apply(advisor, logger),
                negativeAction: () => { }),
                pauseGameActiveState: false);
        }
        catch (Exception ex)
        {
            // Never block the main menu over an advisory notice.
            logger.LogWarning($"[BattleCorpses] settings advice not shown: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void Apply(BattleSettingsAdvisor advisor, IModLogger logger)
    {
        bool saved;
        try
        {
            saved = advisor.ApplyRecommended();
        }
        catch (Exception ex)
        {
            logger.LogError($"[BattleCorpses] applying the recommended options failed: {ex.GetType().Name}: {ex.Message}");
            saved = false;
        }

        logger.LogInfo($"[BattleCorpses] recommended ragdoll and corpse options {(saved ? "saved" : "NOT saved")}");
        InformationManager.DisplayMessage(saved
            ? new InformationMessage(new TextObject("{=taom_bc_advice_done}Battle settings saved.").ToString(), Colors.Green)
            : new InformationMessage(WithVanillaLabels(new TextObject("{=taom_bc_advice_failed}Battle settings could not be saved. Set them by hand in Options, {PERFORMANCE}.")).ToString(), Colors.Red));
    }

    // Vanilla's own keys (Native global_strings.xml), so every language names the options exactly as its
    // Options screen does.
    private static TextObject WithVanillaLabels(TextObject text) => text
        .SetTextVariable("RAGDOLLS", new TextObject("{=1awQTVqN}Number Of Ragdolls"))
        .SetTextVariable("CORPSES", new TextObject("{=h6wUbray}Number Of Corpses"))
        .SetTextVariable("LOW", new TextObject("{=hdLs35aB}Low (25)"))
        .SetTextVariable("PERFORMANCE", new TextObject("{=fM9E7frB}Performance"));
}
