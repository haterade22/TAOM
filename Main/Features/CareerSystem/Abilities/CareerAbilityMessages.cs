using TaleWorlds.Localization;

namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>
/// The career-ability toasts (#1385). The key name and the seconds are text variables, so a translation
/// keeps them. The key is the one the player bound (<see cref="IAbilityInputAdapter.ActivationKeyName"/>),
/// not a fixed V.
/// </summary>
internal static class CareerAbilityMessages
{
    /// <summary>The ready toast. An empty <paramref name="keyName"/> means no key is bound.</summary>
    public static TextObject Ready(string keyName) =>
        string.IsNullOrEmpty(keyName)
            ? new TextObject("{=taom_career_ability_ready_unbound}Career ability is ready! Bind its key under Options, Keybindings to activate it.")
            : new TextObject("{=taom_career_ability_ready}Career ability is ready! Press {KEY} to activate.").SetTextVariable("KEY", keyName);

    public static TextObject Charging(int seconds) =>
        new TextObject("{=taom_career_ability_charging}Career ability still charging — {SECONDS}s remaining.").SetTextVariable("SECONDS", seconds);
}
