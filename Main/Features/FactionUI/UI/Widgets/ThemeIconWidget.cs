using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>The skill icon beside a themed backstory option (#704, Kysaro's): the option's first skill,
/// looked up from its on-screen <see cref="ActionText"/> by <c>NarrativeThemeIconMap</c> whenever the
/// bound text changes (a new menu, a new language). Nothing runs per frame.</summary>
public class ThemeIconWidget : Widget
{
    private string? _actionText;

    public ThemeIconWidget(UIContext context)
        : base(context)
    {
    }

    [Editor(false)]
    public string? ActionText
    {
        get => _actionText;
        set
        {
            if (value == _actionText)
                return;
            _actionText = value;
            var spriteName = FactionUIWidgetContext.ThemeIcons?.SkillIconFor(value);
            Sprite = spriteName == null ? null : Context.SpriteData.GetSprite(spriteName);
        }
    }
}
