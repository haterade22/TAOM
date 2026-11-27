using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The panel click half of the stand-in (Marshal's Word stage M7): a card, a set and an order that the injected click can reach, each fault switchable.</summary>
internal sealed partial class CommandTrialWorld
{
    internal bool PanelHasCard = true;
    internal bool PanelHasSet = true;
    internal bool PanelSetOpensItems = true;
    internal bool CardClickDoesNothing;
    internal bool CardClickSelectsTwo;
    internal bool SetClickDoesNothing;
    internal bool OrderClickDoesNothing;
    internal bool OrderClickRunsNoOrder;
    internal int Injections;

    private bool _cardOnlySelected;
    private bool _subMenuOpen;
    private SeatPanelKind _lastProbe;

    public int OrdersSeen { get; private set; }

    public bool OrderSetSelected => _subMenuOpen;

    public bool CardCompanyIsOnlySelected => _cardOnlySelected;

    public SeatPanelProbe LocatePanelTarget(SeatPanelKind kind)
    {
        _lastProbe = kind;
        var found = kind switch
        {
            SeatPanelKind.Card => PanelHasCard,
            SeatPanelKind.OrderSet => PanelHasSet,
            SeatPanelKind.OrderItem => _subMenuOpen && PanelSetOpensItems,
            _ => false,
        };
        return found ? new SeatPanelProbe(true, 300f, 400f, "scale=1 at=300,400") : new SeatPanelProbe(false, 0f, 0f, "not-found");
    }

    public SeatRelayResult InjectPanelClick(float pixelX, float pixelY)
    {
        Note("inject-panel-click");
        Injections++;
        Faulted |= FaultMidStep;
        if (!_panelIsOpen)
            return SeatRelayResult.Idle;

        switch (_lastProbe)
        {
            case SeatPanelKind.Card when !CardClickDoesNothing:
                _cardOnlySelected = !CardClickSelectsTwo;
                return SeatRelayResult.Acted;
            case SeatPanelKind.OrderSet when !SetClickDoesNothing:
                _subMenuOpen = PanelSetOpensItems;
                return SeatRelayResult.Acted;
            case SeatPanelKind.OrderItem when !OrderClickDoesNothing:
                if (!OrderClickRunsNoOrder)
                    OrdersSeen++;
                _subMenuOpen = false;
                return SeatRelayResult.Acted;
            default:
                return SeatRelayResult.Idle;
        }
    }

    public void ClearSelectedCompanies()
    {
        Note("clear-selected");
        _cardOnlySelected = false;
    }
}
