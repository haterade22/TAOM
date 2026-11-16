using System;
using TaleWorlds.Localization;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>What a co-op guest reads when the host refuses a trade or cannot be asked (#1329).</summary>
public static class GuestTradeTexts
{
    public static string Refusal(GuestTradeRefusal refusal) => refusal switch
    {
        GuestTradeRefusal.NotFreeLeader => new TextObject("{=taom_guest_trade_not_leader}The host cannot trade for you while you do not lead a free party.").ToString(),
        GuestTradeRefusal.NotInSettlement => new TextObject("{=taom_guest_trade_not_here}The host cannot trade for you here: your party is not in this settlement, or it is in a battle or a siege.").ToString(),
        GuestTradeRefusal.NoEmissaryTrade => new TextObject("{=taom_emissary_no_resource}There is no emissary trade in this settlement.").ToString(),
        GuestTradeRefusal.NoArmoury => new TextObject("{=taom_guest_trade_no_armoury}The host finds no armourer at work in this town.").ToString(),
        _ => new TextObject("{=taom_guest_trade_failed}The host could not complete this. Try again.").ToString(),
    };

    /// <summary>
    /// Sends a guest's request; runs <paramref name="onHandled"/> when the host does it, and shows the refusal or the
    /// reason nothing was sent through <paramref name="showRefusal"/> otherwise.
    /// </summary>
    public static void AskHost(GuestTradeClient client, GuestTradeRequest request, Action<GuestTradeReply> onHandled,
        Action<string> showRefusal)
    {
        var sent = client.Request(request, reply =>
        {
            if (reply.Handled)
                onHandled(reply);
            else
                showRefusal(Refusal(reply.Refusal));
        });
        if (sent != GuestTradeSend.Sent)
            showRefusal(NotSent(sent));
    }

    public static string NotSent(GuestTradeSend send) => send switch
    {
        GuestTradeSend.Busy => new TextObject("{=taom_guest_trade_busy}The host has not answered your last request yet.").ToString(),
        GuestTradeSend.NoAnswer => new TextObject("{=taom_guest_trade_no_answer}The host did not answer your last request. Try again.").ToString(),
        _ => new TextObject("{=taom_guest_trade_no_link}The host cannot be reached.").ToString(),
    };
}
