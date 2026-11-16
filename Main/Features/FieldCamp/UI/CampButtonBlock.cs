using TaleWorlds.Localization;
using TAOM.Features.FieldCamp.Activities.Hooks;

namespace TAOM.Features.FieldCamp.UI;

/// <summary>Why the Make Camp button cannot open the camp menu right now; None means it can.</summary>
public enum CampButtonBlock
{
    None = 0,
    InSettlement = 1,
    InEncounter = 2,
    MenuOpen = 3,
    Moving = 4,
    Disorganized = 5,
}

/// <summary>
/// Picks the one reason to tell the player when the button is blocked, and words the tooltip line. Pure: reads
/// <see cref="ICampMenuActivationQuery"/> only. The click (<c>FieldCampOverlayVM.MayOpenCampMenu</c>) asks the same
/// <see cref="Pick"/>, so the tooltip and the click cannot drift apart.
///
/// <para>Order: the most specific cause first. Inside a settlement or an encounter a game menu is also open, so
/// those come before "a menu is open"; the player gets the cause, not the symptom. Moving comes before
/// disorganized because a party that is still moving cannot camp whatever else holds.</para>
/// </summary>
internal static class CampButtonBlocks
{
    public static CampButtonBlock Pick(ICampMenuActivationQuery activation)
    {
        if (activation.IsMainPartyInSettlement) return CampButtonBlock.InSettlement;
        if (activation.IsMainPartyInEncounter) return CampButtonBlock.InEncounter;
        if (!activation.IsMapScreenClear) return CampButtonBlock.MenuOpen;
        if (!activation.IsMainPartyStationary) return CampButtonBlock.Moving;
        if (activation.IsMainPartyDisorganized) return CampButtonBlock.Disorganized;
        return CampButtonBlock.None;
    }

    /// <summary>The tooltip: the caption, plus a second line naming the reason while the button is blocked.</summary>
    public static string Tooltip(string caption, CampButtonBlock block)
    {
        if (block == CampButtonBlock.None)
            return caption;

        var line = new TextObject("{=taom_fcamp_btn_blocked}Cannot camp now: {REASON}")
            .SetTextVariable("REASON", Reason(block)).ToString();
        return caption + "\n" + line;
    }

    private static TextObject Reason(CampButtonBlock block)
    {
        switch (block)
        {
            case CampButtonBlock.InSettlement:
                // Same words the new-campaign start message uses.
                return CampRestStartTexts.Why(CampBlockReason.InSettlement);
            case CampButtonBlock.Moving:
                return CampRestStartTexts.Why(CampBlockReason.Moving);
            case CampButtonBlock.InEncounter:
                return new TextObject("{=taom_fcamp_block_encounter}the party is in an encounter.");
            case CampButtonBlock.MenuOpen:
                return new TextObject("{=taom_fcamp_block_menu}a menu or window is open; close it first.");
            default:
                return new TextObject("{=taom_fcamp_block_disorganized}the party is still disorganized; wait a few hours.");
        }
    }
}
