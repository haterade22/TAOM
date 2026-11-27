extern alias SNV;

using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Order;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;
using TaleWorlds.ScreenSystem;
using TAOM.Features.SeatOfSeeing.Domain;
using Vector2 = SNV::System.Numerics.Vector2;

namespace TAOM.Client.Adapters;

/// <summary>
/// Order panel clicks from the Seat (Marshal's Word stage M7, Seat build-spec 6.1). Vanilla's panel has no mouse path: both movies wrap
/// the visible panel in widgets that take no events (OrderBar.xml:5, OrderRadial.xml:5), and no item, set or card has a click command.
/// The relay finds the panel button under the pointer (<see cref="SeatPanelTarget"/> over the order layer's widgets), tells the cursor
/// layer to take the mouse buttons there (<see cref="ShieldHolds"/>), and turns a click on that button into the call the number keys
/// already make on the panel's own view models (<c>GauntletOrderUIHandler.TickInput</c>, v1.5.7 :446-480, :544-547). It writes no agent and
/// no formation by itself.
/// </summary>
internal sealed class SeatPanelRelay : ISeatPanelTree<Widget>
{
    // GauntletView._viewModel is private (v1.5.7 GauntletView.cs:18), the one way from a widget to the view model it is bound to. Cached
    // once. Missing: the shield falls back to the widget types and the relay is off (a fail line a mission, reflection-sites.md Category B).
    private static readonly FieldInfo? ViewModelField = AccessTools.Field(typeof(GauntletView), "_viewModel");

    // The order layer's name in MissionGauntletSingleplayerOrderUIHandler.cs:127 (v1.5.7).
    private const string OrderLayerName = "MissionOrder";

    private readonly MissionScreen _screen;
    private readonly Mission _mission;
    private readonly Func<MissionOrderVM?> _panel;
    private readonly Func<VisualOrderExecutionParameters?> _parameters;
    private readonly SeatPanelRelayRule _rule = new();
    private GauntletLayer? _orderLayer;
    private ScreenLayer? _shield;
    private bool _cursorOn;
    private bool _commandLive;
    private bool _lastHeld;
    private SeatPanelHit _lastHit;
    private bool _siteReported;

    internal SeatPanelRelay(MissionScreen screen, Mission mission, Func<MissionOrderVM?> panel, Func<VisualOrderExecutionParameters?> parameters)
    {
        _screen = screen;
        _mission = mission;
        _panel = panel;
        _parameters = parameters;
    }

    /// <summary>The cursor layer the relay reads the mouse edges from, and the layer the yield rule never counts.</summary>
    internal void Attach(ScreenLayer shield) => _shield = shield;

    /// <summary>The adapter's cursor switch: the shield holds only while the Seat shows its cursor.</summary>
    internal void SetCursor(bool shown)
    {
        _cursorOn = shown;
        if (!shown)
        {
            _rule.Reset();
            _lastHit = default;
        }
    }

    /// <summary>The command mouse is live: the shield covers the whole screen, not only the panel's buttons.</summary>
    internal void SetCommandLive(bool live) => _commandLive = live;

    // The middle button looks with the cursor hidden. The shield takes every mouse button, so the scene layer's own read of the middle
    // button is false under it: the raw input is the one read both sides agree on.
    internal static bool LookHeld() => Input.IsKeyDown(InputKey.MiddleMouseButton);

    /// <summary>The pointer is over a panel button as the shield last saw it: it is aiming at the button, not scrolling.</summary>
    internal bool PointerOverTarget => _lastHit.Kind != SeatPanelKind.None;

    /// <summary>The cursor layer's hit test, run once a frame by <c>ScreenManager.EarlyUpdate</c> on the main thread.</summary>
    internal bool ShieldHolds()
    {
        var held = ShieldHoldsNow();
        _lastHeld = held;
        return held;
    }

