using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class GameMenuAdapter : IGameMenuAdapter
{
    private readonly IModLogger _logger;

    public GameMenuAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string CurrentMenuId => Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;

    public bool SwitchTo(string menuId)
    {
        if (string.IsNullOrEmpty(menuId))
            return false;
        try
        {
            GameMenu.SwitchToMenu(menuId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] SwitchTo('{menuId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool Activate(string menuId)
    {
        if (string.IsNullOrEmpty(menuId))
            return false;
        try
        {
            GameMenu.ActivateGameMenu(menuId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] Activate('{menuId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool EnsureMenuOpen(string menuId)
    {
        if (string.IsNullOrEmpty(menuId))
            return false;

        try
        {
            if (CurrentMenuId == menuId)
                return true;

            // Switch works only when a menu context already exists; it no-ops silently otherwise.
            GameMenu.SwitchToMenu(menuId);
            if (CurrentMenuId == menuId)
                return true;

            // Activate is the complement — it acts only when there is no current context.
            GameMenu.ActivateGameMenu(menuId);
            if (CurrentMenuId == menuId)
                return true;

            _logger?.LogError($"[Enlistment] EnsureMenuOpen('{menuId}') — menu is still '{CurrentMenuId}' after switch and activate");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] EnsureMenuOpen('{menuId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool ExitToLast()
    {
        try
        {
            GameMenu.ExitToLast();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] ExitToLast failed: {ex.Message}");
            return false;
        }
    }
}
