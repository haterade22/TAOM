namespace TAOM.Features.Messengers;

public class MessengerSettingsProvider : IMessengerSettingsProvider
{
    public bool EnableMessengers => TaomSettings.Instance?.EnableMessengers ?? true;

    // Codex review #34 round 2: MCM JSON is editable on disk, so a player-edited negative cost would let
    // GiveGoldAction.ApplyBetweenCharacters(player, null, -100) GRANT the player 100 gold while still sending
    // a messenger. Clamp to MCM's documented range here so every consumer (validation, deduction, dialog cost
    // text, hint) sees the same sanitized value.
    public int MessengerGoldCost => Clamp(TaomSettings.Instance?.MessengerGoldCost ?? 50, min: 10, max: 500, fallback: 50);
    public int MessengerTravelDays => Clamp(TaomSettings.Instance?.MessengerTravelDays ?? 3, min: 1, max: 10, fallback: 3);

    public bool MessengerAccidents => TaomSettings.Instance?.MessengerAccidents ?? true;

    private static int Clamp(int value, int min, int max, int fallback)
    {
        if (value < min || value > max)
            return fallback;
        return value;
    }
}