    private bool ShieldHoldsNow()
    {
        _lastHit = default;
        var holdsPanel = false;
        if (MayHold(out var root, out var pointer))
        {
            var hit = SeatPanelTarget.Topmost(this, root, pointer.X, pointer.Y);
            _lastHit = hit;
            holdsPanel = SeatPanelYieldRule.Holds(_cursorOn, true, false, hit.Kind != SeatPanelKind.None, hit.Kind != SeatPanelKind.None && AnotherLayerClaims());
        }

        // The command mouse takes every button wherever the pointer is, so neither the placer nor the panel's own tick sees one. The order layer
        // is looked up first, so the yield test never counts it as another overlay.
        return holdsPanel || (_commandLive && CommandHolds());
    }

    private bool CommandHolds()
    {
        OrderLayer();
        return SeatCommandRule.ShieldHolds(_cursorOn, _commandLive, LookHeld(), AnotherLayerClaims(), TransferActive(), ButtonTaken());
    }

    // The troop transfer popup: vanilla gives the order layer the whole mouse then (GauntletOrderUIHandler.cs:215-217), so nothing of ours holds.
    internal bool TransferActive() => _panel()?.TroopController.IsTransferActive ?? false;

    // A left or right button the shield held on the last frame is still down: a middle press now must not hand the scene layer a release it
    // never saw the press of.
    private bool ButtonTaken() => _lastHeld && (Input.IsKeyDown(InputKey.LeftMouseButton) || Input.IsKeyDown(InputKey.RightMouseButton));

    /// <summary>The cursor layer's hit test at a given point, which the engine asks for other layers' gamepad cursors.</summary>
    internal bool ShieldHoldsAt(float x, float y)
    {
        if (!MayHold(out var root, out _))
            return false;

        var hit = SeatPanelTarget.Topmost(this, root, x, y);
        return hit.Kind != SeatPanelKind.None && !AnotherLayerClaims();
    }

    // The order layer takes the mouse itself in the transfer popup and in a deployment (GauntletOrderUIHandler.cs:215-218).
    private bool MayHold(out Widget root, out Vector2 pointer)
    {
        root = null!;
        pointer = default;
        if (!_cursorOn || !_mission.IsOrderMenuOpen || _mission.Mode == MissionMode.Deployment || (LookHeld() && !ButtonTaken()))
            return false;

        var panel = _panel();
        if (panel == null || panel.TroopController.IsTransferActive)
            return false;

        var layer = OrderLayer();
        var context = layer?.UIContext;
        if (context == null)
            return false;

        root = context.Root;
        pointer = context.EventManager.MousePosition;
        return root != null;
    }

    private GauntletLayer? OrderLayer()
    {
        if (_orderLayer != null && !_orderLayer.IsFinalized)
            return _orderLayer;

        _orderLayer = null;
        foreach (var layer in _screen.Layers)
        {
            if (layer is GauntletLayer gauntlet && gauntlet.Name == OrderLayerName)
            {
                _orderLayer = gauntlet;
                break;
            }
        }

        return _orderLayer;
    }

    private bool AnotherLayerClaims()
    {
        foreach (var layer in _screen.Layers)
        {
            if (layer == null)
                continue;
            var claims = SeatPanelYieldRule.LayerMayClaim(layer.IsActive && !layer.IsFinalized, (layer.InputUsageMask & InputUsageMask.MouseButtons) != InputUsageMask.Invalid,
                layer == _screen.SceneLayer, layer == _orderLayer, layer == _shield);
            if (claims && layer.HitTest())
                return true;
        }

        return false;
    }

    public bool Shown(Widget node) => node.IsVisible;

    public bool Contains(Widget node, float x, float y) => node.IsPointInsideMeasuredArea(new Vector2(x, y));

    public int ChildCount(Widget node) => node.ChildCount;

    public Widget Child(Widget node, int index) => node.GetChild(index);

