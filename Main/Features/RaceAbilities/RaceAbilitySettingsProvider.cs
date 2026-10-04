namespace TAOM.Features.RaceAbilities;

// The MCM switches (Battle Tactics/Race Abilities in TaomSettings), read live. Enabled decides each
// mission's gate on its first tick and every tree's decision; switching it off mid-battle stops new
// activations at once while running ones finish.
public class RaceAbilitySettingsProvider
{
    public bool Enabled => TaomSettings.Instance?.EnableRaceAbilities ?? true;

    public bool WarCries => TaomSettings.Instance?.RaceAbilityWarCries ?? true;

    public bool Messages => TaomSettings.Instance?.RaceAbilityMessages ?? true;

    // One log line per wave and per phase change, on top of the 30 s and mission-end reports.
    public bool DebugLog => TaomSettings.Instance?.RaceAbilityDebugLog ?? false;
}
