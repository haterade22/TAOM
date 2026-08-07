namespace TAOM.Adapters;

/// <summary>Wraps the static GameMenu switching surface for the enlistment wait-menu layer.</summary>
public interface IGameMenuAdapter
{
    /// <summary>StringId of the active game menu, or null when not at a menu.</summary>
    string CurrentMenuId { get; }

    bool SwitchTo(string menuId);

    bool Activate(string menuId);

    /// <summary>
    /// Open the given menu and return true ONLY when it is verifiably current afterwards.
    ///
    /// Neither underlying call is trustworthy on its own: `GameMenu.SwitchToMenu` silently
    /// no-ops when `Campaign.Current.CurrentMenuContext` is null (it only `Debug.FailedAssert`s,
    /// which is inert in release), while `GameMenu.ActivateGameMenu` only acts when the context
    /// IS null. This tries the switch, falls back to activation, and confirms against the
    /// observable menu id rather than the absence of an exception.
    /// </summary>
    bool EnsureMenuOpen(string menuId);

    bool ExitToLast();
}
