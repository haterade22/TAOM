using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>
/// The two panel click steps of the field trial (Marshal's Word stage M7, Seat build-spec 6.1 and 12). A step finds a panel button in the
/// order layer's widgets, injects a pointer click at its centre pixel through the relay's own entry (never an operating system click), and
/// reads the effect from what the panel's own view models write: the selected companies, the open sub-menu and the engine's own order event.
/// What the step cannot prove is the shield taking a real mouse click and the click's feel: those stay with a person.
/// </summary>
internal sealed partial class CommandTrialScript
{
    private IEnumerable<Verdict> PanelClickCard()
    {
        if (!Seated)
            yield return NotSeated();

        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        yield return Verdict.InMissionTick;
        _host.ClearSelectedCompanies();
        var card = _host.LocatePanelTarget(SeatPanelKind.Card);
        if (!card.Found)
        {
            yield return Verdict.InMissionTick;
            _host.ClosePanel(asPlayer: true);
            yield return Verdict.Failed("reason=no-card opened=" + Bit(opened) + " " + card.Detail + Standing());
        }

        yield return Verdict.InMissionTick;
        var clicked = _host.InjectPanelClick(card.PixelX, card.PixelY);
        var clickedAt = _now;
        while (_now - clickedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("click-settle");

        var only = _host.CardCompanyIsOnlySelected;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "opened=" + Bit(opened) + " clicked=" + clicked + " only_selected=" + Bit(only) + " " + card.Detail + Standing();
        yield return SeatTrialPanelRules.CardClickWorked(card.Found, clicked, only, _host.Faulted) && opened && Seated ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> PanelClickOrder()
    {
        if (!Seated)
            yield return NotSeated();

        var units = _host.ChooseCompany(largest: true);
        if (units == 0)
            yield return Verdict.Failed("reason=no-company");

        yield return Verdict.InMissionTick;
        _host.SelectChosenCompany();
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        var set = _host.LocatePanelTarget(SeatPanelKind.OrderSet);
        var setClicked = SeatRelayResult.Idle;
        if (set.Found)
        {
            yield return Verdict.InMissionTick;
            setClicked = _host.InjectPanelClick(set.PixelX, set.PixelY);
        }

        var setAt = _now;
        while (_now - setAt < PanelSettleSeconds)
            yield return Verdict.Waiting("sub-menu-settle");

        var setOpened = _host.OrderSetSelected;
        var item = setOpened ? _host.LocatePanelTarget(SeatPanelKind.OrderItem) : new SeatPanelProbe(false, 0f, 0f, "no-sub-menu");
        var itemClicked = SeatRelayResult.Idle;
        if (item.Found)
        {
            yield return Verdict.InMissionTick;
            itemClicked = _host.InjectPanelClick(item.PixelX, item.PixelY);
        }

        var itemAt = _now;
        while (_now - itemAt < PanelSettleSeconds)
            yield return Verdict.Waiting("order-settle");

        var seen = _host.OrdersSeen;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "units=" + units.ToString(CultureInfo.InvariantCulture) + " opened=" + Bit(opened) + " set=" + setClicked + " sub_menu=" + Bit(setOpened) + " order=" + itemClicked
            + " orders_seen=" + seen.ToString(CultureInfo.InvariantCulture) + " " + set.Detail + " " + item.Detail + Standing();
        var worked = SeatTrialPanelRules.OrderClickWorked(set.Found, setClicked, setOpened, item.Found, itemClicked, seen, _host.Faulted);
        yield return worked && opened && Seated ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
