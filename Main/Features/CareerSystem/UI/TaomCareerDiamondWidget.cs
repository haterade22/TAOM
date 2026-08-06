using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace TAOM.Features.CareerSystem.UI;

/// <summary>
/// Issue #388 — one career-choice diamond on the career screen.
///
/// Extends plain <see cref="Widget"/>, NOT <c>ImageWidget</c>: ImageWidget ignores the
/// <c>Color</c> attribute, which silently kills every state tint, while a plain Widget
/// multiplies its Sprite by Color (the same mechanism the original pip strip relied on).
///
/// Drives three children found by Id: "DiamondGlow" (steady ring while the choice is taken),
/// "DiamondTooltip" (shown while the cursor is inside this diamond's measured rect — polling
/// the point directly rather than using hover events, because the +/- overlay buttons steal
/// hover focus), and the keystone icon tint.
///
/// Named with the Taom prefix deliberately: <c>WidgetFactory._builtinTypes</c> is keyed on the
/// SIMPLE type name across every loaded assembly (namespace ignored, verified in the v1.4.7
/// decompile), so an unprefixed CareerDiamondWidget would collide with the external reference
/// module's class of the same name if both were ever loaded.
///
/// Registration is automatic: <c>WidgetInfo.CollectWidgetTypes()</c> scans every loaded
/// assembly that references TaleWorlds.GauntletUI and collects all Widget subclasses, so
/// defining this type inside TAOM.dll is sufficient — there is no register call.
/// </summary>
public class TaomCareerDiamondWidget : Widget
{
    // Keystones carry the faction sigil — render it silver-white so it reads apart from the
    // gold passive icons.
    private static readonly Color KeystoneTakenTint = new Color(0.93f, 0.96f, 1f, 1f);
    private static readonly Color KeystoneFreeTint = new Color(0.87f, 0.91f, 0.97f, 0.95f);

    private bool _isTakenState;
    private bool _isKeystoneState;
    private bool _childrenResolved;
    private bool _keystoneTintApplied;
    private Widget _glowWidget;
    private Widget _tooltipWidget;
    private Widget _iconTaken;
    private Widget _iconFree;

    public TaomCareerDiamondWidget(UIContext context) : base(context)
    {
    }

    [Editor(false)]
    public bool IsTakenState
    {
        get => _isTakenState;
        set
        {
            if (_isTakenState != value)
            {
                _isTakenState = value;
                OnPropertyChanged(value, nameof(IsTakenState));
            }
        }
    }

    [Editor(false)]
    public bool IsKeystoneState
    {
        get => _isKeystoneState;
        set
        {
            if (_isKeystoneState != value)
            {
                _isKeystoneState = value;
                OnPropertyChanged(value, nameof(IsKeystoneState));
            }
        }
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        if (!_childrenResolved)
        {
            _glowWidget = FindById(this, "DiamondGlow");
            _tooltipWidget = FindById(this, "DiamondTooltip");
            _iconTaken = FindById(this, "IconTaken");
            _iconFree = FindById(this, "IconFree");

            // Latch ONLY once the tree actually produced the children. This runs on the first
            // OnLateUpdate, which for a widget built from a ListPanel ItemTemplate can fire
            // before the template has populated its children — latching unconditionally then
            // pinned _glowWidget at null for the widget's whole life, so a taken diamond never
            // got its bright rim (and, since the locked/available layers hide when taken, it
            // lost its rim entirely). Keep retrying until they resolve.
            _childrenResolved = _glowWidget != null && _tooltipWidget != null;
        }

        if (_isKeystoneState && !_keystoneTintApplied)
        {
            _keystoneTintApplied = true;
            if (_iconTaken != null) _iconTaken.Color = KeystoneTakenTint;
            if (_iconFree != null) _iconFree.Color = KeystoneFreeTint;
        }

        if (_tooltipWidget != null)
            _tooltipWidget.IsVisible = IsPointInsideMeasuredArea(EventManager.MousePosition);

        if (_glowWidget != null)
        {
            // Steady luminescence, no pulse — an animated breath read as distracting in play.
            _glowWidget.IsVisible = _isTakenState;
            _glowWidget.AlphaFactor = _isKeystoneState ? 1.0f : 0.8f;
        }
    }

    private static Widget FindById(Widget root, string id)
    {
        for (var i = 0; i < root.ChildCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null) continue;
            if (child.Id == id) return child;

            var nested = FindById(child, id);
            if (nested != null) return nested;
        }

        return null;
    }
}
