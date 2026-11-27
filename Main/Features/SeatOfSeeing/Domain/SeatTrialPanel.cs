namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// What the field trial's lookup found on the order panel (Marshal's Word stage M7): the pixel at the centre of the first panel widget of
/// a kind that a click acts on, and the detail the step prints (the UI scale and the pixel, or why nothing was found).
/// </summary>
internal readonly record struct SeatPanelProbe(bool Found, float PixelX, float PixelY, string Detail);

/// <summary>The verdicts of the two panel click steps: each reads an effect the panel's own view models write, never the relay's own count.</summary>
internal static class SeatTrialPanelRules
{
    /// <summary>
    /// A click on a troop card selects its company and no other: the relay acted, and the one selected company is the card's.
    /// </summary>
    internal static bool CardClickWorked(bool cardFound, SeatRelayResult clicked, bool onlyThatCompanySelected, bool faulted) =>
        cardFound && clicked == SeatRelayResult.Acted && onlyThatCompanySelected && !faulted;

    /// <summary>
    /// Two clicks run an order: the first on an order set opens its sub-menu, the second on one of its orders makes the panel's own
    /// <c>OrderItemVM.OnExecuteOrder</c> event fire at least once (a count taken from the event, which only the view model raises).
    /// </summary>
    internal static bool OrderClickWorked(bool setFound, SeatRelayResult setClicked, bool setOpened, bool itemFound, SeatRelayResult itemClicked, int ordersSeen,
        bool faulted) =>
        setFound && setClicked == SeatRelayResult.Acted && setOpened && itemFound && itemClicked == SeatRelayResult.Acted && ordersSeen >= 1 && !faulted;
}