    public SeatPanelKind Classify(Widget node, out object? target)
    {
        target = null;
        if (ViewModelField == null)
            return node is OrderItemButtonWidget ? SeatPanelKind.OrderItem : node is OrderTroopItemBrushWidget ? SeatPanelKind.Card : SeatPanelKind.None;

        var view = node.GetComponent<GauntletView>();
        var model = view == null ? null : ViewModelField.GetValue(view);
        target = model;
        return model switch
        {
            OrderTroopItemVM => SeatPanelKind.Card,
            OrderSetVM => SeatPanelKind.OrderSet,
            OrderItemVM => SeatPanelKind.OrderItem,
            _ => SeatPanelKind.None,
        };
    }

    /// <summary>
    /// One screen tick's clicks, taken from the cursor layer's own input: it has the mouse buttons only on a frame its hit test was true, so
    /// an edge it reports is a click the shield took. Idle with no edge. SiteMissing once the view model of a widget cannot be read, or the
    /// panel's own view model cannot be reached, and nothing is relayed.
    /// </summary>
    internal SeatRelayResult Tick()
    {
        if (ViewModelField == null)
            return SiteMissing();

        var input = _shield?.Input;
        if (input == null)
            return SeatRelayResult.Idle;

        var pressed = input.IsKeyPressed(InputKey.LeftMouseButton);
        var released = input.IsKeyReleased(InputKey.LeftMouseButton);
        var rightPressed = input.IsKeyPressed(InputKey.RightMouseButton);
        var rightReleased = input.IsKeyReleased(InputKey.RightMouseButton);
        if (!pressed && !released && !rightPressed && !rightReleased)
        {
            if (!Input.IsKeyDown(InputKey.LeftMouseButton) && !Input.IsKeyDown(InputKey.RightMouseButton))
                _rule.Reset();
            return SeatRelayResult.Idle;
        }

        var hit = _lastHit;
        var action = _rule.Step(pressed, released, rightReleased, hit, hit.Target is OrderItemVM { Order: ReturnVisualOrder }, rightPressed);
        return action == SeatPanelAction.None ? SeatRelayResult.Idle : Perform(action, hit.Target);
    }

    private SeatRelayResult SiteMissing()
    {
        if (_siteReported)
            return SeatRelayResult.Idle;
        _siteReported = true;
        return SeatRelayResult.SiteMissing;
    }

    private SeatRelayResult Perform(SeatPanelAction action, object? target)
    {
        var panel = _panel();
        if (panel == null)
            return SiteMissing();

        switch (action)
        {
            // Control adds the company: OnSelectFormation reads LeftControl from the raw input itself (MissionOrderTroopControllerVM.cs:617).
            case SeatPanelAction.SelectCard when target is OrderTroopItemVM { IsValid: true, IsSelectable: true } card:
                card.ExecuteAction();
                return SeatRelayResult.Acted;

            case SeatPanelAction.SelectOrderSet when target is OrderSetVM set:
                return panel.TrySelectOrderSet(set) ? SeatRelayResult.Acted : SeatRelayResult.Idle;

            // No IsActive test: OrderItemVM.OnRefreshState sets IsActive = (activeState == OrderState.Active), the state already in force for
            // every selected formation, and the number keys run an order whatever its look (GauntletOrderUIHandler.cs:466-471).
            case SeatPanelAction.ExecuteOrderItem when target is OrderItemVM item:
                return ExecuteItem(panel, item);

            case SeatPanelAction.DeselectOrderSet:
                panel.SelectedOrderSet?.ExecuteDeSelect();
                return SeatRelayResult.Acted;

            case SeatPanelAction.Escape:
                panel.OnEscape();
                return SeatRelayResult.Acted;

            default:
                return SeatRelayResult.Idle;
        }
    }

