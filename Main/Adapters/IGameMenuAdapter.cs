namespace TAOM.Adapters;

/// <summary>Wraps the static GameMenu switching surface for the enlistment wait-menu layer.</summary>
public interface IGameMenuAdapter
{
    /// <summary>StringId of the active game menu, or null when not at a menu.</summary>
    string CurrentMenuId { get; }

    bool SwitchTo(string menuId);

    bool Activate(string menuId);

    bool ExitToLast();
}
