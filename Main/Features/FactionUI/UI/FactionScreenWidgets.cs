using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The faction screen's live touches (#704), from Kysaro's <c>FogDrift</c>, <c>PortraitReveal</c>,
/// <c>MinimapPopup</c> and <c>RightColumnScroll</c>: two layers of drifting fog, the 3D character
/// sliding in on each new faction or pick, the minimap opening into a large
/// clickable map, and the right column scrolling back to the top on a new faction. Widgets are found by
/// id once the screen is built.
/// </summary>
public sealed class FactionScreenWidgets
{
    private const float FogWidth = 2200f;
    private const float FogSpeedNear = 14f;
    private const float FogSpeedFar = 8f;
    private const float SlideOffset = 700f;
    private const float SlideDuration = 0.75f;
    private const float MinimapDuration = 0.35f;
    private const float MinimapIgnoreClicksFor = 0.35f;
    private const float MinimapInteractable = 0.9f;
    private const float MinimapSmallWidth = 480f;
    private const float MinimapSmallHeight = 230f;

    private Widget? _root;

    private Widget? _fogNear;
    private Widget? _fogNear2;
    private Widget? _fogFar;
    private Widget? _fogFar2;
    private float _fogNearTime;
    private float _fogFarTime;

    private Widget? _viewport;
    private float _portraitTime = -1f;

    private Widget? _popup;
    private Widget? _popupButton;
    private Widget? _popupPin;
    private Widget? _smallPin;
    private Widget? _popupMap;
    private Widget? _closeCatcher;
    private float _minimapTime;
    private bool _minimapOpen;
    private bool _haveClosedOffset;
    private float _closedOffsetX;
    private float _closedOffsetY;
    private float _ignoreClicksRemaining;

    private ScrollablePanel? _rightColumn;

    /// <summary>The faction screen is about to load on <paramref name="layer"/>; its widgets are looked
    /// up when first needed.</summary>
    public void Attach(GauntletLayer layer)
    {
        Detach();
        _root = layer.UIContext?.Root;
        _ignoreClicksRemaining = MinimapIgnoreClicksFor;
    }

    public void Detach()
    {
        if (_viewport != null)
        {
            _viewport.AlphaFactor = 1f;
            _viewport.PositionXOffset = 0f;
        }
        _root = _fogNear = _fogNear2 = _fogFar = _fogFar2 = _viewport = null;
        _popup = _popupButton = _popupPin = _smallPin = _popupMap = _closeCatcher = null;
        _rightColumn = null;
        _fogNearTime = _fogFarTime = 0f;
        _portraitTime = -1f;
        _minimapTime = 0f;
        _minimapOpen = false;
        _haveClosedOffset = false;
    }

    public void OnCharacterShown() => _portraitTime = 0f;

    public void ResetRightColumnToTop()
    {
        if (_root == null)
            return;
        _rightColumn ??= Find("RightColumnScroll") as ScrollablePanel;
        var scrollbar = _rightColumn?.VerticalScrollbar;
        if (scrollbar != null)
            scrollbar.ValueFloat = scrollbar.MinValue;
    }

    public void ToggleMinimap()
    {
        if (!(_ignoreClicksRemaining > 0f))
            _minimapOpen = !_minimapOpen;
    }

    /// <summary>Where on the large map the mouse is, normalized 0 to 1, once the map is open enough
    /// to be clicked.</summary>
    public bool TryGetMinimapClick(out float x, out float y)
    {
        x = y = 0f;
        if (_popupMap == null || _minimapTime < MinimapInteractable)
            return false;
        var width = _popupMap.Size.X;
        var height = _popupMap.Size.Y;
        if (!(width > 0f) || !(height > 0f))
            return false;
        var mouse = Input.MousePositionPixel;
        x = Math.Max(0f, Math.Min(1f, (mouse.X - _popupMap.GlobalPosition.X) / width));
        y = Math.Max(0f, Math.Min(1f, (mouse.Y - _popupMap.GlobalPosition.Y) / height));
        return true;
    }

    public void Tick(float dt)
    {
        if (_root == null)
            return;
        try
        {
            TickFog(dt);
            TickPortrait(dt);
            TickMinimap(dt);
        }
        catch (Exception)
        {
            Detach();
        }
    }

