using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.Stamina;

/// <summary>The player's words for stamina (rows taom_stamina_* in taom_module_strings.xml), shown for the player's own soldier only.</summary>
public static class StaminaTexts
{
    /// <summary>Once per battle, the first time the player's soldier becomes Tired.</summary>
    public static TextObject Tired() => new("{=taom_stamina_tired}You are tiring. Ease off before your strength fails.");

    /// <summary>Each time the player's soldier becomes Exhausted.</summary>
    public static TextObject Exhausted() => new("{=taom_stamina_exhausted}Your strength fails you. Fall back and catch your breath.");

    public static void ShowTired() =>
        InformationManager.DisplayMessage(new InformationMessage(Tired().ToString(), Colors.Yellow));

    public static void ShowExhausted() =>
        InformationManager.DisplayMessage(new InformationMessage(Exhausted().ToString(), Colors.Red));
}
