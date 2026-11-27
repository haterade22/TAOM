extern alias SNV;

using System;
using TaleWorlds.Engine.GauntletUI;
using Vector2 = SNV::System.Numerics.Vector2;

namespace TAOM.Client.Adapters;

/// <summary>
/// The Seat's cursor layer, which also stands as the click shield over the order panel's buttons (Marshal's Word stage M7, Seat
/// build-spec 6.1). Vanilla wraps the panel in widgets that take no events, and the order layer's mask stays <c>Invalid</c> while the panel
/// shows, so a click on a button reaches the ground behind it. This layer carries no movie: its two hit tests answer the shield rule, and
/// <c>ScreenManager.EarlyUpdate</c> gives the mouse buttons to the first active layer whose hit test is true and whose mask names them
/// (ScreenManager.cs:642-662). The adapter sets the mask to the mouse buttons and the wheel while the cursor shows, and to <c>Invalid</c> otherwise.
/// The wheel goes with the buttons so the scene layer is not hit on a shielded frame (ScreenManager.cs:663-668) and vanilla's own mouse reads stay off. <c>IsKeyPressed(LeftMouseButton)</c> still reads true on the focused scene layer, which the order placer ignores while the shield suspends it.
/// </summary>
internal sealed class SeatCursorLayer : GauntletLayer
{
    private readonly Func<bool> _holds;
    private readonly Func<float, float, bool> _holdsAt;

    internal SeatCursorLayer(string name, int order, Func<bool> holds, Func<float, float, bool> holdsAt)
        : base(name, order)
    {
        _holds = holds;
        _holdsAt = holdsAt;
    }

    public override bool HitTest() => _holds();

    // ScreenManager.IsLayerBlockedAtPosition asks the layers above another layer whether they cover a point (ScreenManager.cs:808-828).
    public override bool HitTest(Vector2 position) => _holdsAt(position.X, position.Y);
}