    private void TickFog(float dt)
    {
        _fogNear ??= Find("FogA");
        _fogNear2 ??= Find("FogB");
        _fogFar ??= Find("FogFarA");
        _fogFar2 ??= Find("FogFarB");
        if (_fogNear == null || _fogNear2 == null)
            return;

        _fogNearTime += dt * FogSpeedNear;
        _fogFarTime += dt * FogSpeedFar;
        PlaceFog(_fogNear, _fogNear2, _fogNearTime);
        if (_fogFar != null && _fogFar2 != null)
            PlaceFog(_fogFar, _fogFar2, _fogFarTime);
    }

    private static void PlaceFog(Widget first, Widget second, float travelled)
    {
        var shift = travelled % FogWidth;
        first.PositionXOffset = -shift;
        second.PositionXOffset = FogWidth - shift;
    }

    private void TickPortrait(float dt)
    {
        if (_portraitTime < 0f)
            return;
        _viewport ??= Find("CharacterViewportWrapper");
        if (_viewport == null)
        {
            _portraitTime = -1f;
            return;
        }

        _portraitTime += dt;
        var eased = EaseOut(_portraitTime, SlideDuration, out var done);
        _viewport.PositionXOffset = SlideOffset * (1f - eased);
        _viewport.AlphaFactor = eased;
        if (done)
            _portraitTime = -1f;
    }

    private void TickMinimap(float dt)
    {
        if (_ignoreClicksRemaining > 0f)
            _ignoreClicksRemaining -= dt;
        _popup ??= Find("MinimapPopup");
        _popupButton ??= Find("MinimapButton");
        _popupPin ??= Find("MinimapPopupPin");
        _smallPin ??= Find("MinimapPin");
        _popupMap ??= Find("MinimapPopupMap");
        _closeCatcher ??= Find("MinimapCloseCatcher");
        if (_popup == null || _popupButton == null)
            return;

        var target = _minimapOpen ? 1f : 0f;
        if (_minimapTime <= 0f && target <= 0f)
            return;

        var step = dt / MinimapDuration;
        _minimapTime = target > _minimapTime ? Math.Min(target, _minimapTime + step) : Math.Max(target, _minimapTime - step);
        var eased = _minimapTime * _minimapTime * (3f - 2f * _minimapTime);

        if (!_haveClosedOffset)
        {
            _closedOffsetX = _popupButton.GlobalPosition.X + _popupButton.Size.X * 0.5f - (_root!.GlobalPosition.X + _root.Size.X * 0.5f);
            _closedOffsetY = _popupButton.GlobalPosition.Y + _popupButton.Size.Y * 0.5f - (_root.GlobalPosition.Y + _root.Size.Y * 0.5f);
            _haveClosedOffset = true;
        }

        _popup.SuggestedWidth = MinimapSmallWidth + MinimapSmallWidth * eased;
        _popup.SuggestedHeight = MinimapSmallHeight + MinimapSmallHeight * eased;
        _popup.AlphaFactor = eased;
        _popup.PositionXOffset = _closedOffsetX * (1f - eased);
        _popup.PositionYOffset = _closedOffsetY * (1f - eased);
        _popup.IsVisible = _minimapTime > 0f;
        if (_closeCatcher != null)
            _closeCatcher.IsVisible = _minimapTime > 0f;
        if (_popupPin != null)
            _popupPin.AlphaFactor = eased > 0.85f ? (eased - 0.85f) / 0.15f : 0f;
        if (_smallPin != null)
            _smallPin.AlphaFactor = 1f - Math.Min(1f, eased * 2f);
        _popupButton.AlphaFactor = 1f - Math.Min(1f, eased * 2f);
        _popupButton.IsVisible = eased < 0.5f;
    }

    private Widget? Find(string id) => _root?.GetFirstInChildrenAndThisRecursive(w => w.Id == id);

    private static float EaseOut(float time, float duration, out bool done)
    {
        var progress = Math.Min(1f, time / duration);
        done = progress >= 1f;
        return 1f - (1f - progress) * (1f - progress);
    }
}
