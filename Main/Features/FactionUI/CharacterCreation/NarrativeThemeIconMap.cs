using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.CharacterCreation;

namespace TAOM.Features.FactionUI.CharacterCreation;

/// <summary>
/// The skill icon each backstory option shows in the themed backstory screens (#704), from Kysaro's
/// <c>NarrativeThemeIcons</c>: an option's first listed skill. The options are TAOM's own, read through
/// <see cref="INarrativeDataProvider"/> like the backstory menus themselves. An option's on-screen text
/// is <c>{=taom_cc_&lt;string_id&gt;_text}&lt;text&gt;</c> (NarrativeMenuBuilder.cs:74), so the map is keyed
/// on that string in the player's language, rebuilt when the language changes; Kysaro's keyed on the
/// raw English and matched nothing in any other language. The icon is vanilla's skill sprite name,
/// which Kysaro's skill icons replace when they are on.
/// </summary>
public sealed class NarrativeThemeIconMap
{
    private const string SkillSpritePrefix = @"SPGeneral\Skills\gui_skills_icon_";
    private const string SkillSpriteSuffix = "_small";

    private static readonly string[] Menus = { "parents", "childhood", "education", "youth", "adulthood" };

    private readonly INarrativeDataProvider _menus;
    private readonly ITextLocalizerAdapter _localizer;
    private Dictionary<string, string>? _skillByText;
    private string? _builtForLanguage;

    public NarrativeThemeIconMap(INarrativeDataProvider menus, ITextLocalizerAdapter localizer)
    {
        _menus = menus;
        _localizer = localizer;
    }

    /// <summary>The skill sprite for an option's on-screen text, or null when it has none.</summary>
    public string? SkillIconFor(string? actionText)
    {
        if (string.IsNullOrEmpty(actionText))
            return null;

        var language = _localizer.ActiveLanguage;
        if (_skillByText == null || _builtForLanguage != language)
        {
            _skillByText = Build();
            _builtForLanguage = language;
        }
        return _skillByText.TryGetValue(actionText!, out var skill)
            ? SkillSpritePrefix + skill + SkillSpriteSuffix
            : null;
    }

    private Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var menu in Menus)
        {
            foreach (var option in _menus.LoadMenuOptions(menu))
            {
                var firstSkill = option.Skills.FirstOrDefault();
                if (option.StringId.Length == 0 || option.Text.Length == 0 || string.IsNullOrEmpty(firstSkill))
                    continue;

                var shown = _localizer.Localize($"{{=taom_cc_{option.StringId}_text}}{option.Text}");
                if (shown.Length > 0)
                    map[shown] = firstSkill!.ToLowerInvariant();
            }
        }
        return map;
    }
}