    // The number keys' own sequence (GauntletOrderUIHandler.cs:466-479): execute with the handler's own parameters (its focused formation and
    // the order flag's position, the same arguments MissionOrderVM.TrySelectOrderSet gets, :892), then deselect the set while deploying or
    // holding, else close the panel. No parameters (the handler's method is missing): nothing runs, as a site fail.
    private SeatRelayResult ExecuteItem(MissionOrderVM panel, OrderItemVM item)
    {
        var parameters = _parameters();
        if (parameters == null)
            return SiteMissing();

        item.ExecuteAction(parameters.Value);
        if (_mission.Mode == MissionMode.Deployment || panel.IsHolding)
            panel.SelectedOrderSet?.ExecuteDeSelect();
        else
            panel.TryCloseToggleOrder();
        return SeatRelayResult.Acted;
    }

    /// <summary>The centre of the first shown panel widget of a kind a click acts on, as a pixel the injected click can use; false when there is none. An order set is one that opens a sub-menu (a solo set would run its order at once).</summary>
    internal bool TryLocate(SeatPanelKind kind, out float x, out float y, out object? target, out string detail)
    {
        x = y = 0f;
        target = null;
        detail = "no-order-layer";
        var root = OrderLayer()?.UIContext?.Root;
        if (root == null || !Shown(root))
            return false;

        detail = "not-found";
        var widget = kind == SeatPanelKind.OrderItem ? FirstOfKind(root, kind, true, out target) : null;
        widget ??= FirstOfKind(root, kind, false, out target);
        if (widget == null)
            return false;
        var inForce = target is OrderItemVM { IsActive: true };

        var context = OrderLayer()!.UIContext;
        var centre = widget.GlobalPosition + widget.Size * 0.5f;
        var usable = new Vector2(context.EventManager.LeftUsableAreaStart, context.EventManager.TopUsableAreaStart);
        foreach (var candidate in new[] { centre, centre + usable })
        {
            if (!widget.IsPointInsideMeasuredArea(candidate))
                continue;
            x = candidate.X;
            y = candidate.Y;
            detail = (kind == SeatPanelKind.OrderItem ? "in_force=" + (inForce ? "1" : "0") + " " : "") + "scale=" + context.ScaleModifier.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " at=" + (int)x + "," + (int)y;
            return true;
        }

        detail = "centre-outside-area";
        return false;
    }

    private Widget? FirstOfKind(Widget node, SeatPanelKind kind, bool notInForce, out object? target)
    {
        target = null;
        if (!Shown(node))
            return null;
        if (Classify(node, out target) == kind && Clickable(target) && (!notInForce || target is OrderItemVM { IsActive: false }) && (kind != SeatPanelKind.OrderSet || target is OrderSetVM { HasSingleOrder: false })
            && node.Size.X > 1f && node.Size.Y > 1f)
            return node;

        for (var i = 0; i < node.ChildCount; i++)
        {
            var found = FirstOfKind(node.GetChild(i), kind, notInForce, out target);
            if (found != null)
                return found;
        }

        target = null;
        return null;
    }

    private static bool Clickable(object? target) => target switch
    {
        OrderTroopItemVM card => card.IsValid && card.IsSelectable,
        OrderItemVM item => item.Order is not ReturnVisualOrder,
        OrderSetVM => true,
        _ => false,
    };

    /// <summary>A press and a release at a pixel, through the same decision and the same view model calls as a real click. Idle when no panel button is there.</summary>
    internal SeatRelayResult InjectClick(float x, float y)
    {
        if (ViewModelField == null)
            return SiteMissing();

        var root = OrderLayer()?.UIContext?.Root;
        if (root == null)
            return SeatRelayResult.Idle;

        var hit = SeatPanelTarget.Topmost(this, root, x, y);
        var injected = new SeatPanelRelayRule();
        var action = injected.Step(true, true, false, hit, hit.Target is OrderItemVM { Order: ReturnVisualOrder });
        return action == SeatPanelAction.None ? SeatRelayResult.Idle : Perform(action, hit.Target);
    }
}
