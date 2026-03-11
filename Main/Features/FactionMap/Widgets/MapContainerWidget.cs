using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionMap.Widgets;

public class MapContainerWidget : Widget
{
    private const float MapAspect = 6166f / 4096f;

    public MapContainerWidget(UIContext context) : base(context) { }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        if (ParentWidget == null) return;

        float parentW = ParentWidget.Size.X;
        float parentH = ParentWidget.Size.Y;
        if (parentW <= 0 || parentH <= 0) return;

        float parentAspect = parentW / parentH;

        float targetW, targetH;
        if (parentAspect > MapAspect)
        {
            targetH = parentH;
            targetW = targetH * MapAspect;
        }
        else
        {
            targetW = parentW;
            targetH = targetW / MapAspect;
        }

        float marginX = (parentW - targetW) / 2f;
        float marginY = (parentH - targetH) / 2f;

        float invScale = _scaleToUse > 0f ? 1f / _scaleToUse : 1f;
        MarginLeft = Math.Max(0, marginX * invScale);
        MarginRight = Math.Max(0, marginX * invScale);
        MarginTop = Math.Max(0, marginY * invScale);
        MarginBottom = Math.Max(0, marginY * invScale);
    }
}
