using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CheatMenu.Hooks;

namespace TAOM.Features.CheatMenu.Battle;

/// <summary>
/// The one place that decides whether the battle cheat menu shows and opens: the MCM switch and single player, as the campaign
/// menu (<see cref="CheatMenuPolicy.IsMenuAvailable"/>). The Esc-menu item (Patch152, TAOM.Client) asks <see cref="IsShown"/>
/// each time the Esc menu builds its items and calls <see cref="TryOpen"/> on a click; a key reader calls
/// <see cref="TryOpenOnKey"/>, which reads no key at all while the menu is not shown.
/// </summary>
public sealed class BattleCheatOpener
{
    /// <summary>The list's entries, in order.</summary>
    public static readonly IReadOnlyList<BattleCheatAction> Actions = new[]
    {
        BattleCheatAction.HealSelf,
        BattleCheatAction.HealSide,
        BattleCheatAction.Rout,
        BattleCheatAction.Kill,
        BattleCheatAction.SpawnAllies,
        BattleCheatAction.SpawnEnemies,
    };

    private readonly ICheatMenuSettingsProvider _settings;
    private readonly BattleCheatService _service;
    private readonly IBattleCheatHostAdapter _host;

    public BattleCheatOpener(ICheatMenuSettingsProvider settings, BattleCheatService service, IBattleCheatHostAdapter host)
    {
        _settings = settings;
        _service = service;
        _host = host;
    }

    public bool IsShown => CheatMenuPolicy.IsMenuAvailable(_settings.EnableCheatMenu, _service.IsSinglePlayer);

    /// <summary>Shows the list when the menu is shown. A pick runs the service and shows its outcome.</summary>
    public bool TryOpen()
    {
        if (!IsShown) return false;
        _host.ShowList(Actions, Pick);
        return true;
    }

    /// <summary>For a key reader: switch and single player first, then the key, then <see cref="TryOpen"/>.</summary>
    public bool TryOpenOnKey(Func<bool> keyPressed)
    {
        if (!IsShown) return false;
        return keyPressed() && TryOpen();
    }

    /// <summary>One list pick, inside the campaign menu's guard: a cheat that throws shows a warning instead of escaping the click.</summary>
    internal void Pick(BattleCheatAction action) =>
        CheatMenuMenus.Guarded("battle_" + action, () => _host.ShowOutcome(action, _service.Run(action)));

    /// <summary>
    /// Pure: where the Esc menu's "Cheats" item goes. -1 (no item) when the menu is not shown or the list already holds it; else
    /// just before "Photo Mode" (compared as shown, in the player's language), or at the end when there is no Photo Mode item.
    /// </summary>
    public static int EscapeItemIndex(bool shown, IReadOnlyList<string?> itemTexts, string photoModeText, string cheatsText)
    {
        if (!shown) return -1;
        for (var i = 0; i < itemTexts.Count; i++)
            if (string.Equals(itemTexts[i], cheatsText, StringComparison.Ordinal)) return -1;
        for (var i = 0; i < itemTexts.Count; i++)
            if (string.Equals(itemTexts[i], photoModeText, StringComparison.Ordinal)) return i;
        return itemTexts.Count;
    }
}
