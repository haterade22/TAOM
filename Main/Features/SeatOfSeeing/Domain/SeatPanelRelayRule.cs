namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What the relay does with the panel's own view model methods for one frame's clicks (Marshal's Word stage M7).</summary>
internal enum SeatPanelAction
{
    None,

    /// <summary><c>OrderTroopItemVM.ExecuteAction()</c>: the vanilla card click. Control adds the company because vanilla reads the key itself.</summary>
    SelectCard,

    /// <summary><c>MissionOrderVM.TrySelectOrderSet(set)</c>.</summary>
    SelectOrderSet,

    /// <summary>The order's <c>ExecuteAction</c>, then the panel closes unless it is held open, as the number keys do.</summary>
    ExecuteOrderItem,

    /// <summary>The set's <c>ExecuteDeSelect()</c>: the Return order of a sub-menu.</summary>
    DeselectOrderSet,

    /// <summary><c>MissionOrderVM.OnEscape()</c>: vanilla's right button release.</summary>
    Escape,
}

/// <summary>What the relay did in one screen tick: nothing, a click handed to a view model method, or no way to read the widgets' view models.</summary>
internal enum SeatRelayResult
{
    Idle,
    Acted,
    SiteMissing,
}

/// <summary>
/// A button click is a press and a release on the same target (the view model, never the widget: a button's icon and label are widgets of
/// the same view model). The rule remembers the press; the release acts only when the pointer is still over that view model. A right
/// release over the panel is vanilla's Escape (<c>GauntletOrderUIHandler.cs:372-375</c>), but only when the rule saw that press go down over
/// the panel too: a release belongs to whoever saw its press, so a right press on the field that is let go over a button is the command mouse's alone.
/// </summary>
internal sealed class SeatPanelRelayRule
{
    private SeatPanelHit _pressed;
    private bool _rightPressedOver;

    /// <summary>
    /// One frame's edges and the target under the pointer. A press is taken first, so a press and a release in one frame act. A target
    /// whose view model could not be read never acts. <paramref name="returnsToSet"/> is true when the target is the Return order of a set.
    /// </summary>
    internal SeatPanelAction Step(bool leftPressed, bool leftReleased, bool rightReleased, SeatPanelHit over, bool returnsToSet, bool rightPressed = false)
    {
        if (leftPressed)
            _pressed = over;
        if (rightPressed)
            _rightPressedOver = over.Kind != SeatPanelKind.None;

        if (leftReleased)
        {
            var pressed = _pressed;
            _pressed = default;
            var action = Act(pressed, over, returnsToSet);
            if (action != SeatPanelAction.None)
                return action;
        }

        if (!rightReleased)
            return SeatPanelAction.None;

        var escape = _rightPressedOver && over.Kind != SeatPanelKind.None;
        _rightPressedOver = false;
        return escape ? SeatPanelAction.Escape : SeatPanelAction.None;
    }

    internal void Reset()
    {
        _pressed = default;
        _rightPressedOver = false;
    }

    private static SeatPanelAction Act(SeatPanelHit pressed, SeatPanelHit released, bool returnsToSet)
    {
        if (pressed.Kind == SeatPanelKind.None || pressed.Target == null || pressed.Kind != released.Kind || !ReferenceEquals(pressed.Target, released.Target))
            return SeatPanelAction.None;

        return pressed.Kind switch
        {
            SeatPanelKind.Card => SeatPanelAction.SelectCard,
            SeatPanelKind.OrderSet => SeatPanelAction.SelectOrderSet,
            SeatPanelKind.OrderItem => returnsToSet ? SeatPanelAction.DeselectOrderSet : SeatPanelAction.ExecuteOrderItem,
            _ => SeatPanelAction.None,
        };
    }
}

/// <summary>
/// When the click shield takes the mouse buttons from the scene (Seat build-spec 6.1). It holds only while the Seat shows its cursor for
/// an open panel, the player is not looking with the middle button, and the pointer is over a panel target; and it yields to any other
/// overlay that takes the mouse buttons, so the battle action bar and the other views keep their clicks.
/// </summary>
internal static class SeatPanelYieldRule
{
    internal static bool Holds(bool cursorShown, bool panelOpen, bool lookHeld, bool overTarget, bool anotherLayerClaims) =>
        cursorShown && panelOpen && !lookHeld && overTarget && !anotherLayerClaims;

    /// <summary>
    /// Whether a layer other than the shield counts as claiming the mouse: it is active and its mask includes the mouse buttons. The scene
    /// layer (mask All) and the order layer are never counted: the first is the ground the shield lets through, the second is the panel itself.
    /// </summary>
    internal static bool LayerMayClaim(bool active, bool maskHasMouseButtons, bool isScene, bool isOrder, bool isShield) =>
        active && maskHasMouseButtons && !isScene && !isOrder && !isShield;
}

/// <summary>
/// Where the Seat's zoom wheel comes from. The click shield takes the wheel with the mouse buttons over a panel button (so the scene layer is
/// not hit and vanilla's own mouse reads stay false), which leaves the scene layer's read of the wheel at 0 there. The raw wheel stands in
/// only while the shield is up and the scene layer, the shield or nothing has the focus, so a popup or a menu never zooms the camera.
/// </summary>
internal static class SeatWheelRule
{
    internal static float Read(float sceneWheel, float rawWheel, bool shieldUp, bool focusIsSceneShieldOrNone) =>
        sceneWheel != 0f || !shieldUp || !focusIsSceneShieldOrNone ? sceneWheel : rawWheel;
}
